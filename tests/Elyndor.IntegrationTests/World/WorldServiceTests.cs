using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.World;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class BootstrapServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private long _nextTelegramUserId = 1_000;

    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BootstrapRestoresAuthoritativeCharacterLocationAndContentVersions()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: true);
        await using GameDbContext context = postgres.CreateDbContext();
        BootstrapService service = new(
            context,
            Content,
            Map,
            new FixedTimeProvider(Now));

        BootstrapSnapshot snapshot = await service.GetAsync(accountId, CancellationToken.None);

        Assert.Equal(accountId, snapshot.AccountId);
        Assert.Equal("0.1.0", snapshot.ContentVersion);
        Assert.Equal("0.1.0", snapshot.BalanceVersion);
        Assert.Equal(Now, snapshot.ServerTimeUtc);
        Assert.NotNull(snapshot.Character);
        Assert.Equal("STARTER_TOWN", snapshot.World!.CurrentLocation.Id);
        Assert.Equal(
            ["WHISPERING_FOREST"],
            snapshot.World.OutgoingTransitions.Select(location => location.Id));
    }

    [Fact]
    public async Task BootstrapReturnsExplicitNoCharacterState()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: false);
        await using GameDbContext context = postgres.CreateDbContext();
        BootstrapService service = new(
            context,
            Content,
            Map,
            new FixedTimeProvider(Now));

        BootstrapSnapshot snapshot = await service.GetAsync(accountId, CancellationToken.None);

        Assert.Null(snapshot.Character);
        Assert.Null(snapshot.World);
    }

    [Fact]
    public async Task TravelUsesActualWorldLinksAndReplaysExactRequest()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: true);
        Guid requestId = Guid.CreateVersion7();

        TravelResult first = await TravelAsync(accountId, requestId, "WHISPERING_FOREST");
        TravelResult replay = await TravelAsync(accountId, requestId, "WHISPERING_FOREST");
        TravelResult mismatch = await TravelAsync(accountId, requestId, "DEEP_FOREST");

        Assert.True(first.IsSuccess);
        Assert.Equal("WHISPERING_FOREST", first.LocationId);
        Assert.Equal(2, first.Version);
        Assert.Equal(first, replay);
        Assert.Equal(TravelErrorCodes.IdempotencyConflict, mismatch.ErrorCode);
    }

    [Fact]
    public async Task DirectTownToDeepAndUnknownTargetsAreRejected()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: true);

        TravelResult direct = await TravelAsync(
            accountId,
            Guid.CreateVersion7(),
            "DEEP_FOREST");
        TravelResult unknown = await TravelAsync(
            accountId,
            Guid.CreateVersion7(),
            "MISSING");

        Assert.Equal(TravelErrorCodes.InvalidTransition, direct.ErrorCode);
        Assert.Equal(TravelErrorCodes.UnknownLocation, unknown.ErrorCode);
    }

    [Fact]
    public async Task ConcurrentTravelFromSameVersionHasOneWinnerAndNoDuplicateOperation()
    {
        Guid accountId = await CreatePlayerAsync(withCharacter: true);

        Task<TravelResult>[] attempts =
        [
            TravelAsync(accountId, Guid.CreateVersion7(), "WHISPERING_FOREST"),
            TravelAsync(accountId, Guid.CreateVersion7(), "WHISPERING_FOREST")
        ];
        TravelResult[] results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(
            results,
            result => result.ErrorCode is TravelErrorCodes.Conflict
                or TravelErrorCodes.InvalidTransition);

        await using GameDbContext context = postgres.CreateDbContext();
        Assert.Equal(1, await context.TravelOperations.CountAsync());
        CharacterLocation location = await context.CharacterLocations.SingleAsync();
        Assert.Equal("WHISPERING_FOREST", location.LocationId);
        Assert.Equal(2, location.Version);
    }

    private async Task<TravelResult> TravelAsync(
        Guid accountId,
        Guid requestId,
        string targetLocationId)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        TravelService service = new(context, Map, new FixedTimeProvider(Now));
        return await service.TravelAsync(
            accountId,
            requestId,
            targetLocationId,
            CancellationToken.None);
    }

    private async Task<Guid> CreatePlayerAsync(bool withCharacter)
    {
        Guid accountId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(
            accountId,
            Interlocked.Increment(ref _nextTelegramUserId),
            Now));

        if (withCharacter)
        {
            Character character = new(
                Guid.CreateVersion7(),
                accountId,
                Guid.CreateVersion7(),
                "Arthas",
                $"ARTHAS{accountId:N}"[..16],
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now);
            context.Characters.Add(character);
            context.CharacterLocations.Add(
                new CharacterLocation(character.Id, "STARTER_TOWN", 1, Now));
        }

        await context.SaveChangesAsync();
        return accountId;
    }

    private static readonly GameContentPackage Content = new(
        "0.1.0",
        "0.1.0",
        Now,
        [],
        [
            new("STARTER_TOWN", "Starter Town", "SAFE", 1, ["WHISPERING_FOREST"]),
            new(
                "WHISPERING_FOREST",
                "Whispering Forest",
                "ADVENTURE",
                1,
                ["STARTER_TOWN", "DEEP_FOREST"]),
            new("DEEP_FOREST", "Deep Forest", "DANGEROUS", 3, ["WHISPERING_FOREST"])
        ]);

    private static readonly WorldMap Map = new(Content.Locations);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
