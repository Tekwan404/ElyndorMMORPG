using Elyndor.Core.Characters;
using Elyndor.Core.World;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.World;

public sealed record TravelResult(
    bool IsSuccess,
    string? LocationId,
    long? Version,
    string? ErrorCode)
{
    public static TravelResult Success(string locationId, long version) =>
        new(true, locationId, version, null);

    public static TravelResult Failure(string errorCode) =>
        new(false, null, null, errorCode);
}

public static class TravelErrorCodes
{
    public const string CharacterNotFound = "character_not_found";
    public const string InvalidRequest = "travel_request_invalid";
    public const string UnknownLocation = "travel_location_unknown";
    public const string InvalidTransition = "travel_transition_invalid";
    public const string IdempotencyConflict = "idempotency_conflict";
    public const string Conflict = "travel_conflict";
}

public sealed class TravelService
{
    private readonly GameDbContext dbContext;
    private readonly IContentSnapshotProvider? contentProvider;
    private readonly WorldMap? fixedWorldMap;
    private readonly TimeProvider timeProvider;

    public TravelService(
        GameDbContext dbContext,
        IContentSnapshotProvider contentProvider,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public TravelService(
        GameDbContext dbContext,
        WorldMap worldMap,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        fixedWorldMap = worldMap ?? throw new ArgumentNullException(nameof(worldMap));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<TravelResult> TravelAsync(
        Guid accountId,
        Guid requestId,
        string targetLocationId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty
            || requestId == Guid.Empty
            || string.IsNullOrWhiteSpace(targetLocationId))
        {
            return TravelResult.Failure(TravelErrorCodes.InvalidRequest);
        }

        WorldMap worldMap = contentProvider?.GetCurrent().WorldMap
            ?? fixedWorldMap
            ?? throw new InvalidOperationException("World map content is unavailable.");

        try
        {
            worldMap.GetRequired(targetLocationId);
        }
        catch (KeyNotFoundException)
        {
            return TravelResult.Failure(TravelErrorCodes.UnknownLocation);
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            () => TravelCoreAsync(
                accountId,
                requestId,
                targetLocationId,
                worldMap,
                timeProvider.GetUtcNow(),
                cancellationToken));
    }

    private async Task<TravelResult> TravelCoreAsync(
        Guid accountId,
        Guid requestId,
        string targetLocationId,
        WorldMap worldMap,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId,
                cancellationToken);
        if (character is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return TravelResult.Failure(TravelErrorCodes.CharacterNotFound);
        }

        TravelOperation? replay = await dbContext.TravelOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                operation => operation.CharacterId == character.Id
                    && operation.RequestId == requestId,
                cancellationToken);
        if (replay is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return ToReplay(replay, targetLocationId);
        }

        CharacterLocation location = await dbContext.CharacterLocations
            .AsNoTracking()
            .SingleAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);
        if (!worldMap.CanTravel(location.LocationId, targetLocationId))
        {
            await transaction.CommitAsync(cancellationToken);
            return TravelResult.Failure(TravelErrorCodes.InvalidTransition);
        }

        long resultVersion = checked(location.Version + 1);
        int affectedRows = await dbContext.CharacterLocations
            .Where(candidate => candidate.CharacterId == character.Id
                && candidate.Version == location.Version)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.LocationId, targetLocationId)
                    .SetProperty(candidate => candidate.Version, resultVersion)
                    .SetProperty(candidate => candidate.UpdatedAtUtc, now),
                cancellationToken);

        if (affectedRows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return await ResolveConcurrentResultAsync(
                character.Id,
                requestId,
                targetLocationId,
                cancellationToken);
        }

        dbContext.TravelOperations.Add(new TravelOperation(
            character.Id,
            requestId,
            targetLocationId,
            targetLocationId,
            resultVersion,
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TravelResult.Success(targetLocationId, resultVersion);
    }

    private async Task<TravelResult> ResolveConcurrentResultAsync(
        Guid characterId,
        Guid requestId,
        string targetLocationId,
        CancellationToken cancellationToken)
    {
        TravelOperation? operation = await dbContext.TravelOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CharacterId == characterId
                    && candidate.RequestId == requestId,
                cancellationToken);
        return operation is null
            ? TravelResult.Failure(TravelErrorCodes.Conflict)
            : ToReplay(operation, targetLocationId);
    }

    private static TravelResult ToReplay(
        TravelOperation operation,
        string targetLocationId) =>
        string.Equals(
            operation.TargetLocationId,
            targetLocationId,
            StringComparison.Ordinal)
                ? TravelResult.Success(
                    operation.ResultLocationId,
                    operation.ResultVersion)
                : TravelResult.Failure(TravelErrorCodes.IdempotencyConflict);
}
