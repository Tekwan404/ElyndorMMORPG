using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Characters;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Monsters;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Raids;
using Elyndor.Infrastructure.World;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elyndor.IntegrationTests.Raids;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class RaidCombatStartTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 21, 20, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LeaderStartsOneSharedSessionForCapturedRaidRosterAndReconnectsEveryParticipant()
    {
        SeededRaidPlayers seeded = await CreateRaidPlayersAsync(3);
        await using WebApplicationFactory<Program> factory = CreateFactory();
        CharacterResponse[] characters = await CreateCharactersAsync(factory, seeded);
        (string locationId, string monsterId) = ResolveRaidEncounter(factory);
        await RelocateAsync(characters.Select(character => character.Id), locationId);

        using IServiceScope scope = factory.Services.CreateScope();
        RaidService raids = scope.ServiceProvider.GetRequiredService<RaidService>();
        await CreateRaidAsync(raids, seeded.AccountIds, characters.Select(character => character.Id).ToArray());
        WorldEncounterRegistry encounters = scope.ServiceProvider.GetRequiredService<WorldEncounterRegistry>();
        RaidCombatApplicationService combat = scope.ServiceProvider.GetRequiredService<RaidCombatApplicationService>();
        PendingWorldEncounter pending = encounters.Register(
            seeded.AccountIds[0],
            locationId,
            monsterId);

        CombatOperationResult started = await combat.StartAsync(
            seeded.AccountIds[0],
            pending.EncounterId,
            CancellationToken.None);

        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.NotNull(started.Snapshot);
        Assert.Equal(CombatParticipantLimit.MaximumRaid, 20);
        Assert.Equal(3, started.Snapshot!.ParticipantRoster?.Count);
        Assert.All(
            started.Snapshot.ParticipantRoster!,
            participant => Assert.Equal(
                CombatParticipantStatus.Active,
                participant.Status));
        Assert.Equal(
            characters.Select(character => character.Id).Order().ToArray(),
            started.Snapshot.ParticipantRoster!
                .Select(participant => participant.CharacterId)
                .Order()
                .ToArray());
        Assert.False(encounters.TryConsume(
            seeded.AccountIds[0],
            pending.EncounterId,
            out _));

        Guid sessionId = started.Snapshot.SessionId;
        for (var index = 0; index < seeded.AccountIds.Length; index++)
        {
            CombatOperationResult resumed = combat.Resume(seeded.AccountIds[index]);
            Assert.True(resumed.Succeeded, resumed.ErrorCode);
            Assert.NotNull(resumed.Snapshot);
            Assert.Equal(sessionId, resumed.Snapshot!.SessionId);
            Assert.Equal(characters[index].Id, resumed.Snapshot.Player.ActorId);
            Assert.Equal(characters[index].Id, resumed.Snapshot.PlayerContribution?.CharacterId);
            Assert.Equal(
                characters.Select(character => character.Id).Order().ToArray(),
                resumed.Snapshot.ParticipantRoster!
                    .Select(participant => participant.CharacterId)
                    .Order()
                    .ToArray());
        }

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            characters.Length,
            await verify.ActiveCombatSessions.CountAsync(
                state => state.SessionId == sessionId));
    }

    [Fact]
    public async Task NonLeaderCannotStartAndDoesNotConsumeTheirEncounter()
    {
        SeededRaidPlayers seeded = await CreateRaidPlayersAsync(2);
        await using WebApplicationFactory<Program> factory = CreateFactory();
        CharacterResponse[] characters = await CreateCharactersAsync(factory, seeded);
        (string locationId, string monsterId) = ResolveRaidEncounter(factory);
        await RelocateAsync(characters.Select(character => character.Id), locationId);

        using IServiceScope scope = factory.Services.CreateScope();
        RaidService raids = scope.ServiceProvider.GetRequiredService<RaidService>();
        await CreateRaidAsync(raids, seeded.AccountIds, characters.Select(character => character.Id).ToArray());
        WorldEncounterRegistry encounters = scope.ServiceProvider.GetRequiredService<WorldEncounterRegistry>();
        RaidCombatApplicationService combat = scope.ServiceProvider.GetRequiredService<RaidCombatApplicationService>();
        PendingWorldEncounter pending = encounters.Register(
            seeded.AccountIds[1],
            locationId,
            monsterId);

        CombatOperationResult result = await combat.StartAsync(
            seeded.AccountIds[1],
            pending.EncounterId,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(RaidCombatRosterErrorCodes.NotLeader, result.ErrorCode);
        Assert.True(encounters.TryConsume(
            seeded.AccountIds[1],
            pending.EncounterId,
            out PendingWorldEncounter preserved));
        Assert.Equal(monsterId, preserved.MonsterId);
        Assert.Equal(locationId, preserved.LocationId);
    }

    [Fact]
    public async Task MissingRaidIsRejectedBeforeEncounterConsumption()
    {
        SeededRaidPlayers seeded = await CreateRaidPlayersAsync(1);
        await using WebApplicationFactory<Program> factory = CreateFactory();
        CharacterResponse[] characters = await CreateCharactersAsync(factory, seeded);
        (string locationId, string monsterId) = ResolveRaidEncounter(factory);
        await RelocateAsync([characters[0].Id], locationId);

        using IServiceScope scope = factory.Services.CreateScope();
        WorldEncounterRegistry encounters = scope.ServiceProvider.GetRequiredService<WorldEncounterRegistry>();
        RaidCombatApplicationService combat = scope.ServiceProvider.GetRequiredService<RaidCombatApplicationService>();
        PendingWorldEncounter pending = encounters.Register(
            seeded.AccountIds[0],
            locationId,
            monsterId);

        CombatOperationResult result = await combat.StartAsync(
            seeded.AccountIds[0],
            pending.EncounterId,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(RaidCombatRosterErrorCodes.RaidNotFound, result.ErrorCode);
        Assert.True(encounters.TryConsume(
            seeded.AccountIds[0],
            pending.EncounterId,
            out _));
    }

    [Fact]
    public void RaidAndPartyParticipantLimitsRemainIndependent()
    {
        Assert.Equal(5, CombatParticipantLimit.DefaultParty);
        Assert.Equal(20, CombatParticipantLimit.MaximumRaid);
        Assert.Equal(
            CombatParticipantLimit.DefaultParty,
            CombatParticipantRoster.DefaultMaximumParticipants);
    }

    private async Task<SeededRaidPlayers> CreateRaidPlayersAsync(int count)
    {
        Guid[] accountIds = new Guid[count];
        long[] telegramUserIds = new long[count];
        await using GameDbContext context = postgres.CreateDbContext();
        for (var index = 0; index < count; index++)
        {
            accountIds[index] = Guid.CreateVersion7();
            telegramUserIds[index] = 9800 + index;
            context.Accounts.Add(new Account(accountIds[index], telegramUserIds[index], Now));
        }
        await context.SaveChangesAsync();
        return new SeededRaidPlayers(accountIds, telegramUserIds);
    }

    private static async Task<CharacterResponse[]> CreateCharactersAsync(
        WebApplicationFactory<Program> factory,
        SeededRaidPlayers seeded)
    {
        CharacterResponse[] characters = new CharacterResponse[seeded.AccountIds.Length];
        for (var index = 0; index < seeded.AccountIds.Length; index++)
        {
            using HttpClient client = CreateAuthenticatedClient(
                factory,
                seeded.AccountIds[index],
                seeded.TelegramUserIds[index]);
            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/character",
                new CreateCharacterRequest(
                    Guid.CreateVersion7(),
                    $"RaidStart{index}",
                    "HUMAN",
                    index % 2 == 0 ? "MALE" : "FEMALE",
                    index == 0 ? "WARRIOR" : "MAGE"));
            response.EnsureSuccessStatusCode();
            CharacterResponse? character =
                await response.Content.ReadFromJsonAsync<CharacterResponse>();
            Assert.NotNull(character);
            characters[index] = character;
        }
        return characters;
    }

    private static async Task CreateRaidAsync(
        RaidService raids,
        IReadOnlyList<Guid> accountIds,
        IReadOnlyList<Guid> characterIds)
    {
        RaidOperationResult created = await raids.CreateAsync(
            accountIds[0],
            Guid.CreateVersion7(),
            CancellationToken.None);
        Assert.True(created.IsSuccess, created.ErrorCode);
        for (var index = 1; index < accountIds.Count; index++)
        {
            RaidOperationResult invited = await raids.InviteAsync(
                accountIds[0],
                Guid.CreateVersion7(),
                characterIds[index],
                CancellationToken.None);
            Assert.True(invited.IsSuccess, invited.ErrorCode);
            RaidOperationResult joined = await raids.AcceptInviteAsync(
                accountIds[index],
                invited.Invite!.Id,
                CancellationToken.None);
            Assert.True(joined.IsSuccess, joined.ErrorCode);
        }
    }

    private async Task RelocateAsync(IEnumerable<Guid> characterIds, string locationId)
    {
        Guid[] ids = characterIds.ToArray();
        await using GameDbContext context = postgres.CreateDbContext();
        CharacterLocation[] locations = await context.CharacterLocations
            .Where(location => ids.Contains(location.CharacterId))
            .ToArrayAsync();
        Assert.Equal(ids.Length, locations.Length);
        foreach (CharacterLocation location in locations)
            location.Relocate(locationId, Now);
        await context.SaveChangesAsync();
    }

    private static (string LocationId, string MonsterId) ResolveRaidEncounter(
        WebApplicationFactory<Program> factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IContentSnapshotProvider content = scope.ServiceProvider.GetRequiredService<IContentSnapshotProvider>();
        GameContentSnapshot snapshot = content.GetCurrent();
        foreach (var location in snapshot.Indexes.LocationsById.Values)
        {
            foreach (var encounter in location.Encounters ?? [])
            {
                if (snapshot.Indexes.MonstersById.TryGetValue(
                        encounter.MonsterId,
                        out MonsterDefinition? monster)
                    && monster.Rank is MonsterRank.Elite or MonsterRank.Boss)
                {
                    return (location.Id, monster.Id);
                }
            }
        }
        throw new InvalidOperationException("No open-world elite or boss encounter is available for raid combat tests.");
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
                builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
                builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
                builder.UseSetting(
                    "Authentication:SigningKey",
                    "raid-combat-start-test-signing-key-with-more-than-32-bytes");
                builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
                builder.UseSetting("Authentication:Development:Enabled", "false");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
                });
            });

    private static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> factory,
        Guid accountId,
        long telegramUserId)
    {
        JwtTokenIssuer tokenIssuer =
            factory.Services.GetRequiredService<JwtTokenIssuer>();
        IssuedAccessToken token = tokenIssuer.Issue(accountId, telegramUserId);
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);
        return client;
    }

    private sealed record SeededRaidPlayers(Guid[] AccountIds, long[] TelegramUserIds);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
