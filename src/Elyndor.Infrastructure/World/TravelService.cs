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
    string? ErrorCode,
    bool IsTravelling = false,
    string? TargetLocationId = null,
    DateTimeOffset? EndsAtUtc = null)
{
    public static TravelResult Completed(string locationId, long version) =>
        new(true, locationId, version, null);

    public static TravelResult Started(
        string locationId,
        long version,
        string targetLocationId,
        DateTimeOffset endsAtUtc) =>
        new(
            true,
            locationId,
            version,
            null,
            true,
            targetLocationId,
            endsAtUtc);

    public static TravelResult Failure(string errorCode) =>
        new(false, null, null, errorCode);
}

public static class TravelErrorCodes
{
    public const string CharacterNotFound = "character_not_found";
    public const string InvalidRequest = "travel_request_invalid";
    public const string UnknownLocation = "travel_location_unknown";
    public const string InvalidTransition = "travel_transition_invalid";
    public const string LevelRequired = "travel_level_required";
    public const string ContractRequired = "travel_contract_required";
    public const string IdempotencyConflict = "idempotency_conflict";
    public const string InProgress = "travel_in_progress";
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

        LocationDefinition target;
        try
        {
            target = worldMap.GetRequired(targetLocationId);
        }
        catch (KeyNotFoundException)
        {
            return TravelResult.Failure(TravelErrorCodes.UnknownLocation);
        }

        if (target.TravelDurationSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"Location '{target.Id}' has invalid travel duration.");
        }

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(
                () => TravelCoreAsync(
                    accountId,
                    requestId,
                    target,
                    worldMap,
                    timeProvider.GetUtcNow(),
                    cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            return TravelResult.Failure(TravelErrorCodes.Conflict);
        }
    }

    private async Task<TravelResult> TravelCoreAsync(
        Guid accountId,
        Guid requestId,
        LocationDefinition target,
        WorldMap worldMap,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        Character? character = await dbContext.Characters
            .FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (character is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return TravelResult.Failure(TravelErrorCodes.CharacterNotFound);
        }

        await TravelPersistence.CompleteDueAsync(
            dbContext,
            character.Id,
            now,
            cancellationToken);

        TravelOperation? replay = await dbContext.TravelOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                operation => operation.CharacterId == character.Id
                    && operation.RequestId == requestId,
                cancellationToken);
        if (replay is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return ToReplay(replay, target.Id);
        }

        CharacterLocation location = await dbContext.CharacterLocations
            .SingleAsync(
                candidate => candidate.CharacterId == character.Id,
                cancellationToken);

        CharacterTravelState? active = await dbContext.CharacterTravelStates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                state => state.CharacterId == character.Id,
                cancellationToken);
        if (active is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return active.RequestId == requestId
                && string.Equals(
                    active.TargetLocationId,
                    target.Id,
                    StringComparison.Ordinal)
                    ? TravelResult.Started(
                        location.LocationId,
                        location.Version,
                        active.TargetLocationId,
                        active.EndsAtUtc)
                    : TravelResult.Failure(TravelErrorCodes.InProgress);
        }

        if (!worldMap.CanTravel(location.LocationId, target.Id))
        {
            await transaction.RollbackAsync(cancellationToken);
            return TravelResult.Failure(TravelErrorCodes.InvalidTransition);
        }

        if (character.Level < target.MinimumLevel)
        {
            await transaction.RollbackAsync(cancellationToken);
            return TravelResult.Failure(TravelErrorCodes.LevelRequired);
        }

        if (target.RequiredContractId is { Length: > 0 } requiredContractId)
        {
            bool completed = await dbContext.CharacterContractCompletions
                .AsNoTracking()
                .AnyAsync(
                    state => state.CharacterId == character.Id
                        && state.ContractId == requiredContractId,
                    cancellationToken);
            if (!completed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TravelResult.Failure(TravelErrorCodes.ContractRequired);
            }
        }

        DateTimeOffset endsAtUtc = now.AddSeconds(
            (double)target.TravelDurationSeconds);
        dbContext.CharacterTravelStates.Add(new CharacterTravelState(
            character.Id,
            requestId,
            location.LocationId,
            target.Id,
            now,
            endsAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TravelResult.Started(
            location.LocationId,
            location.Version,
            target.Id,
            endsAtUtc);
    }

    private static TravelResult ToReplay(
        TravelOperation operation,
        string targetLocationId) =>
        string.Equals(
            operation.TargetLocationId,
            targetLocationId,
            StringComparison.Ordinal)
                ? TravelResult.Completed(
                    operation.ResultLocationId,
                    operation.ResultVersion)
                : TravelResult.Failure(TravelErrorCodes.IdempotencyConflict);
}
