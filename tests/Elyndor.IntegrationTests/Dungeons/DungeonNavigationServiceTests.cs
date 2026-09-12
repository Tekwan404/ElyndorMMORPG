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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExitLeavesRunWithoutLeavingPartyAndReturnsToSafeTown(bool completed)
    {
        (Guid accountId, Guid characterId, Guid partyId, Guid runId) =
            await SeedRunAsync(completed);

        await using GameDbContext context = postgres.CreateDbContext();
        DungeonNavigationService service = new(context, new FixedTimeProvider(Now.AddMinutes(10)));

        DungeonNavigationResult result = await service.ExitAsync(
            accountId,
            runId,
            CancellationToken.None);

        Assert.True(result.Succeeded);
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
            WorldLocationIds.StarterTown,
            await verify.CharacterLocations
                .Where(location => location.CharacterId == characterId)
                .Select(location => location.LocationId)
                .SingleAsync());
    }

    [Fact]
    public async Task ExitRetryIsIdempotent()
    {
        (Guid accountId, _, _, Guid runId) = await SeedRunAsync(completed: false);

        await using GameDbContext context = postgres.CreateDbContext();
        DungeonNavigationService service = new(context, new FixedTimeProvider(Now.AddMinutes(10)));

        DungeonNavigationResult first = await service.ExitAsync(accountId, runId, CancellationToken.None);
        DungeonNavigationResult replay = await service.ExitAsync(accountId, runId, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(WorldLocationIds.StarterTown, replay.LocationId);
        Assert.Equal(first.LocationVersion, replay.LocationVersion);
    }

    [Fact]
    public async Task LeftMemberCanPassEnterGuardForActiveRun()
    {
        (Guid accountId, Guid characterId, _, Guid runId) = await SeedRunAsync(completed: false);
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

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
    }

    private async Task<(Guid AccountId, Guid CharacterId, Guid PartyId, Guid RunId)> SeedRunAsync(
        bool completed)
    {
        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        Guid partyId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, completed ? 3951 : 3952, Now));
        context.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.NewGuid(),
            completed ? "CompletedRunner" : "ActiveRunner",
            completed ? "COMPLETEDRUNNER" : "ACTIVERUNNER",
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
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
        if (completed)
            run.Complete(Now.AddMinutes(5));
        context.DungeonRuns.Add(run);
        context.DungeonRunMembers.AddRange(run.Members);
        await context.SaveChangesAsync();

        return (accountId, characterId, partyId, runId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}