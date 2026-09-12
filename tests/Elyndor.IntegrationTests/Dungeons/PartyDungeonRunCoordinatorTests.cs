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
public sealed class PartyDungeonRunCoordinatorTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 11, 22, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task NewLeaderRunReplacesEveryMembersPreviousRunAndIsSharedByTheParty()
    {
        Guid leaderAccountId = Guid.Parse("71000000-0000-0000-0000-000000000001");
        Guid memberAccountId = Guid.Parse("71000000-0000-0000-0000-000000000002");
        Guid leaderCharacterId = Guid.Parse("71111111-1111-1111-1111-111111111111");
        Guid memberCharacterId = Guid.Parse("72222222-2222-2222-2222-222222222222");
        Guid oldMemberRunId = Guid.Parse("73333333-3333-3333-3333-333333333333");

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.AddRange(
            new Account(leaderAccountId, 7101, Now),
            new Account(memberAccountId, 7102, Now));

        Character leader = CreateCharacter(leaderCharacterId, leaderAccountId, "RunLeader");
        Character member = CreateCharacter(memberCharacterId, memberAccountId, "RunMember");
        leader.SetLevel(25);
        member.SetLevel(25);
        context.Characters.AddRange(leader, member);
        context.CharacterVitals.AddRange(CreateVitals(leaderCharacterId), CreateVitals(memberCharacterId));
        context.CharacterLocations.AddRange(
            new CharacterLocation(leaderCharacterId, WorldLocationIds.StarterTown, 1, Now),
            new CharacterLocation(memberCharacterId, "WHISPERING_FOREST", 1, Now));

        DungeonRun oldMemberRun = DungeonRun.Create(
            oldMemberRunId,
            Guid.NewGuid(),
            memberCharacterId,
            "ANCIENT_MINE",
            Now.AddMinutes(-10));
        oldMemberRun.AddMember(memberCharacterId, Now.AddMinutes(-10));
        oldMemberRun.Encounters.Add(DungeonEncounter.Create(
            Guid.NewGuid(),
            oldMemberRunId,
            0,
            "ANCIENT_MINE_RAT_L15",
            Now.AddMinutes(-10)));
        context.DungeonRuns.Add(oldMemberRun);
        await context.SaveChangesAsync();

        FixedTimeProvider time = new(Now);
        PartyService partyService = new(context, time);
        PartyOperationResult createdParty = await partyService.CreateAsync(
            leaderAccountId,
            Guid.NewGuid(),
            CancellationToken.None);
        Assert.True(createdParty.IsSuccess, createdParty.ErrorCode);

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

        PartySnapshot party = (await partyService.GetAsync(
            leaderAccountId,
            CancellationToken.None))!;

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        PartyDungeonRunCoordinator coordinator = new(
            context,
            partyService,
            new StaticContentSnapshotProvider(content),
            new NoActiveCombatReader(),
            time);

        Guid firstRequestId = Guid.NewGuid();
        PartyDungeonStartResult first = await coordinator.StartAsync(
            leaderAccountId,
            "ECLIPSED_CITADEL",
            firstRequestId,
            CancellationToken.None);

        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.NotNull(first.RunId);

        context.ChangeTracker.Clear();
        DungeonRun persistedOldMemberRun = await context.DungeonRuns
            .Include(run => run.Members)
            .SingleAsync(run => run.Id == oldMemberRunId);
        Assert.Equal(DungeonRunState.Abandoned, persistedOldMemberRun.State);
        Assert.Equal(
            DungeonRunMemberState.Left,
            persistedOldMemberRun.Members.Single(memberState => memberState.CharacterId == memberCharacterId).State);

        DungeonRun firstRun = await context.DungeonRuns
            .Include(run => run.Members)
            .SingleAsync(run => run.Id == first.RunId);
        Assert.Equal(party.PartyId, firstRun.PartyId);
        Assert.Equal(DungeonRunState.Active, firstRun.State);
        Assert.Equal(2, firstRun.Members.Count);
        Assert.All(firstRun.Members, runMember => Assert.Equal(DungeonRunMemberState.Active, runMember.State));

        string[] entryLocations = await context.CharacterLocations
            .Where(location => location.CharacterId == leaderCharacterId || location.CharacterId == memberCharacterId)
            .Select(location => location.LocationId)
            .ToArrayAsync();
        Assert.Equal(2, entryLocations.Length);
        Assert.All(entryLocations, locationId => Assert.Equal("ECLIPSED_CITADEL", locationId));

        PartyDungeonStartResult replay = await coordinator.StartAsync(
            leaderAccountId,
            "ECLIPSED_CITADEL",
            firstRequestId,
            CancellationToken.None);
        Assert.True(replay.Succeeded, replay.ErrorCode);
        Assert.Equal(first.RunId, replay.RunId);

        PartyDungeonStartResult replacement = await coordinator.StartAsync(
            leaderAccountId,
            "ECLIPSED_CITADEL",
            Guid.NewGuid(),
            CancellationToken.None);
        Assert.True(replacement.Succeeded, replacement.ErrorCode);
        Assert.NotEqual(first.RunId, replacement.RunId);

        context.ChangeTracker.Clear();
        DungeonRun superseded = await context.DungeonRuns
            .Include(run => run.Members)
            .SingleAsync(run => run.Id == first.RunId);
        Assert.Equal(DungeonRunState.Abandoned, superseded.State);
        Assert.All(superseded.Members, runMember => Assert.Equal(DungeonRunMemberState.Left, runMember.State));

        DungeonRun latest = await context.DungeonRuns
            .Include(run => run.Members)
            .SingleAsync(run => run.Id == replacement.RunId);
        Assert.Equal(DungeonRunState.Active, latest.State);
        Assert.Equal(party.PartyId, latest.PartyId);
        Assert.Equal(2, latest.Members.Count);
        Assert.All(latest.Members, runMember => Assert.Equal(DungeonRunMemberState.Active, runMember.State));
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
