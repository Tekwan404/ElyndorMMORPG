using Elyndor.Core.Characters;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Dungeons;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class DungeonNavigationServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 11, 21, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExitToCityKeepsMembershipActiveAndPartyIntact()
    {
        (Guid accountId, Guid characterId, Guid partyId, Guid runId) =
            await SeedRunAsync();

        await using GameDbContext context = postgres.CreateDbContext();
        DungeonNavigationService service = new(context, new FixedTimeProvider(Now.AddMinutes(10)));

        DungeonNavigationResult result = await service.ExitToCityAsync(
            accountId,
            runId,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(WorldLocationIds.StarterTown, result.LocationId);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.True(await verify.PartyMembers.AnyAsync(member =>
            member.PartyId == partyId && member.CharacterId == characterId));
        Assert.Equal(
            DungeonRunMemberState.Active,
            await verify.DungeonRunMembers
                .Where(member => member.RunId == runId && member.CharacterId == characterId)
                .Select(member => member.State)
                .SingleAsync());
        Assert.Equal(
            WorldLocationIds.StarterTown,
            await verify.CharacterLocations
                .Where(location => location.CharacterId == characterId)
                .Select(location => location.LocationId)
                .SingleAsync());
    }

    [Fact]
    public async Task ExitToCityRetryIsIdempotent()
    {
        (Guid accountId, _, _, Guid runId) = await SeedRunAsync();

        await using GameDbContext context = postgres.CreateDbContext();
        DungeonNavigationService service = new(context, new FixedTimeProvider(Now.AddMinutes(10)));

        DungeonNavigationResult first = await service.ExitToCityAsync(accountId, runId, CancellationToken.None);
        DungeonNavigationResult replay = await service.ExitToCityAsync(accountId, runId, CancellationToken.None);

        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.True(replay.Succeeded, replay.ErrorCode);
        Assert.Equal(WorldLocationIds.StarterTown, replay.LocationId);
        Assert.Equal(first.LocationVersion, replay.LocationVersion);
    }

    [Fact]
    public async Task ReturnToRunUsesSameRunAndPreservesProgress()
    {
        (Guid accountId, Guid characterId, _, Guid runId) =
            await SeedRunAsync(progressed: true);

        await using GameDbContext context = postgres.CreateDbContext();
        DungeonNavigationService service = new(context, new FixedTimeProvider(Now.AddMinutes(10)));

        DungeonNavigationResult exited = await service.ExitToCityAsync(
            accountId,
            runId,
            CancellationToken.None);
        Assert.True(exited.Succeeded, exited.ErrorCode);

        DungeonNavigationResult returned = await service.ReturnToRunAsync(
            accountId,
            runId,
            "ECLIPSED_CITADEL",
            "ECLIPSED_CITADEL",
            CancellationToken.None);

        Assert.True(returned.Succeeded, returned.ErrorCode);
        Assert.Equal("ECLIPSED_CITADEL", returned.LocationId);

        await using GameDbContext verify = postgres.CreateDbContext();
        DungeonRun persisted = await verify.DungeonRuns
            .Include(run => run.Members)
            .Include(run => run.Encounters)
            .SingleAsync(run => run.Id == runId);
        Assert.Equal(runId, persisted.Id);
        Assert.Equal(DungeonRunState.Active, persisted.State);
        Assert.Equal(1, persisted.CurrentEncounterIndex);
        Assert.Equal(DungeonRunMemberState.Active, persisted.Members.Single(
            member => member.CharacterId == characterId).State);
        Assert.Contains(persisted.Encounters, encounter =>
            encounter.EncounterIndex == 0 && encounter.State == DungeonEncounterState.Completed);
        Assert.Contains(persisted.Encounters, encounter =>
            encounter.EncounterIndex == 1 && encounter.State == DungeonEncounterState.Pending);
    }

    [Fact]
    public async Task ExitToCityDuringActiveEncounterIsRejected()
    {
        (Guid accountId, Guid characterId, _, Guid runId) =
            await SeedRunAsync(activeEncounter: true);

        await using GameDbContext context = postgres.CreateDbContext();
        DungeonNavigationService service = new(context, new FixedTimeProvider(Now.AddMinutes(10)));

        DungeonNavigationResult result = await service.ExitToCityAsync(
            accountId,
            runId,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(DungeonErrorCodes.EncounterActive, result.ErrorCode);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            "ECLIPSED_CITADEL",
            await verify.CharacterLocations
                .Where(location => location.CharacterId == characterId)
                .Select(location => location.LocationId)
                .SingleAsync());
        Assert.Equal(
            DungeonRunMemberState.Active,
            await verify.DungeonRunMembers
                .Where(member => member.RunId == runId && member.CharacterId == characterId)
                .Select(member => member.State)
                .SingleAsync());
    }

    [Fact]
    public async Task LeaveRunMarksMemberLeftWithoutLeavingParty()
    {
        (Guid accountId, Guid characterId, Guid partyId, Guid runId) =
            await SeedRunAsync();

        await using GameDbContext context = postgres.CreateDbContext();
        DungeonNavigationService service = new(context, new FixedTimeProvider(Now.AddMinutes(10)));

        DungeonNavigationResult result = await service.LeaveRunAsync(
            accountId,
            runId,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(WorldLocationIds.StarterTown, result.LocationId);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.True(await verify.PartyMembers.AnyAsync(member =>
            member.PartyId == partyId && member.CharacterId == characterId));
        Assert.Equal(
            DungeonRunMemberState.Left,
            await verify.DungeonRunMembers
                .Where(member => member.RunId == runId && member.CharacterId == characterId)
                .Select(member => member.State)
                .SingleAsync());
        Assert.Equal(
            DungeonRunState.Abandoned,
            await verify.DungeonRuns
                .Where(run => run.Id == runId)
                .Select(run => run.State)
                .SingleAsync());
    }

    [Fact]
    public async Task LeftMemberCannotReenterActiveRun()
    {
        (Guid accountId, Guid characterId, _, Guid runId) = await SeedRunAsync();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            DungeonRunMember member = await setup.DungeonRunMembers
                .SingleAsync(candidate => candidate.RunId == runId && candidate.CharacterId == characterId);
            member.MarkLeft();
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        DungeonNavigationService service = new(context, new FixedTimeProvider(Now));

        DungeonNavigationResult result = await service.CanEnterAsync(
            accountId,
            runId,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(DungeonErrorCodes.MemberCannotEnter, result.ErrorCode);
    }

    [Fact]
    public async Task LeaveRunThenReturnIsRejected()
    {
        (Guid accountId, _, _, Guid runId) = await SeedRunAsync();

        await using GameDbContext context = postgres.CreateDbContext();
        DungeonNavigationService service = new(context, new FixedTimeProvider(Now.AddMinutes(10)));

        DungeonNavigationResult left = await service.LeaveRunAsync(
            accountId,
            runId,
            CancellationToken.None);
        Assert.True(left.Succeeded, left.ErrorCode);

        DungeonNavigationResult returned = await service.ReturnToRunAsync(
            accountId,
            runId,
            "ECLIPSED_CITADEL",
            "ECLIPSED_CITADEL",
            CancellationToken.None);

        Assert.False(returned.Succeeded);
        Assert.Contains(returned.ErrorCode, new[]
        {
            DungeonErrorCodes.MemberCannotEnter,
            DungeonErrorCodes.EncounterNotReady
        });
    }

    [Fact]
    public async Task CompletedRunCanExitToCityWithoutDisbandingParty()
    {
        (Guid accountId, Guid characterId, Guid partyId, Guid runId) =
            await SeedRunAsync(completed: true);

        await using GameDbContext context = postgres.CreateDbContext();
        DungeonNavigationService service = new(context, new FixedTimeProvider(Now.AddMinutes(10)));

        DungeonNavigationResult result = await service.ExitToCityAsync(
            accountId,
            runId,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(WorldLocationIds.StarterTown, result.LocationId);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.True(await verify.PartyMembers.AnyAsync(member =>
            member.PartyId == partyId && member.CharacterId == characterId));
        Assert.Equal(
            DungeonRunState.Completed,
            await verify.DungeonRuns.Where(run => run.Id == runId).Select(run => run.State).SingleAsync());
        Assert.Equal(
            DungeonRunMemberState.Active,
            await verify.DungeonRunMembers
                .Where(member => member.RunId == runId && member.CharacterId == characterId)
                .Select(member => member.State)
                .SingleAsync());
    }

    private async Task<(Guid AccountId, Guid CharacterId, Guid PartyId, Guid RunId)> SeedRunAsync(
        bool completed = false,
        bool progressed = false,
        bool activeEncounter = false)
    {
        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        Guid partyId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, 4952, Now));
        Character character = new(
            characterId,
            accountId,
            Guid.NewGuid(),
            "DungeonRunner",
            "DUNGEONRUNNER",
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);
        character.SetLevel(25);
        context.Characters.Add(character);
        context.CharacterVitals.Add(new CharacterVitals(characterId, 500, 0, Now, Now));
        context.CharacterLocations.Add(new CharacterLocation(
            characterId,
            "ECLIPSED_CITADEL",
            1,
            Now));

        Party party = Party.Create(
            partyId,
            Guid.NewGuid(),
            characterId,
            Now);
        context.Parties.Add(party);
        context.PartyMembers.AddRange(party.Members);

        DungeonRun run = DungeonRun.Create(
            runId,
            Guid.NewGuid(),
            partyId,
            "ECLIPSED_CITADEL",
            Now);
        run.AddMember(characterId, Now);

        if (progressed)
        {
            DungeonEncounter first = DungeonEncounter.Create(
                Guid.NewGuid(),
                runId,
                0,
                "CITADEL_FIRST",
                Now);
            first.Activate(Guid.NewGuid());
            first.MarkCompleted(Now.AddMinutes(1));
            run.Encounters.Add(first);
            run.AdvanceEncounter(Now.AddMinutes(1));
            run.Encounters.Add(DungeonEncounter.Create(
                Guid.NewGuid(),
                runId,
                1,
                "CITADEL_SECOND",
                Now.AddMinutes(1)));
        }
        else if (activeEncounter)
        {
            DungeonEncounter encounter = DungeonEncounter.Create(
                Guid.NewGuid(),
                runId,
                0,
                "CITADEL_ACTIVE",
                Now);
            encounter.Activate(Guid.NewGuid());
            run.Encounters.Add(encounter);
        }

        if (completed)
            run.Complete(Now.AddMinutes(5));

        context.DungeonRuns.Add(run);
        await context.SaveChangesAsync();

        return (accountId, characterId, partyId, runId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
