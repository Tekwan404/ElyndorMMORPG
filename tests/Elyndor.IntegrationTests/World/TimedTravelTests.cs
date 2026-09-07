using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.World;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class TimedTravelTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 9, 30, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TravelPersistsUntilDueAndCompletesAfterOfflineTime()
    {
        (Guid accountId, Guid characterId) = await CreateCharacterAsync();
        var time = new MutableTimeProvider(Now);
        var content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        WorldMap worldMap = new(content.Locations);
        Guid requestId = Guid.CreateVersion7();

        await using GameDbContext context = postgres.CreateDbContext();
        TravelService service = new(context, worldMap, time);

        TravelResult started = await service.TravelAsync(
            accountId,
            requestId,
            "WHISPERING_FOREST",
            CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.True(started.IsTravelling);
        Assert.Equal("STARTER_TOWN", started.LocationId);
        Assert.Equal("WHISPERING_FOREST", started.TargetLocationId);
        Assert.NotNull(started.EndsAtUtc);

        await using (GameDbContext verifyStarted = postgres.CreateDbContext())
        {
            CharacterLocation location = await verifyStarted.CharacterLocations
                .AsNoTracking()
                .SingleAsync(state => state.CharacterId == characterId);
            CharacterTravelState travel = await verifyStarted.CharacterTravelStates
                .AsNoTracking()
                .SingleAsync(state => state.CharacterId == characterId);
            Assert.Equal("STARTER_TOWN", location.LocationId);
            Assert.Equal("WHISPERING_FOREST", travel.TargetLocationId);
            Assert.Empty(await verifyStarted.TravelOperations.ToArrayAsync());
        }

        time.Advance(TimeSpan.FromSeconds(6));

        TravelResult completed = await service.TravelAsync(
            accountId,
            requestId,
            "WHISPERING_FOREST",
            CancellationToken.None);

        Assert.True(completed.IsSuccess);
        Assert.False(completed.IsTravelling);
        Assert.Equal("WHISPERING_FOREST", completed.LocationId);

        await using GameDbContext verifyCompleted = postgres.CreateDbContext();
        Assert.Equal(
            "WHISPERING_FOREST",
            await verifyCompleted.CharacterLocations
                .Where(state => state.CharacterId == characterId)
                .Select(state => state.LocationId)
                .SingleAsync());
        Assert.Empty(await verifyCompleted.CharacterTravelStates.ToArrayAsync());
        Assert.Single(await verifyCompleted.TravelOperations
            .Where(operation =>
                operation.CharacterId == characterId
                && operation.RequestId == requestId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task SecondTravelCannotReplaceActiveJourney()
    {
        (Guid accountId, _) = await CreateCharacterAsync();
        var time = new MutableTimeProvider(Now);
        var content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        TravelService service = new(
            postgres.CreateDbContext(),
            new WorldMap(content.Locations),
            time);

        TravelResult first = await service.TravelAsync(
            accountId,
            Guid.CreateVersion7(),
            "WHISPERING_FOREST",
            CancellationToken.None);
        TravelResult second = await service.TravelAsync(
            accountId,
            Guid.CreateVersion7(),
            "WHISPERING_FOREST",
            CancellationToken.None);

        Assert.True(first.IsTravelling);
        Assert.False(second.IsSuccess);
        Assert.Equal(TravelErrorCodes.InProgress, second.ErrorCode);
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(
            accountId,
            Random.Shared.NextInt64(1, long.MaxValue),
            Now));
        context.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "Traveler",
            $"TRAVEL{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        context.CharacterLocations.Add(new CharacterLocation(
            characterId,
            "STARTER_TOWN",
            1,
            Now));
        await context.SaveChangesAsync();
        return (accountId, characterId);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan duration) => current += duration;
    }
}
