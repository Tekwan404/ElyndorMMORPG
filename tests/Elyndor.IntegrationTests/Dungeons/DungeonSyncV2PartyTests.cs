using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Dungeons;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class DungeonSyncV2PartyTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 13, 16, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OneMemberCanStayInCityWhilePartyContinuesSameRun()
    {
        Guid leaderAccountId = Guid.Parse("83000000-0000-0000-0000-000000000001");
        Guid memberAccountId = Guid.Parse("83000000-0000-0000-0000-000000000002");
        Guid leaderCharacterId = Guid.Parse("83111111-1111-1111-1111-111111111111");
        Guid memberCharacterId = Guid.Parse("83222222-2222-2222-2222-222222222222");

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.AddRange(
            new Account(leaderAccountId, 8301, Now),
            new Account(memberAccountId, 8302, Now));

        Character leader = CreateCharacter(leaderCharacterId, leaderAccountId, "SyncLeader");
        Character member = CreateCharacter(memberCharacterId, memberAccountId, "SyncMember");
        leader.SetLevel(25);
        member.SetLevel(25);
        context.Characters.AddRange(leader, member);
        context.CharacterVitals.AddRange(CreateVitals(leaderCharacterId), CreateVitals(memberCharacterId));
        context.CharacterLocations.AddRange(
            new CharacterLocation(leaderCharacterId, WorldLocationIds.StarterTown, 1, Now),
            new CharacterLocation(memberCharacterId, WorldLocationIds.StarterTown, 1, Now));
        await context.SaveChangesAsync();

        FixedTimeProvider time = new(Now);
        PartyService partyService = new(context, time);
        Assert.True((await partyService.CreateAsync(
            leaderAccountId,
            Guid.NewGuid(),
            CancellationToken.None)).IsSuccess);

        Guid inviteId = Guid.NewGuid();
        Assert.True((await partyService.InviteAsync(
            leaderAccountId,
            inviteId,
            memberCharacterId,
            PartyInviteMode.Direct,
            CancellationToken.None)).IsSuccess);
        Assert.True((await partyService.AcceptInviteAsync(
            memberAccountId,
            inviteId,
            CancellationToken.None)).IsSuccess);

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        StaticContentSnapshotProvider contentProvider = new(content);
        PartyDungeonRunCoordinator coordinator = new(
            context,
            partyService,
            contentProvider,
            new NoActiveCombatReader(),
            time);

        PartyDungeonStartResult started = await coordinator.StartAsync(
            leaderAccountId,
            "ECLIPSED_CITADEL",
            Guid.NewGuid(),
            CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.NotNull(started.RunId);
        Guid runId = started.RunId!.Value;

        DungeonNavigationService navigation = new(context, new FixedTimeProvider(Now.AddMinutes(1)));
        DungeonNavigationResult cityExit = await navigation.ExitToCityAsync(
            memberAccountId,
            runId,
            CancellationToken.None);
        Assert.True(cityExit.Succeeded, cityExit.ErrorCode);
        Assert.Equal(WorldLocationIds.StarterTown, cityExit.LocationId);

        DungeonService dungeonService = new(context, partyService, contentProvider, time);
        (DungeonPreparation? preparation, string? errorCode) = await dungeonService.PrepareEncounterAsync(
            leaderAccountId,
            runId,
            CancellationToken.None);

        Assert.Null(errorCode);
        Assert.NotNull(preparation);
        Assert.Equal(runId, preparation!.RunId);
        Assert.Contains(preparation.Participants, participant => participant.CharacterId == leaderCharacterId);
        Assert.DoesNotContain(preparation.Participants, participant => participant.CharacterId == memberCharacterId);

        context.ChangeTracker.Clear();
        DungeonRun persisted = await context.DungeonRuns
            .Include(run => run.Members)
            .SingleAsync(run => run.Id == runId);
        Assert.Equal(DungeonRunState.Active, persisted.State);
        Assert.Equal(DungeonRunMemberState.Active, persisted.Members.Single(
            runMember => runMember.CharacterId == memberCharacterId).State);
        Assert.True(await context.PartyMembers.AnyAsync(partyMember =>
            partyMember.PartyId == persisted.PartyId
            && partyMember.CharacterId == memberCharacterId));
    }

    private static Character CreateCharacter(Guid id, Guid accountId, string name) => new(
        id,
        accountId,
        Guid.NewGuid(),
        name,
        name.ToUpperInvariant(),
        "HUMAN",
        "MALE",
        "WARRIOR",
        Now);

    private static CharacterVitals CreateVitals(Guid characterId) => new(
        characterId,
        500,
        0,
        Now,
        Now);

    private sealed class NoActiveCombatReader : ICombatActivityReader
    {
        public bool HasActiveCombat(Guid accountId) => false;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
