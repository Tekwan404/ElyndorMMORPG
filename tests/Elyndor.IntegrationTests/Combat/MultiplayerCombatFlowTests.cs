using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Characters;
using Elyndor.Contracts.Combat;
using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Core.Social;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elyndor.IntegrationTests.Combat;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class MultiplayerCombatFlowTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 11, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PartyLeaderStartsOneCombatAndEachMemberCanRouteCommandsToOwnActor()
    {
        Guid leaderAccountId = Guid.CreateVersion7();
        Guid memberAccountId = Guid.CreateVersion7();

        await using (GameDbContext seedContext = postgres.CreateDbContext())
        {
            seedContext.Accounts.AddRange(
                new Account(leaderAccountId, 9101, Now),
                new Account(memberAccountId, 9102, Now));
            await seedContext.SaveChangesAsync();
        }

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient leaderClient = CreateAuthenticatedClient(
            factory,
            leaderAccountId,
            9101);
        using HttpClient memberClient = CreateAuthenticatedClient(
            factory,
            memberAccountId,
            9102);

        CharacterResponse leader = await CreateCharacterAsync(
            leaderClient,
            "LeaderAlpha");
        CharacterResponse member = await CreateCharacterAsync(
            memberClient,
            "MemberBeta");

        await SeedFriendsAndPartyAsync(leader.Id, member.Id);

        using IServiceScope scope = factory.Services.CreateScope();
        WorldEncounterRegistry encounterRegistry =
            scope.ServiceProvider.GetRequiredService<WorldEncounterRegistry>();
        CombatApplicationService combat =
            scope.ServiceProvider.GetRequiredService<CombatApplicationService>();

        PendingWorldEncounter pending = encounterRegistry.Register(
            leaderAccountId,
            "WHISPERING_FOREST",
            "FOREST_WOLF_L1");

        CombatOperationResult started = await combat.StartAsync(
            leaderAccountId,
            pending.EncounterId,
            CancellationToken.None);

        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.NotNull(started.Snapshot);
        Assert.Equal(leader.Id, started.Snapshot!.Player.ActorId);
        Assert.Equal(2, started.Snapshot.ParticipantRoster?.Count);
        Assert.All(
            started.Snapshot.ParticipantRoster!,
            participant => Assert.Equal(
                Elyndor.Core.Combat.Participants.CombatParticipantStatus.Active,
                participant.Status));

        CombatOperationResult memberView = combat.Resume(memberAccountId);

        Assert.True(memberView.Succeeded, memberView.ErrorCode);
        Assert.NotNull(memberView.Snapshot);
        Assert.Equal(started.Snapshot.SessionId, memberView.Snapshot!.SessionId);
        Assert.Equal(member.Id, memberView.Snapshot.Player.ActorId);
        Assert.NotEqual(
            started.Snapshot.Player.ActorId,
            memberView.Snapshot.Player.ActorId);

        CombatOperationResult memberCommand = await combat.StartAutoAttackAsync(
            memberAccountId,
            started.Snapshot.SessionId,
            "member-auto-attack-1",
            CancellationToken.None);

        Assert.True(memberCommand.Succeeded, memberCommand.ErrorCode);
        Assert.NotNull(memberCommand.Snapshot);
        Assert.Equal(member.Id, memberCommand.Snapshot!.Player.ActorId);
        Assert.True(memberCommand.Snapshot.Player.AutoAttackEnabled);

        await combat.LeaveAsync(
            memberAccountId,
            "member-cleanup-1",
            CancellationToken.None);
        await combat.LeaveAsync(
            leaderAccountId,
            "leader-cleanup-1",
            CancellationToken.None);
    }

    [Fact]
    public async Task SignalRHubRoutesParticipantCommandsAndBroadcastsScopedSnapshots()
    {
        Guid leaderAccountId = Guid.CreateVersion7();
        Guid memberAccountId = Guid.CreateVersion7();

        await using (GameDbContext seedContext = postgres.CreateDbContext())
        {
            seedContext.Accounts.AddRange(
                new Account(leaderAccountId, 9201, Now),
                new Account(memberAccountId, 9202, Now));
            await seedContext.SaveChangesAsync();
        }

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient leaderClient = CreateAuthenticatedClient(
            factory,
            leaderAccountId,
            9201);
        using HttpClient memberClient = CreateAuthenticatedClient(
            factory,
            memberAccountId,
            9202);
        CharacterResponse leader = await CreateCharacterAsync(leaderClient, "HubLeader");
        CharacterResponse member = await CreateCharacterAsync(memberClient, "HubMember");
        await SeedFriendsAndPartyAsync(leader.Id, member.Id);

        using IServiceScope scope = factory.Services.CreateScope();
        WorldEncounterRegistry encounterRegistry =
            scope.ServiceProvider.GetRequiredService<WorldEncounterRegistry>();
        PendingWorldEncounter pending = encounterRegistry.Register(
            leaderAccountId,
            "WHISPERING_FOREST",
            "FOREST_WOLF_L1");

        IssuedAccessToken leaderToken = IssueToken(factory, leaderAccountId, 9201);
        IssuedAccessToken memberToken = IssueToken(factory, memberAccountId, 9202);
        await using HubConnection leaderHub = CreateHubConnection(factory, leaderToken);
        await using HubConnection memberHub = CreateHubConnection(factory, memberToken);

        await leaderHub.StartAsync();
        await memberHub.StartAsync();

        CombatUpdateResponse started = await leaderHub.InvokeAsync<CombatUpdateResponse>(
            "StartCombat",
            pending.EncounterId.ToString());
        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.NotNull(started.Snapshot);
        Assert.Equal(leader.Id, started.Snapshot!.Player.ActorId);

        CombatUpdateResponse resumed = await memberHub.InvokeAsync<CombatUpdateResponse>(
            "ResumeCombat");
        Assert.True(resumed.Succeeded, resumed.ErrorCode);
        Assert.NotNull(resumed.Snapshot);
        Assert.Equal(started.Snapshot.SessionId, resumed.Snapshot!.SessionId);
        Assert.Equal(member.Id, resumed.Snapshot.Player.ActorId);

        TaskCompletionSource<CombatUpdateResponse> memberBroadcast =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        memberHub.On<CombatUpdateResponse>(
            "CombatUpdated",
            update =>
            {
                if (update.Snapshot?.SessionId == started.Snapshot.SessionId)
                    memberBroadcast.TrySetResult(update);
            });

        CombatUpdateResponse memberCommand = await memberHub.InvokeAsync<CombatUpdateResponse>(
            "StartAutoAttack",
            started.Snapshot.SessionId,
            "hub-member-auto-attack-1");
        Assert.True(memberCommand.Succeeded, memberCommand.ErrorCode);
        Assert.Equal(member.Id, memberCommand.Snapshot?.Player.ActorId);

        CombatUpdateResponse broadcast = await memberBroadcast.Task.WaitAsync(
            TimeSpan.FromSeconds(5));
        Assert.True(broadcast.Succeeded, broadcast.ErrorCode);
        Assert.Equal(member.Id, broadcast.Snapshot?.Player.ActorId);
        Assert.True(broadcast.Snapshot?.Player.AutoAttackEnabled);

        await memberHub.InvokeAsync<CombatUpdateResponse>(
            "LeaveCombat",
            "hub-member-cleanup-1");
        await leaderHub.InvokeAsync<CombatUpdateResponse>(
            "LeaveCombat",
            "hub-leader-cleanup-1");
    }

    [Fact]
    public async Task DungeonHubStartsOneSessionAndFleeKeepsCombatForRemainingParticipant()
    {
        Guid leaderAccountId = Guid.CreateVersion7();
        Guid memberAccountId = Guid.CreateVersion7();

        await using (GameDbContext seedContext = postgres.CreateDbContext())
        {
            seedContext.Accounts.AddRange(
                new Account(leaderAccountId, 9301, Now),
                new Account(memberAccountId, 9302, Now));
            await seedContext.SaveChangesAsync();
        }

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient leaderClient = CreateAuthenticatedClient(factory, leaderAccountId, 9301);
        using HttpClient memberClient = CreateAuthenticatedClient(factory, memberAccountId, 9302);
        CharacterResponse leader = await CreateCharacterAsync(leaderClient, "DungeonLeader");
        CharacterResponse member = await CreateCharacterAsync(memberClient, "DungeonMember");
        await SeedFriendsAndPartyAsync(
            leader.Id,
            member.Id,
            "ANCIENT_MINE",
            requireDungeonLevel: true);

        Guid runId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            DungeonService dungeon = scope.ServiceProvider.GetRequiredService<DungeonService>();
            var created = await dungeon.CreateAsync(
                leaderAccountId,
                "ANCIENT_MINE",
                Guid.CreateVersion7(),
                CancellationToken.None);
            Assert.True(created.Succeeded, created.ErrorCode);
            runId = created.Run!.RunId;
        }

        IssuedAccessToken leaderToken = IssueToken(factory, leaderAccountId, 9301);
        IssuedAccessToken memberToken = IssueToken(factory, memberAccountId, 9302);
        await using HubConnection leaderHub = CreateHubConnection(factory, leaderToken);
        await using HubConnection memberHub = CreateHubConnection(factory, memberToken);
        await leaderHub.StartAsync();
        await memberHub.StartAsync();

        CombatUpdateResponse started = await leaderHub.InvokeAsync<CombatUpdateResponse>(
            "StartDungeonEncounter",
            runId);
        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.Equal(leader.Id, started.Snapshot?.Player.ActorId);
        Guid sessionId = started.Snapshot!.SessionId;

        CombatUpdateResponse memberView = await memberHub.InvokeAsync<CombatUpdateResponse>(
            "ResumeCombat");
        Assert.True(memberView.Succeeded, memberView.ErrorCode);
        Assert.Equal(sessionId, memberView.Snapshot?.SessionId);
        Assert.Equal(member.Id, memberView.Snapshot?.Player.ActorId);

        CombatUpdateResponse memberFlee = await memberHub.InvokeAsync<CombatUpdateResponse>(
            "FleeCombat",
            sessionId,
            "dungeon-member-flee-1");
        Assert.True(memberFlee.Succeeded, memberFlee.ErrorCode);
        Assert.Equal("Active", memberFlee.Snapshot?.Status.ToString());
        Assert.Equal(
            "Fled",
            memberFlee.Snapshot?.ParticipantRoster?.Single(
                participant => participant.CharacterId == member.Id).Status.ToString());

        CombatUpdateResponse leaderFlee = await leaderHub.InvokeAsync<CombatUpdateResponse>(
            "FleeCombat",
            sessionId,
            "dungeon-leader-flee-1");
        Assert.True(leaderFlee.Succeeded, leaderFlee.ErrorCode);
        Assert.Equal("Defeat", leaderFlee.Snapshot?.Status.ToString());
    }

    [Fact]
    public async Task RemotePartyMemberIsExcludedFromOpenWorldEncounterRoster()
    {
        Guid leaderAccountId = Guid.CreateVersion7();
        Guid memberAccountId = Guid.CreateVersion7();

        await using (GameDbContext seedContext = postgres.CreateDbContext())
        {
            seedContext.Accounts.AddRange(
                new Account(leaderAccountId, 9401, Now),
                new Account(memberAccountId, 9402, Now));
            await seedContext.SaveChangesAsync();
        }

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient leaderClient = CreateAuthenticatedClient(factory, leaderAccountId, 9401);
        using HttpClient memberClient = CreateAuthenticatedClient(factory, memberAccountId, 9402);
        CharacterResponse leader = await CreateCharacterAsync(leaderClient, "LateLeader");
        CharacterResponse member = await CreateCharacterAsync(memberClient, "LateMember");
        await SeedFriendsAndPartyAsync(leader.Id, member.Id);

        await using (GameDbContext moveContext = postgres.CreateDbContext())
        {
            CharacterLocation memberLocation = await moveContext.CharacterLocations
                .SingleAsync(location => location.CharacterId == member.Id);
            memberLocation.Relocate("STARTER_TOWN", Now);
            await moveContext.SaveChangesAsync();
        }

        IssuedAccessToken leaderToken = IssueToken(factory, leaderAccountId, 9401);
        await using HubConnection leaderHub = CreateHubConnection(factory, leaderToken);
        await leaderHub.StartAsync();

        using IServiceScope scope = factory.Services.CreateScope();
        WorldEncounterRegistry encounterRegistry =
            scope.ServiceProvider.GetRequiredService<WorldEncounterRegistry>();
        PendingWorldEncounter pending = encounterRegistry.Register(
            leaderAccountId,
            "WHISPERING_FOREST",
            "FOREST_WOLF_L1");
        CombatUpdateResponse started = await leaderHub.InvokeAsync<CombatUpdateResponse>(
            "StartCombat",
            pending.EncounterId.ToString());

        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.NotNull(started.Snapshot?.ParticipantRoster);
        Assert.DoesNotContain(
            started.Snapshot!.ParticipantRoster!,
            participant => participant.CharacterId == member.Id);

        await leaderHub.InvokeAsync<CombatUpdateResponse>(
            "LeaveCombat",
            "remote-member-exclusion-cleanup-1");
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
        CharacterResponse? character =
            await response.Content.ReadFromJsonAsync<CharacterResponse>();
        Assert.NotNull(character);
        return character;
    }

    private async Task SeedFriendsAndPartyAsync(
        Guid leaderCharacterId,
        Guid memberCharacterId,
        string locationId = "WHISPERING_FOREST",
        bool requireDungeonLevel = false)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        CharacterLocation leaderLocation = await context.CharacterLocations
            .SingleAsync(location => location.CharacterId == leaderCharacterId);
        CharacterLocation memberLocation = await context.CharacterLocations
            .SingleAsync(location => location.CharacterId == memberCharacterId);
        Character leaderCharacter = await context.Characters
            .SingleAsync(character => character.Id == leaderCharacterId);
        Character memberCharacter = await context.Characters
            .SingleAsync(character => character.Id == memberCharacterId);
        if (requireDungeonLevel)
        {
            leaderCharacter.SetLevel(15);
            memberCharacter.SetLevel(15);
        }
        leaderLocation.Relocate(locationId, Now);
        memberLocation.Relocate(locationId, Now);
        Friendship friendship = Friendship.Create(
            leaderCharacterId,
            memberCharacterId,
            Now);
        Party party = Party.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            leaderCharacterId,
            Now);
        party.AddMember(memberCharacterId, Now.AddSeconds(1));

        context.Friendships.Add(friendship);
        context.Parties.Add(party);
        context.PartyMembers.AddRange(party.Members);
        await context.SaveChangesAsync();
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
                    "multiplayer-combat-test-signing-key-with-more-than-32-bytes");
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

    private static IssuedAccessToken IssueToken(
        WebApplicationFactory<Program> factory,
        Guid accountId,
        long telegramUserId) =>
        factory.Services.GetRequiredService<JwtTokenIssuer>().Issue(
            accountId,
            telegramUserId);

    private static HubConnection CreateHubConnection(
        WebApplicationFactory<Program> factory,
        IssuedAccessToken token) =>
        new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/hubs/combat",
                options =>
                {
                    options.AccessTokenProvider = () =>
                        Task.FromResult<string?>(token.AccessToken);
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}