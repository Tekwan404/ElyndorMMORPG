using System.Text.Json;
using Elyndor.Core.Afk;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Monsters;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Afk;

public static class AfkFarmErrorCodes
{
    public const string CharacterNotFound = "afk_character_not_found";
    public const string CharacterDead = "afk_character_dead";
    public const string InCombat = "afk_conflict_combat";
    public const string Traveling = "afk_conflict_travel";
    public const string InDungeon = "afk_conflict_dungeon";
    public const string InvalidLocation = "afk_invalid_location";
    public const string LockedLocation = "afk_locked_location";
    public const string NotAllowed = "afk_not_allowed";
    public const string NoEligibleEncounters = "afk_no_eligible_encounters";
    public const string AlreadyActive = "afk_already_active";
    public const string InvalidDuration = "afk_invalid_duration";
    public const string NoSession = "afk_session_not_found";
}

public sealed record AfkFarmMutationResult(
    bool Succeeded,
    string? ErrorCode,
    AfkFarmSession? Session)
{
    public static AfkFarmMutationResult Success(AfkFarmSession session) =>
        new(true, null, session);

    public static AfkFarmMutationResult Failure(string errorCode, AfkFarmSession? session = null) =>
        new(false, errorCode, session);
}

public sealed class AfkFarmService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    CharacterDerivedStateService derivedStateService,
    CharacterOperationGuard operationGuard,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public Task<AfkFarmMutationResult> StartAsync(
        Guid accountId,
        string locationId,
        AfkFarmMode mode,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            return Task.FromResult(AfkFarmMutationResult.Failure(AfkFarmErrorCodes.CharacterNotFound));
        if (string.IsNullOrWhiteSpace(locationId))
            return Task.FromResult(AfkFarmMutationResult.Failure(AfkFarmErrorCodes.InvalidLocation));
        if (duration <= TimeSpan.Zero)
            return Task.FromResult(AfkFarmMutationResult.Failure(AfkFarmErrorCodes.InvalidDuration));

        return operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            () => dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
                () => StartCoreAsync(accountId, locationId, mode, duration, cancellationToken)),
            () => AfkFarmMutationResult.Failure(AfkFarmErrorCodes.InCombat),
            cancellationToken);
    }

    public Task<AfkFarmMutationResult> StopAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            return Task.FromResult(AfkFarmMutationResult.Failure(AfkFarmErrorCodes.CharacterNotFound));

        return operationGuard.ExecuteExclusiveAsync(
            accountId,
            () => dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
                () => StopCoreAsync(accountId, cancellationToken)),
            cancellationToken);
    }

    private async Task<AfkFarmMutationResult> StartCoreAsync(
        Guid accountId,
        string locationId,
        AfkFarmMode mode,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        Character? character = await LockCharacterAsync(accountId, cancellationToken);
        if (character is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.CharacterNotFound);
        }

        await AcquireCharacterActivityLockAsync(character.Id, cancellationToken);

        if (await dbContext.ActiveCombatSessions.AsNoTracking()
            .AnyAsync(state => state.CharacterId == character.Id, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.InCombat);
        }

        CharacterVitals? vitals = await dbContext.CharacterVitals
            .AsNoTracking()
            .SingleOrDefaultAsync(state => state.CharacterId == character.Id, cancellationToken);
        if (vitals is null || vitals.CurrentHp <= 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.CharacterDead);
        }

        if (await dbContext.CharacterTravelStates.AsNoTracking()
            .AnyAsync(state => state.CharacterId == character.Id, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.Traveling);
        }

        bool inDungeon = await dbContext.DungeonRunMembers.AsNoTracking()
            .AnyAsync(
                member => member.CharacterId == character.Id
                    && member.State == DungeonRunMemberState.Active
                    && dbContext.DungeonRuns.Any(run =>
                        run.Id == member.RunId
                        && run.State == DungeonRunState.Active),
                cancellationToken);
        if (inDungeon)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.InDungeon);
        }

        if (await dbContext.AfkFarmSessions.AsNoTracking()
            .AnyAsync(
                session => session.CharacterId == character.Id
                    && session.Status == AfkFarmStatus.Active,
                cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.AlreadyActive);
        }

        CharacterLocation? currentLocation = await dbContext.CharacterLocations
            .AsNoTracking()
            .SingleOrDefaultAsync(state => state.CharacterId == character.Id, cancellationToken);
        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        if (currentLocation is null
            || !string.Equals(currentLocation.LocationId, locationId, StringComparison.Ordinal)
            || !contentSnapshot.Indexes.LocationsById.TryGetValue(locationId, out LocationDefinition? location))
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.InvalidLocation);
        }

        if (character.Level < location.MinimumLevel || character.Level > location.MaximumLevel)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.LockedLocation);
        }

        if (!string.IsNullOrWhiteSpace(location.RequiredContractId)
            && !await dbContext.CharacterContractCompletions.AsNoTracking()
                .AnyAsync(
                    completion => completion.CharacterId == character.Id
                        && completion.ContractId == location.RequiredContractId,
                    cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.LockedLocation);
        }

        if (!location.AllowAfk
            || location.DangerLevel is not ("SAFE" or "ADVENTURE"))
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.NotAllowed);
        }

        bool hasEligibleEncounter = location.Encounters?.Any(encounter =>
            encounter.Weight > 0
            && contentSnapshot.Indexes.MonstersById.TryGetValue(encounter.MonsterId, out MonsterDefinition? monster)
            && monster.Rank == MonsterRank.Normal) == true;
        if (!hasEligibleEncounter)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.NoEligibleEncounters);
        }

        CharacterDerivedState derived = await derivedStateService.ResolveAsync(
            character.Id,
            character.ClassId,
            character.Level,
            contentSnapshot,
            cancellationToken);
        AfkCharacterSnapshot characterSnapshot = new(
            character.ClassId,
            character.Level,
            derived.Stats,
            vitals.CurrentHp,
            vitals.CurrentResource,
            JsonSerializer.Serialize(derived.ClassProfile, SnapshotJsonOptions),
            JsonSerializer.Serialize(derived.EffectiveResourceProfile, SnapshotJsonOptions),
            JsonSerializer.Serialize(derived.Inventory.Equipped, SnapshotJsonOptions),
            derived.ActiveTalentRanks.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal),
            JsonSerializer.Serialize(derived.TalentModifiers, SnapshotJsonOptions),
            derived.KnownAbilityIds.ToArray());

        DateTimeOffset now = timeProvider.GetUtcNow();
        AfkFarmSession session = new(
            Guid.NewGuid(),
            character.Id,
            location.Id,
            mode,
            now,
            now.Add(duration),
            contentSnapshot.ContentVersion,
            contentSnapshot.BalanceVersion,
            JsonSerializer.Serialize(characterSnapshot, SnapshotJsonOptions),
            now);

        dbContext.AfkFarmSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AfkFarmMutationResult.Success(session);
    }

    private async Task<AfkFarmMutationResult> StopCoreAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        Character? character = await LockCharacterAsync(accountId, cancellationToken);
        if (character is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.CharacterNotFound);
        }

        await AcquireCharacterActivityLockAsync(character.Id, cancellationToken);

        AfkFarmSession? session = await dbContext.AfkFarmSessions
            .Where(candidate => candidate.CharacterId == character.Id)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return AfkFarmMutationResult.Failure(AfkFarmErrorCodes.NoSession);
        }

        if (session.Status == AfkFarmStatus.Active)
        {
            session.Cancel(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return AfkFarmMutationResult.Success(session);
    }

    private Task<Character?> LockCharacterAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Characters
            .FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private Task AcquireCharacterActivityLockAsync(Guid characterId, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return Task.CompletedTask;

        string lockKey = $"combat-character:{characterId:N}";
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))",
            cancellationToken);
    }
}
