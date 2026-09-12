using Elyndor.Core.Dungeons;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Dungeons;

public sealed record DungeonNavigationResult(
    bool Succeeded,
    string? ErrorCode,
    string? LocationId = null,
    long? LocationVersion = null)
{
    public static DungeonNavigationResult Success(
        string? locationId = null,
        long? locationVersion = null) =>
        new(true, null, locationId, locationVersion);

    public static DungeonNavigationResult Failure(string code) =>
        new(false, code);
}

public sealed class DungeonNavigationService(
    GameDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<DungeonNavigationResult> CanEnterAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty || runId == Guid.Empty)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.MemberCannotEnter);

        Guid? characterId = await dbContext.Characters
            .AsNoTracking()
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!characterId.HasValue)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.CharacterNotFound);

        DungeonRun? run = await dbContext.DungeonRuns
            .AsNoTracking()
            .Include(candidate => candidate.Members)
            .SingleOrDefaultAsync(candidate => candidate.Id == runId, cancellationToken);
        if (run is null)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.RunNotFound);
        if (run.State != DungeonRunState.Active)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.EncounterNotReady);

        DungeonRunMember? member = run.Members
            .SingleOrDefault(candidate => candidate.CharacterId == characterId.Value);
        if (member is null)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.MemberNotInRun);

        return DungeonNavigationResult.Success();
    }

    public Task<DungeonNavigationResult> ExitAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ExitCoreAsync(accountId, runId, cancellationToken));

    private async Task<DungeonNavigationResult> ExitCoreAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (dbContext.Database.IsNpgsql())
        {
            string runLock = $"dungeon-run:{runId:N}";
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({runLock}))",
                cancellationToken);
        }

        Guid? characterId = await dbContext.Characters
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!characterId.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.CharacterNotFound);
        }

        DungeonRun? run = await dbContext.DungeonRuns
            .Include(candidate => candidate.Members)
            .Include(candidate => candidate.Encounters)
            .SingleOrDefaultAsync(candidate => candidate.Id == runId, cancellationToken);
        if (run is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.RunNotFound);
        }

        DungeonRunMember? member = run.Members
            .SingleOrDefault(candidate => candidate.CharacterId == characterId.Value);
        if (member is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.MemberNotInRun);
        }

        CharacterLocation? location = await dbContext.CharacterLocations
            .SingleOrDefaultAsync(candidate => candidate.CharacterId == characterId.Value, cancellationToken);
        if (location is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.InvalidLocation);
        }

        if (member.State == DungeonRunMemberState.Left || run.State == DungeonRunState.Abandoned)
        {
            await transaction.CommitAsync(cancellationToken);
            return DungeonNavigationResult.Success(location.LocationId, location.Version);
        }

        if (run.Encounters.Any(encounter => encounter.State == DungeonEncounterState.Active))
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.EncounterActive);
        }

        member.MarkLeft();
        if (run.State == DungeonRunState.Active
            && run.Members.All(candidate => candidate.State == DungeonRunMemberState.Left))
        {
            run.Abandon();
        }

        CharacterTravelState? staleTravel = await dbContext.CharacterTravelStates
            .SingleOrDefaultAsync(state => state.CharacterId == characterId.Value, cancellationToken);
        if (staleTravel is not null)
            dbContext.CharacterTravelStates.Remove(staleTravel);

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!string.Equals(location.LocationId, WorldLocationIds.StarterTown, StringComparison.Ordinal))
            location.Relocate(WorldLocationIds.StarterTown, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return DungeonNavigationResult.Success(location.LocationId, location.Version);
    }
}