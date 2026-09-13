using Elyndor.Core.Afk;
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
            .Include(candidate => candidate.Encounters)
            .SingleOrDefaultAsync(candidate => candidate.Id == runId, cancellationToken);
        if (run is null)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.RunNotFound);
        if (run.State != DungeonRunState.Active)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.EncounterNotReady);

        DungeonRunMember? member = run.Members
            .SingleOrDefault(candidate => candidate.CharacterId == characterId.Value);
        if (member?.State == DungeonRunMemberState.Left)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.MemberCannotEnter);

        if (run.Encounters.Any(encounter => encounter.State == DungeonEncounterState.Active))
            return DungeonNavigationResult.Failure(DungeonErrorCodes.EncounterActive);

        if (member is not null)
            return DungeonNavigationResult.Success();

        bool belongsToParty = await dbContext.PartyMembers
            .AsNoTracking()
            .AnyAsync(candidate => candidate.PartyId == run.PartyId
                && candidate.CharacterId == characterId.Value, cancellationToken);
        if (!belongsToParty)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.MemberNotInParty);

        return DungeonNavigationResult.Success();
    }

    public Task<DungeonNavigationResult> ExitToCityAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ExitToCityCoreAsync(accountId, runId, cancellationToken));

    public Task<DungeonNavigationResult> LeaveRunAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => LeaveRunCoreAsync(accountId, runId, cancellationToken));

    public Task<DungeonNavigationResult> ReturnToRunAsync(
        Guid accountId,
        Guid runId,
        string dungeonId,
        string entryLocationId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ReturnToRunCoreAsync(
                accountId,
                runId,
                dungeonId,
                entryLocationId,
                cancellationToken));

    private async Task<DungeonNavigationResult> ExitToCityCoreAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireRunLockAsync(runId, cancellationToken);

        (Guid? characterId, DungeonRun? run, DungeonRunMember? member, CharacterLocation? location) =
            await LoadNavigationStateAsync(accountId, runId, cancellationToken);
        DungeonNavigationResult? validation = ValidateNavigationState(characterId, run, member, location);
        if (validation is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return validation;
        }

        if (member!.State != DungeonRunMemberState.Active)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.MemberCannotEnter);
        }
        if (run!.State == DungeonRunState.Abandoned)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.EncounterNotReady);
        }
        if (run.Encounters.Any(encounter => encounter.State == DungeonEncounterState.Active))
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.EncounterActive);
        }

        await RemoveStaleTravelAsync(characterId!.Value, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!string.Equals(location!.LocationId, WorldLocationIds.StarterTown, StringComparison.Ordinal))
            location.Relocate(WorldLocationIds.StarterTown, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return DungeonNavigationResult.Success(location.LocationId, location.Version);
    }

    private async Task<DungeonNavigationResult> LeaveRunCoreAsync(
        Guid accountId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireRunLockAsync(runId, cancellationToken);

        (Guid? characterId, DungeonRun? run, DungeonRunMember? member, CharacterLocation? location) =
            await LoadNavigationStateAsync(accountId, runId, cancellationToken);
        DungeonNavigationResult? validation = ValidateNavigationState(characterId, run, member, location);
        if (validation is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return validation;
        }

        if (member!.State != DungeonRunMemberState.Left)
        {
            member.MarkLeft();
            if (run!.State == DungeonRunState.Active
                && run.Members.All(candidate => candidate.State == DungeonRunMemberState.Left))
            {
                run.Abandon();
            }
        }

        await RemoveStaleTravelAsync(characterId!.Value, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!string.Equals(location!.LocationId, WorldLocationIds.StarterTown, StringComparison.Ordinal))
            location.Relocate(WorldLocationIds.StarterTown, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return DungeonNavigationResult.Success(location.LocationId, location.Version);
    }

    private async Task<DungeonNavigationResult> ReturnToRunCoreAsync(
        Guid accountId,
        Guid runId,
        string dungeonId,
        string entryLocationId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        if (string.IsNullOrWhiteSpace(dungeonId) || string.IsNullOrWhiteSpace(entryLocationId))
            return DungeonNavigationResult.Failure(DungeonErrorCodes.DungeonNotFound);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await AcquireRunLockAsync(runId, cancellationToken);

        (Guid? characterId, DungeonRun? run, DungeonRunMember? member, CharacterLocation? location) =
            await LoadNavigationStateAsync(accountId, runId, cancellationToken);
        DungeonNavigationResult? validation = ValidateNavigationState(characterId, run, member, location);
        if (validation is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return validation;
        }

        if (run!.State != DungeonRunState.Active
            || !string.Equals(run.DungeonId, dungeonId, StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.EncounterNotReady);
        }
        if (member!.State != DungeonRunMemberState.Active)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.MemberCannotEnter);
        }
        if (run.Encounters.Any(encounter => encounter.State == DungeonEncounterState.Active))
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.EncounterActive);
        }

        bool isAlive = await dbContext.CharacterVitals
            .AsNoTracking()
            .AnyAsync(vitals => vitals.CharacterId == characterId!.Value && vitals.CurrentHp > 0, cancellationToken);
        if (!isAlive)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.MemberCannotEnter);
        }
        if (await dbContext.CharacterTravelStates
                .AsNoTracking()
                .AnyAsync(state => state.CharacterId == characterId!.Value, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.TravelInProgress);
        }
        if (await dbContext.AfkFarmSessions
                .AsNoTracking()
                .AnyAsync(session => session.CharacterId == characterId!.Value
                    && session.Status == AfkFarmStatus.Active, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return DungeonNavigationResult.Failure(DungeonErrorCodes.AfkFarmActive);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!string.Equals(location!.LocationId, entryLocationId, StringComparison.Ordinal))
            location.Relocate(entryLocationId, now);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return DungeonNavigationResult.Success(location.LocationId, location.Version);
    }

    private async Task<(Guid? CharacterId, DungeonRun? Run, DungeonRunMember? Member, CharacterLocation? Location)>
        LoadNavigationStateAsync(
            Guid accountId,
            Guid runId,
            CancellationToken cancellationToken)
    {
        Guid? characterId = await dbContext.Characters
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!characterId.HasValue)
            return (null, null, null, null);

        DungeonRun? run = await dbContext.DungeonRuns
            .Include(candidate => candidate.Members)
            .Include(candidate => candidate.Encounters)
            .SingleOrDefaultAsync(candidate => candidate.Id == runId, cancellationToken);
        DungeonRunMember? member = run?.Members
            .SingleOrDefault(candidate => candidate.CharacterId == characterId.Value);
        CharacterLocation? location = await dbContext.CharacterLocations
            .SingleOrDefaultAsync(candidate => candidate.CharacterId == characterId.Value, cancellationToken);
        return (characterId, run, member, location);
    }

    private static DungeonNavigationResult? ValidateNavigationState(
        Guid? characterId,
        DungeonRun? run,
        DungeonRunMember? member,
        CharacterLocation? location)
    {
        if (!characterId.HasValue)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.CharacterNotFound);
        if (run is null)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.RunNotFound);
        if (member is null)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.MemberNotInRun);
        if (location is null)
            return DungeonNavigationResult.Failure(DungeonErrorCodes.InvalidLocation);
        return null;
    }

    private async Task RemoveStaleTravelAsync(Guid characterId, CancellationToken cancellationToken)
    {
        CharacterTravelState? staleTravel = await dbContext.CharacterTravelStates
            .SingleOrDefaultAsync(state => state.CharacterId == characterId, cancellationToken);
        if (staleTravel is not null)
            dbContext.CharacterTravelStates.Remove(staleTravel);
    }

    private Task AcquireRunLockAsync(Guid runId, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return Task.CompletedTask;

        string runLock = $"dungeon-run:{runId:N}";
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({runLock}))",
            cancellationToken);
    }
}
