using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Dungeons;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class DungeonServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 8, 15, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OverleveledCharacterCanTeleportAndCreateAncientMineRun()
    {
        (Guid leaderAccountId, _, _) = await SeedPartyCharactersAsync();

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        StaticContentSnapshotProvider contentProvider = new(content);

        await using GameDbContext context = postgres.CreateDbContext();
        Character leader = await context.Characters
            .SingleAsync(character => character.AccountId == leaderAccountId);
        leader.SetLevel(25);
        CharacterLocation location = await context.CharacterLocations
            .SingleAsync(candidate => candidate.CharacterId == leader.Id);
        location.Relocate("STARTER_TOWN", Now);
        await context.SaveChangesAsync();

        FixedTimeProvider time = new(Now);
        PartyService partyService = new(context, time);
        DungeonService dungeonService = new(context, partyService, contentProvider, time);

        DungeonTeleportResult teleported = await dungeonService.TeleportToEntryAsync(
            leaderAccountId,
            "ANCIENT_MINE",
            Guid.NewGuid(),
            CancellationToken.None);
        Assert.True(teleported.Succeeded, teleported.ErrorCode);
        Assert.Equal("ANCIENT_MINE", teleported.LocationId);

        Assert.True((await partyService.CreateAsync(
            leaderAccountId,
            Guid.NewGuid(),
            CancellationToken.None)).IsSuccess);

        DungeonOperationResult created = await dungeonService.CreateAsync(
            leaderAccountId,
            "ANCIENT_MINE",
            Guid.NewGuid(),
            CancellationToken.None);
        Assert.True(created.Succeeded, created.ErrorCode);
        Assert.NotNull(created.Run);
    }

    [Fact]
    public async Task CreatingDifferentDungeonAbandonsInactiveRunAndCreatesRequestedDungeon()
    {
        (Guid leaderAccountId, _, _) = await SeedPartyCharactersAsync();

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        StaticContentSnapshotProvider contentProvider = new(content);

        await using GameDbContext context = postgres.CreateDbContext();
        Character leader = await context.Characters
            .SingleAsync(character => character.AccountId == leaderAccountId);
        leader.SetLevel(25);
        await context.SaveChangesAsync();

        FixedTimeProvider time = new(Now);
        PartyService partyService = new(context, time);
        DungeonService dungeonService = new(context, partyService, contentProvider, time);

        Assert.True((await partyService.CreateAsync(
            leaderAccountId,
            Guid.NewGuid(),
            CancellationToken.None)).IsSuccess);

        DungeonOperationResult mine = await dungeonService.CreateAsync(
            leaderAccountId,
            "ANCIENT_MINE",
            Guid.NewGuid(),
            CancellationToken.None);
        Assert.True(mine.Succeeded, mine.ErrorCode);
        Assert.NotNull(mine.Run);

        CharacterLocation location = await context.CharacterLocations
            .SingleAsync(candidate => candidate.CharacterId == leader.Id);
        location.Relocate("ECLIPSED_CITADEL", Now);
        await context.SaveChangesAsync();

        DungeonOperationResult citadel = await dungeonService.CreateAsync(
            leaderAccountId,
            "ECLIPSED_CITADEL",
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(citadel.Succeeded, citadel.ErrorCode);
        Assert.NotNull(citadel.Run);
        Assert.Equal("ECLIPSED_CITADEL", citadel.Run!.DungeonId);
        Assert.NotEqual(mine.Run!.RunId, citadel.Run.RunId);

        context.ChangeTracker.Clear();
        DungeonRun persistedMine = await context.DungeonRuns
            .Include(run => run.Members)
            .SingleAsync(run => run.Id == mine.Run.RunId);
        Assert.Equal(DungeonRunState.Abandoned, persistedMine.State);
        Assert.All(
            persistedMine.Members,
            member => Assert.Equal(DungeonRunMemberState.Left, member.State));
    }

    [Fact]
    public async Task DisbandAbandonsActiveRunWithoutChangingEncounterRoster()
    {
        (Guid leader, _, _) = await SeedPartyCharactersAsync();
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        await using GameDbContext context = postgres.CreateDbContext();
        FixedTimeProvider time = new(Now);
        PartyService parties = new(context, time);
        DungeonService dungeons = new(context, parties, new StaticContentSnapshotProvider(content), time);
        Assert.True((await parties.CreateAsync(leader, Guid.NewGuid(), CancellationToken.None)).IsSuccess);
        DungeonOperationResult created = await dungeons.CreateAsync(leader, "ANCIENT_MINE", Guid.NewGuid(), CancellationToken.None);
        Assert.True(created.Succeeded);
        var (prepared, error) = await dungeons.PrepareEncounterAsync(leader, created.Run!.RunId, CancellationToken.None);
        Assert.Null(error);
        Assert.NotNull(prepared);
        Assert.True(await dungeons.BindCombatAsync(prepared.RunId, prepared.EncounterId, Guid.NewGuid(), CancellationToken.None));
        Assert.True((await parties.DisbandAsync(leader, CancellationToken.None)).IsSuccess);
        context.ChangeTracker.Clear();
        DungeonRun run = await context.DungeonRuns.Include(item => item.Encounters).ThenInclude(item => item.Members).SingleAsync();
        Assert.Equal(DungeonRunState.Abandoned, run.State);
        Assert.Single(run.Encounters.Single().Members);
    }

    [Fact]
    public async Task MemberAddedDuringActiveEncounterJoinsOnlyTheNextEncounter()
    {
        (Guid leaderAccountId, Guid firstMemberAccountId, Guid lateMemberAccountId) =
            await SeedPartyCharactersAsync();

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        StaticContentSnapshotProvider contentProvider = new(content);

        await using GameDbContext context = postgres.CreateDbContext();
        FixedTimeProvider time = new(Now);
        PartyService partyService = new(context, time);
        DungeonService dungeonService = new(context, partyService, contentProvider, time);

        Assert.True((await partyService.CreateAsync(
            leaderAccountId,
            Guid.NewGuid(),
            CancellationToken.None)).IsSuccess);
        Character lateMember = await context.Characters
            .SingleAsync(character => character.AccountId == lateMemberAccountId);
        Assert.True((await partyService.InviteAsync(
            leaderAccountId,
            Guid.NewGuid(),
            (await context.Characters.SingleAsync(
                character => character.AccountId == firstMemberAccountId)).Id,
            PartyInviteMode.Direct,
            CancellationToken.None)).IsSuccess);
        PartyInvite firstInvite = await context.PartyInvites.SingleAsync();
        Assert.True((await partyService.AcceptInviteAsync(
            firstMemberAccountId,
            firstInvite.Id,
            CancellationToken.None)).IsSuccess);

        DungeonOperationResult created = await dungeonService.CreateAsync(
            leaderAccountId,
            "ANCIENT_MINE",
            Guid.NewGuid(),
            CancellationToken.None);
        Assert.True(created.Succeeded);
        DungeonRunView run = created.Run!;
        Assert.Equal(2, run.Members.Count);

        (DungeonPreparation? preparation, string? preparationError) =
            await dungeonService.PrepareEncounterAsync(
                leaderAccountId,
                run.RunId,
                CancellationToken.None);
        Assert.Null(preparationError);
        Assert.NotNull(preparation);
        Assert.Equal(2, preparation!.Participants.Count);
        Assert.True(await dungeonService.BindCombatAsync(
            preparation.RunId,
            preparation.EncounterId,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CancellationToken.None));

        Guid lateCharacterId = lateMember.Id;
        Assert.True((await partyService.InviteAsync(
            leaderAccountId,
            Guid.NewGuid(),
            lateCharacterId,
            PartyInviteMode.Direct,
            CancellationToken.None)).IsSuccess);
        PartyInvite lateInvite = await context.PartyInvites
            .Where(invite => invite.TargetCharacterId == lateCharacterId
                && invite.Status == PartyInviteStatus.Pending)
            .FirstAsync();
        PartyOperationResult lateAccepted = await partyService.AcceptInviteAsync(
            lateMemberAccountId,
            lateInvite.Id,
            CancellationToken.None);
        Assert.True(lateAccepted.IsSuccess, lateAccepted.ErrorCode);

        DungeonOperationResult entered = await dungeonService.EnterAsync(
            lateMemberAccountId,
            run.RunId,
            CancellationToken.None);
        Assert.True(entered.Succeeded);
        DungeonEncounterView activeEncounter = entered.Run!.Encounters
            .Single(encounter => encounter.EncounterIndex == 0);
        Assert.DoesNotContain(lateCharacterId, activeEncounter.CharacterIds);
        Assert.Contains(lateCharacterId, entered.Run.Members.Select(member => member.CharacterId));

        await dungeonService.HandleCombatFinishedAsync(
            VictorySnapshot(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            CancellationToken.None);

        (DungeonPreparation? nextPreparation, string? nextError) =
            await dungeonService.PrepareEncounterAsync(
                leaderAccountId,
                run.RunId,
                CancellationToken.None);
        Assert.Null(nextError);
        Assert.NotNull(nextPreparation);
        Assert.Equal(3, nextPreparation!.Participants.Count);
        Assert.Contains(
            lateCharacterId,
            nextPreparation.Participants.Select(participant => participant.CharacterId));
    }

    [Fact]
    public async Task AncientMineCompletesEveryEncounterAndDuplicateVictoryIsIdempotent()
    {
        (Guid leaderAccountId, Guid firstMemberAccountId, _) =
            await SeedPartyCharactersAsync();

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        StaticContentSnapshotProvider contentProvider = new(content);

        await using GameDbContext context = postgres.CreateDbContext();
        FixedTimeProvider time = new(Now);
        PartyService partyService = new(context, time);
        DungeonService dungeonService = new(context, partyService, contentProvider, time);

        Assert.True((await partyService.CreateAsync(
            leaderAccountId,
            Guid.NewGuid(),
            CancellationToken.None)).IsSuccess);
        Character firstMember = await context.Characters
            .SingleAsync(character => character.AccountId == firstMemberAccountId);
        Assert.True((await partyService.InviteAsync(
            leaderAccountId,
            Guid.NewGuid(),
            firstMember.Id,
            PartyInviteMode.Direct,
            CancellationToken.None)).IsSuccess);
        PartyInvite invite = await context.PartyInvites.SingleAsync();
        Assert.True((await partyService.AcceptInviteAsync(
            firstMemberAccountId,
            invite.Id,
            CancellationToken.None)).IsSuccess);

        DungeonOperationResult created = await dungeonService.CreateAsync(
            leaderAccountId,
            "ANCIENT_MINE",
            Guid.NewGuid(),
            CancellationToken.None);
        Assert.True(created.Succeeded, created.ErrorCode);
        DungeonRunView run = created.Run!;
        Assert.Equal(5, run.EncounterCount);

        for (int encounterIndex = 0; encounterIndex < run.EncounterCount; encounterIndex++)
        {
            DungeonRunView activeBeforePreparation = (await dungeonService.GetCurrentAsync(
                leaderAccountId,
                CancellationToken.None))!;
            Assert.Equal(encounterIndex, activeBeforePreparation.CurrentEncounterIndex);

            (DungeonPreparation? preparation, string? preparationError) =
                await dungeonService.PrepareEncounterAsync(
                    leaderAccountId,
                    run.RunId,
                    CancellationToken.None);
            Assert.Null(preparationError);
            Assert.NotNull(preparation);
            Assert.Equal(2, preparation!.Participants.Count);

            Guid sessionId = Guid.NewGuid();
            Assert.True(await dungeonService.BindCombatAsync(
                preparation.RunId,
                preparation.EncounterId,
                sessionId,
                CancellationToken.None));

            CombatSessionSnapshot victory = VictorySnapshot(sessionId);
            await dungeonService.HandleCombatFinishedAsync(victory, CancellationToken.None);
            await dungeonService.HandleCombatFinishedAsync(victory, CancellationToken.None);

            if (encounterIndex < run.EncounterCount - 1)
            {
                DungeonRunView activeRun = (await dungeonService.GetCurrentAsync(
                    leaderAccountId,
                    CancellationToken.None))!;
                Assert.Equal(DungeonRunState.Active, activeRun.State);
                Assert.Equal(encounterIndex + 1, activeRun.CurrentEncounterIndex);
                Assert.Single(
                    activeRun.Encounters,
                    encounter => encounter.EncounterIndex == encounterIndex
                        && encounter.State == DungeonEncounterState.Completed);
            }
        }

        DungeonRun persistedRun = await context.DungeonRuns
            .Include(candidate => candidate.Encounters)
            .SingleAsync(candidate => candidate.Id == run.RunId);
        Assert.Equal(DungeonRunState.Completed, persistedRun.State);
        Assert.Equal(run.EncounterCount, persistedRun.CurrentEncounterIndex);
        Assert.Equal(run.EncounterCount, persistedRun.Encounters.Count);
        Assert.All(
            persistedRun.Encounters,
            encounter => Assert.Equal(DungeonEncounterState.Completed, encounter.State));

        context.ChangeTracker.Clear();
        DungeonRunView? completedAfterRefresh = await dungeonService.GetCurrentAsync(
            leaderAccountId,
            CancellationToken.None);
        Assert.NotNull(completedAfterRefresh);
        Assert.Equal(DungeonRunState.Completed, completedAfterRefresh!.State);
        Assert.Equal(run.EncounterCount, completedAfterRefresh.CurrentEncounterIndex);
    }

    [Fact]
    public async Task LeaderCanRestartWipedEncounterAndMemberCanExitAndReenterRun()
    {
        (Guid leaderAccountId, Guid firstMemberAccountId, _) =
            await SeedPartyCharactersAsync();

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        StaticContentSnapshotProvider contentProvider = new(content);

        await using GameDbContext context = postgres.CreateDbContext();
        FixedTimeProvider time = new(Now);
        PartyService partyService = new(context, time);
        DungeonService dungeonService = new(context, partyService, contentProvider, time);

        Assert.True((await partyService.CreateAsync(
            leaderAccountId,
            Guid.NewGuid(),
            CancellationToken.None)).IsSuccess);
        Character firstMember = await context.Characters
            .SingleAsync(character => character.AccountId == firstMemberAccountId);
        Assert.True((await partyService.InviteAsync(
            leaderAccountId,
            Guid.NewGuid(),
            firstMember.Id,
            PartyInviteMode.Direct,
            CancellationToken.None)).IsSuccess);
        PartyInvite invite = await context.PartyInvites.SingleAsync();
        Assert.True((await partyService.AcceptInviteAsync(
            firstMemberAccountId,
            invite.Id,
            CancellationToken.None)).IsSuccess);

        DungeonOperationResult created = await dungeonService.CreateAsync(
            leaderAccountId,
            "ANCIENT_MINE",
            Guid.NewGuid(),
            CancellationToken.None);
        Assert.True(created.Succeeded, created.ErrorCode);

        (DungeonPreparation? preparation, string? preparationError) =
            await dungeonService.PrepareEncounterAsync(
                leaderAccountId,
                created.Run!.RunId,
                CancellationToken.None);
        Assert.Null(preparationError);
        Assert.NotNull(preparation);
        Guid sessionId = Guid.NewGuid();
        Assert.True(await dungeonService.BindCombatAsync(
            preparation!.RunId,
            preparation.EncounterId,
            sessionId,
            CancellationToken.None));

        await dungeonService.HandleCombatFinishedAsync(
            DefeatSnapshot(sessionId),
            CancellationToken.None);

        DungeonOperationResult restarted = await dungeonService.RestartEncounterAsync(
            leaderAccountId,
            created.Run!.RunId,
            CancellationToken.None);
        Assert.True(restarted.Succeeded, restarted.ErrorCode);
        Assert.Equal("MINE_ENTRANCE", restarted.Run!.CurrentCheckpointId);
        Assert.Equal(DungeonEncounterState.Pending, restarted.Run.Encounters
            .Single(encounter => encounter.EncounterIndex == 0)
            .State);
        Assert.Empty(restarted.Run.Encounters
            .Single(encounter => encounter.EncounterIndex == 0)
            .CharacterIds);

        DungeonOperationResult exited = await dungeonService.ExitAsync(
            firstMemberAccountId,
            created.Run!.RunId,
            CancellationToken.None);
        Assert.True(exited.Succeeded, exited.ErrorCode);
        Assert.Equal(
            DungeonRunMemberState.Left,
            exited.Run!.Members.Single(member => member.CharacterId == firstMember.Id).State);

        DungeonOperationResult repeatedExit = await dungeonService.ExitAsync(
            firstMemberAccountId,
            created.Run!.RunId,
            CancellationToken.None);
        Assert.True(repeatedExit.Succeeded, repeatedExit.ErrorCode);
        Assert.Equal(
            DungeonRunMemberState.Left,
            repeatedExit.Run!.Members.Single(member => member.CharacterId == firstMember.Id).State);

        DungeonOperationResult reentered = await dungeonService.EnterAsync(
            firstMemberAccountId,
            created.Run!.RunId,
            CancellationToken.None);
        Assert.True(reentered.Succeeded, reentered.ErrorCode);
        Assert.Equal(
            DungeonRunMemberState.Active,
            reentered.Run!.Members.Single(member => member.CharacterId == firstMember.Id).State);

        DungeonOperationResult leaderExited = await dungeonService.ExitAsync(
            leaderAccountId,
            created.Run!.RunId,
            CancellationToken.None);
        Assert.True(leaderExited.Succeeded, leaderExited.ErrorCode);

        (DungeonPreparation? blockedPreparation, string? blockedError) =
            await dungeonService.PrepareEncounterAsync(
                leaderAccountId,
                created.Run!.RunId,
                CancellationToken.None);
        Assert.Null(blockedPreparation);
        Assert.Equal(DungeonErrorCodes.MemberNotInRun, blockedError);

        DungeonOperationResult leaderReentered = await dungeonService.EnterAsync(
            leaderAccountId,
            created.Run!.RunId,
            CancellationToken.None);
        Assert.True(leaderReentered.Succeeded, leaderReentered.ErrorCode);
    }

    [Fact]
    public async Task DungeonMemberCannotExitOrRestartWhileEncounterIsActive()
    {
        (Guid leaderAccountId, Guid firstMemberAccountId, _) =
            await SeedPartyCharactersAsync();

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        StaticContentSnapshotProvider contentProvider = new(content);

        await using GameDbContext context = postgres.CreateDbContext();
        FixedTimeProvider time = new(Now);
        PartyService partyService = new(context, time);
        DungeonService dungeonService = new(context, partyService, contentProvider, time);

        Assert.True((await partyService.CreateAsync(
            leaderAccountId,
            Guid.NewGuid(),
            CancellationToken.None)).IsSuccess);
        Character firstMember = await context.Characters
            .SingleAsync(character => character.AccountId == firstMemberAccountId);
        Assert.True((await partyService.InviteAsync(
            leaderAccountId,
            Guid.NewGuid(),
            firstMember.Id,
            PartyInviteMode.Direct,
            CancellationToken.None)).IsSuccess);
        PartyInvite invite = await context.PartyInvites.SingleAsync();
        Assert.True((await partyService.AcceptInviteAsync(
            firstMemberAccountId,
            invite.Id,
            CancellationToken.None)).IsSuccess);

        DungeonOperationResult created = await dungeonService.CreateAsync(
            leaderAccountId,
            "ANCIENT_MINE",
            Guid.NewGuid(),
            CancellationToken.None);
        (DungeonPreparation? preparation, string? preparationError) =
            await dungeonService.PrepareEncounterAsync(
                leaderAccountId,
                created.Run!.RunId,
                CancellationToken.None);
        Assert.Null(preparationError);
        Assert.NotNull(preparation);
        Assert.True(await dungeonService.BindCombatAsync(
            preparation!.RunId,
            preparation.EncounterId,
            Guid.NewGuid(),
            CancellationToken.None));

        DungeonOperationResult exited = await dungeonService.ExitAsync(
            firstMemberAccountId,
            created.Run!.RunId,
            CancellationToken.None);
        Assert.False(exited.Succeeded);
        Assert.Equal(DungeonErrorCodes.EncounterActive, exited.ErrorCode);

        DungeonOperationResult restarted = await dungeonService.RestartEncounterAsync(
            leaderAccountId,
            created.Run!.RunId,
            CancellationToken.None);
        Assert.False(restarted.Succeeded);
        Assert.Equal(DungeonErrorCodes.EncounterActive, restarted.ErrorCode);
    }

    private async Task<(Guid LeaderAccountId, Guid FirstMemberAccountId, Guid LateMemberAccountId)>
        SeedPartyCharactersAsync()
    {
        Guid leaderAccountId = Guid.Parse("60000000-0000-0000-0000-000000000001");
        Guid firstMemberAccountId = Guid.Parse("60000000-0000-0000-0000-000000000002");
        Guid lateMemberAccountId = Guid.Parse("60000000-0000-0000-0000-000000000003");
        Guid leaderCharacterId = Guid.Parse("61000000-0000-0000-0000-000000000001");
        Guid firstMemberCharacterId = Guid.Parse("61000000-0000-0000-0000-000000000002");
        Guid lateMemberCharacterId = Guid.Parse("61000000-0000-0000-0000-000000000003");

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.AddRange(
            new Account(leaderAccountId, 6001, Now),
            new Account(firstMemberAccountId, 6002, Now),
            new Account(lateMemberAccountId, 6003, Now));
        Character leader = CreateCharacter(leaderCharacterId, leaderAccountId, "Leader");
        Character firstMember = CreateCharacter(firstMemberCharacterId, firstMemberAccountId, "First");
        Character lateMember = CreateCharacter(lateMemberCharacterId, lateMemberAccountId, "Late");
        leader.SetLevel(15);
        firstMember.SetLevel(15);
        lateMember.SetLevel(15);
        context.Characters.AddRange(leader, firstMember, lateMember);
        context.CharacterVitals.AddRange(
            CreateVitals(leaderCharacterId),
            CreateVitals(firstMemberCharacterId),
            CreateVitals(lateMemberCharacterId));
        context.CharacterLocations.AddRange(
            CreateLocation(leaderCharacterId),
            CreateLocation(firstMemberCharacterId),
            CreateLocation(lateMemberCharacterId));
        await context.SaveChangesAsync();
        return (leaderAccountId, firstMemberAccountId, lateMemberAccountId);
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
        200,
        0,
        Now,
        Now);

    private static CharacterLocation CreateLocation(Guid characterId) => new(
        characterId,
        "ANCIENT_MINE",
        1,
        Now);

    private static CombatSessionSnapshot VictorySnapshot(Guid sessionId)
        => CombatSnapshot(sessionId, CombatSessionStatus.Victory);

    private static CombatSessionSnapshot DefeatSnapshot(Guid sessionId)
        => CombatSnapshot(sessionId, CombatSessionStatus.Defeat);

    private static CombatSessionSnapshot CombatSnapshot(
        Guid sessionId,
        CombatSessionStatus status)
    {
        CombatActorSnapshot player = new(
            Guid.Parse("62000000-0000-0000-0000-000000000001"),
            CombatActorKind.Player,
            "WARRIOR",
            "Leader",
            200,
            200,
            "RAGE",
            0,
            100,
            false,
            null,
            new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(),
            [],
            []);
        CombatActorSnapshot enemy = new(
            Guid.Parse("63000000-0000-0000-0000-000000000001"),
            CombatActorKind.Monster,
            "DEEP_WOLF_L6",
            "Wolf",
            0,
            100,
            "NONE",
            0,
            0,
            false,
            null,
            new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(),
            [],
            []);
        return new CombatSessionSnapshot(
            sessionId,
            1,
            status,
            Now,
            player,
            enemy);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
