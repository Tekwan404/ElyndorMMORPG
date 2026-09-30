using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Characters;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Pvp;
using Elyndor.Infrastructure.World;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elyndor.IntegrationTests.Combat;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ArenaAdmissionRaceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 30, 7, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ArenaVsSoloPveLeavesExactlyOneRuntimeOwner()
    {
        Guid accountId = Guid.CreateVersion7();
        long telegramUserId = 9701;
        await SeedAccountAsync(accountId, telegramUserId);

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = CreateAuthenticatedClient(factory, accountId, telegramUserId);
        CharacterResponse character = await CreateCharacterAsync(client, "AdmissionSolo");
        await RelocateAsync(character.Id, "WHISPERING_FOREST");

        using IServiceScope scope = factory.Services.CreateScope();
        CombatApplicationService combat = scope.ServiceProvider.GetRequiredService<CombatApplicationService>();
        CombatSessionRegistry pve = scope.ServiceProvider.GetRequiredService<CombatSessionRegistry>();
        WorldEncounterRegistry encounters = scope.ServiceProvider.GetRequiredService<WorldEncounterRegistry>();
        CharacterOperationGuard guard = scope.ServiceProvider.GetRequiredService<CharacterOperationGuard>();
        ArenaMatchRuntime arena = scope.ServiceProvider.GetRequiredService<ArenaMatchRuntime>();

        PendingWorldEncounter pending = encounters.Register(
            accountId,
            "WHISPERING_FOREST",
            "WHISPERING_FOREST_MOLODOI_VOLK_L1");
        Guid opponentAccountId = Guid.CreateVersion7();
        Guid matchId = Guid.CreateVersion7();
        ArenaTestEntrant entrant = CreateEntrant(accountId, character.Id, "AdmissionSolo");
        ArenaTestEntrant opponent = CreateEntrant(opponentAccountId, Guid.CreateVersion7(), "Opponent");

        IDisposable blocker = await guard.AcquireManyAsync(
            [accountId, opponentAccountId],
            CancellationToken.None);
        Task<CombatOperationResult> pveStart = combat.StartAsync(
            accountId,
            pending.EncounterId,
            CancellationToken.None);
        Task<bool> arenaStart = TryRegisterArenaAsync(
            guard,
            pve,
            arena,
            matchId,
            entrant,
            opponent,
            CancellationToken.None);
        blocker.Dispose();

        CombatOperationResult pveResult = await pveStart.WaitAsync(TimeSpan.FromSeconds(10));
        bool arenaResult = await arenaStart.WaitAsync(TimeSpan.FromSeconds(10));
        bool ownsPve = pve.HasActiveCombat(accountId);
        bool ownsArena = arena.IsInMatch(accountId);

        Assert.NotEqual(ownsPve, ownsArena);
        Assert.Equal(pveResult.Succeeded, ownsPve);
        Assert.Equal(arenaResult, ownsArena);
        Assert.False(ownsPve && ownsArena);

        if (arenaResult)
            arena.MarkFinalized(matchId);
    }

    [Fact]
    public async Task ArenaVsTrainingDummyLeavesExactlyOneRuntimeOwner()
    {
        Guid accountId = Guid.CreateVersion7();
        long telegramUserId = 9702;
        await SeedAccountAsync(accountId, telegramUserId);

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = CreateAuthenticatedClient(factory, accountId, telegramUserId);
        CharacterResponse character = await CreateCharacterAsync(client, "AdmissionDummy");

        using IServiceScope scope = factory.Services.CreateScope();
        CombatApplicationService combat = scope.ServiceProvider.GetRequiredService<CombatApplicationService>();
        CombatSessionRegistry pve = scope.ServiceProvider.GetRequiredService<CombatSessionRegistry>();
        CharacterOperationGuard guard = scope.ServiceProvider.GetRequiredService<CharacterOperationGuard>();
        ArenaMatchRuntime arena = scope.ServiceProvider.GetRequiredService<ArenaMatchRuntime>();

        Guid opponentAccountId = Guid.CreateVersion7();
        Guid matchId = Guid.CreateVersion7();
        ArenaTestEntrant entrant = CreateEntrant(accountId, character.Id, "AdmissionDummy");
        ArenaTestEntrant opponent = CreateEntrant(opponentAccountId, Guid.CreateVersion7(), "Opponent");

        IDisposable blocker = await guard.AcquireManyAsync(
            [accountId, opponentAccountId],
            CancellationToken.None);
        Task<CombatOperationResult> pveStart = combat.StartTrainingAsync(
            accountId,
            CancellationToken.None);
        Task<bool> arenaStart = TryRegisterArenaAsync(
            guard,
            pve,
            arena,
            matchId,
            entrant,
            opponent,
            CancellationToken.None);
        blocker.Dispose();

        CombatOperationResult pveResult = await pveStart.WaitAsync(TimeSpan.FromSeconds(10));
        bool arenaResult = await arenaStart.WaitAsync(TimeSpan.FromSeconds(10));
        bool ownsPve = pve.HasActiveCombat(accountId);
        bool ownsArena = arena.IsInMatch(accountId);

        Assert.NotEqual(ownsPve, ownsArena);
        Assert.Equal(pveResult.Succeeded, ownsPve);
        Assert.Equal(arenaResult, ownsArena);
        Assert.False(ownsPve && ownsArena);

        if (arenaResult)
            arena.MarkFinalized(matchId);
    }

    [Fact]
    public async Task ArenaVsPartyPveNeverLeavesMemberDualOwnedOrPartyPartiallyRegistered()
    {
        Guid leaderAccountId = Guid.CreateVersion7();
        Guid memberAccountId = Guid.CreateVersion7();
        await SeedAccountAsync(leaderAccountId, 9703);
        await SeedAccountAsync(memberAccountId, 9704);

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient leaderClient = CreateAuthenticatedClient(factory, leaderAccountId, 9703);
        using HttpClient memberClient = CreateAuthenticatedClient(factory, memberAccountId, 9704);
        CharacterResponse leader = await CreateCharacterAsync(leaderClient, "AdmissionLeader");
        CharacterResponse member = await CreateCharacterAsync(memberClient, "AdmissionMember");
        await SeedPartyAsync(leader.Id, member.Id, "WHISPERING_FOREST");

        using IServiceScope scope = factory.Services.CreateScope();
        CombatApplicationService combat = scope.ServiceProvider.GetRequiredService<CombatApplicationService>();
        CombatSessionRegistry pve = scope.ServiceProvider.GetRequiredService<CombatSessionRegistry>();
        WorldEncounterRegistry encounters = scope.ServiceProvider.GetRequiredService<WorldEncounterRegistry>();
        CharacterOperationGuard guard = scope.ServiceProvider.GetRequiredService<CharacterOperationGuard>();
        ArenaMatchRuntime arena = scope.ServiceProvider.GetRequiredService<ArenaMatchRuntime>();

        PendingWorldEncounter pending = encounters.Register(
            leaderAccountId,
            "WHISPERING_FOREST",
            "WHISPERING_FOREST_MOLODOI_VOLK_L1");
        Guid opponentAccountId = Guid.CreateVersion7();
        Guid matchId = Guid.CreateVersion7();
        ArenaTestEntrant entrant = CreateEntrant(memberAccountId, member.Id, "AdmissionMember");
        ArenaTestEntrant opponent = CreateEntrant(opponentAccountId, Guid.CreateVersion7(), "Opponent");

        IDisposable blocker = await guard.AcquireManyAsync(
            [leaderAccountId, memberAccountId, opponentAccountId],
            CancellationToken.None);
        Task<CombatOperationResult> pveStart = combat.StartAsync(
            leaderAccountId,
            pending.EncounterId,
            CancellationToken.None);
        Task<bool> arenaStart = TryRegisterArenaAsync(
            guard,
            pve,
            arena,
            matchId,
            entrant,
            opponent,
            CancellationToken.None);
        blocker.Dispose();

        CombatOperationResult pveResult = await pveStart.WaitAsync(TimeSpan.FromSeconds(10));
        bool arenaResult = await arenaStart.WaitAsync(TimeSpan.FromSeconds(10));
        bool leaderOwnsPve = pve.HasActiveCombat(leaderAccountId);
        bool memberOwnsPve = pve.HasActiveCombat(memberAccountId);
        bool memberOwnsArena = arena.IsInMatch(memberAccountId);

        Assert.False(memberOwnsPve && memberOwnsArena);
        Assert.Equal(leaderOwnsPve, memberOwnsPve);
        if (pveResult.Succeeded)
        {
            Assert.True(leaderOwnsPve);
            Assert.True(memberOwnsPve);
            Assert.False(arenaResult);
            Assert.False(memberOwnsArena);
            Assert.Equal(
                combat.Resume(leaderAccountId).Snapshot?.SessionId,
                combat.Resume(memberAccountId).Snapshot?.SessionId);
        }
        else
        {
            Assert.False(leaderOwnsPve);
            Assert.False(memberOwnsPve);
            Assert.True(arenaResult);
            Assert.True(memberOwnsArena);
        }

        if (arenaResult)
            arena.MarkFinalized(matchId);
    }

    private static async Task<bool> TryRegisterArenaAsync(
        CharacterOperationGuard guard,
        ICombatActivityReader pve,
        ArenaMatchRuntime arena,
        Guid matchId,
        ArenaTestEntrant first,
        ArenaTestEntrant second,
        CancellationToken cancellationToken)
    {
        Guid firstAccountId = first.Fighter.AccountId;
        Guid secondAccountId = second.Fighter.AccountId;
        using IDisposable lease = await guard.AcquireManyAsync(
            [firstAccountId, secondAccountId],
            cancellationToken);
        if (pve.HasActiveCombat(firstAccountId)
            || pve.HasActiveCombat(secondAccountId)
            || arena.IsInMatch(firstAccountId)
            || arena.IsInMatch(secondAccountId))
        {
            return false;
        }

        arena.Register(matchId, first, second);
        return true;
    }

    private static ArenaTestEntrant CreateEntrant(
        Guid accountId,
        Guid characterId,
        string name)
    {
        var actor = new CombatActorState(
            characterId,
            100,
            100,
            100,
            100,
            CombatStats.Default);
        var fighter = new ArenaFighter(
            accountId,
            characterId,
            actor,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            new AutoAttackProfile(TimeSpan.FromHours(1), 1, 0, 0),
            CanAutoAttack: false);
        return new ArenaTestEntrant(
            fighter,
            1,
            name,
            "WARRIOR",
            "MALE",
            null,
            "RAGE");
    }

    private async Task SeedAccountAsync(Guid accountId, long telegramUserId)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, telegramUserId, Now));
        await context.SaveChangesAsync();
    }

    private async Task RelocateAsync(Guid characterId, string locationId)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        CharacterLocation location = await context.CharacterLocations
            .SingleAsync(candidate => candidate.CharacterId == characterId);
        location.Relocate(locationId, Now);
        await context.SaveChangesAsync();
    }

    private async Task SeedPartyAsync(
        Guid leaderCharacterId,
        Guid memberCharacterId,
        string locationId)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        CharacterLocation leaderLocation = await context.CharacterLocations
            .SingleAsync(candidate => candidate.CharacterId == leaderCharacterId);
        CharacterLocation memberLocation = await context.CharacterLocations
            .SingleAsync(candidate => candidate.CharacterId == memberCharacterId);
        leaderLocation.Relocate(locationId, Now);
        memberLocation.Relocate(locationId, Now);

        Party party = Party.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            leaderCharacterId,
            Now);
        party.AddMember(memberCharacterId, Now.AddSeconds(1));
        context.Parties.Add(party);
        context.PartyMembers.AddRange(party.Members);
        await context.SaveChangesAsync();
    }

    private static async Task<CharacterResponse> CreateCharacterAsync(
        HttpClient client,
        string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/character",
            new CreateCharacterRequest(
                Guid.CreateVersion7(),
                name,
                "HUMAN",
                "MALE",
                "WARRIOR"));
        response.EnsureSuccessStatusCode();
        CharacterResponse? character = await response.Content.ReadFromJsonAsync<CharacterResponse>();
        Assert.NotNull(character);
        return character;
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
                    "arena-admission-test-signing-key-with-more-than-32-bytes");
                builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
                builder.UseSetting("Authentication:Development:Enabled", "false");
                builder.UseSetting("Arena:Enabled", "true");
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
        JwtTokenIssuer tokenIssuer = factory.Services.GetRequiredService<JwtTokenIssuer>();
        IssuedAccessToken token = tokenIssuer.Issue(accountId, telegramUserId);
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);
        return client;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);
            return FrozenTimer.Instance;
        }
    }

    private sealed class FrozenTimer : ITimer
    {
        public static readonly FrozenTimer Instance = new();

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
