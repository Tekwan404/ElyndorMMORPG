using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.WorldBosses;

public static class WorldBossErrorCodes
{
    public const string SpawnNotFound = "world_boss_spawn_not_found";
    public const string AlreadyDefeated = "world_boss_already_defeated";
    public const string Expired = "world_boss_expired";
    public const string NotActive = "world_boss_not_active";
    public const string MutationConflict = "world_boss_mutation_conflict";
}

public sealed record WorldBossDamageCommitResult(
    bool Succeeded,
    string? ErrorCode,
    bool Replayed,
    decimal AppliedDamage,
    decimal CurrentHealth,
    decimal MaxHealth,
    int Phase,
    WorldBossSpawnStatus? Status,
    bool DefeatedNow,
    bool PhaseChanged);

public sealed class WorldBossDamageService(
    GameDbContext db,
    TimeProvider time,
    IContentSnapshotProvider? contentProvider = null,
    IWorldBossUpdatePublisher? updatePublisher = null,
    ILogger<WorldBossDamageService>? logger = null)
{
    private static readonly Action<ILogger, Guid, Exception?> RealtimeDefeatDeliveryFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(4201, nameof(RealtimeDefeatDeliveryFailed)),
            "World boss {SpawnId} was defeated, but realtime defeat delivery failed.");

    private static readonly Action<ILogger, Guid, Guid, Guid, decimal, Exception?> BossDefeated =
        LoggerMessage.Define<Guid, Guid, Guid, decimal>(
            LogLevel.Information,
            new EventId(4203, nameof(BossDefeated)),
            "World boss defeated: spawn {SpawnId}, final character {CharacterId}, "
            + "combatSession {CombatSessionId}, finalAppliedDamage {AppliedDamage}.");

    public async Task<WorldBossDamageCommitResult> ApplyDamageAsync(
        Guid spawnId,
        Guid characterId,
        Guid combatSessionId,
        Guid? partyId,
        decimal requestedDamage,
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        Validate(spawnId, characterId, combatSessionId, partyId, requestedDamage, mutationId);

        WorldBossDamageCommitResult result = await db.Database
            .CreateExecutionStrategy()
            .ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            WorldBossSpawn? spawn = await db.WorldBossSpawns
                .FromSqlInterpolated(
                    $"SELECT * FROM game.world_boss_spawns WHERE \"Id\" = {spawnId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (spawn is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Missing();
            }

            WorldBossDamageMutation? existing = await db.WorldBossDamageMutations.FindAsync(
                [spawnId, mutationId],
                cancellationToken);

            if (existing is not null)
            {
                if (!Matches(existing, characterId, combatSessionId, partyId, requestedDamage))
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return FromSpawn(
                        spawn,
                        succeeded: false,
                        WorldBossErrorCodes.MutationConflict,
                        replayed: false,
                        appliedDamage: 0,
                        defeatedNow: false,
                        phaseChanged: false);
                }

                await transaction.RollbackAsync(CancellationToken.None);
                return FromSpawn(
                    spawn,
                    succeeded: true,
                    errorCode: null,
                    replayed: true,
                    existing.AppliedDamage,
                    defeatedNow: false,
                    phaseChanged: false);
            }

            DateTimeOffset now = time.GetUtcNow();
            if (spawn.Status == WorldBossSpawnStatus.Active && now >= spawn.ExpiresAtUtc)
            {
                spawn.TryExpire(now);
                await db.SaveChangesAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                await transaction.CommitAsync(CancellationToken.None);
                return FromSpawn(
                    spawn,
                    succeeded: false,
                    WorldBossErrorCodes.Expired,
                    replayed: false,
                    appliedDamage: 0,
                    defeatedNow: false,
                    phaseChanged: false);
            }

            string? inactiveError = spawn.Status switch
            {
                WorldBossSpawnStatus.Defeated => WorldBossErrorCodes.AlreadyDefeated,
                WorldBossSpawnStatus.Expired => WorldBossErrorCodes.Expired,
                WorldBossSpawnStatus.Active => null,
                _ => WorldBossErrorCodes.NotActive
            };
            if (inactiveError is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FromSpawn(
                    spawn,
                    succeeded: false,
                    inactiveError,
                    replayed: false,
                    appliedDamage: 0,
                    defeatedNow: false,
                    phaseChanged: false);
            }

            decimal appliedDamage = spawn.ApplyDamage(requestedDamage);
            if (appliedDamage > 0)
            {
                WorldBossContribution? personal = await db.WorldBossContributions.SingleOrDefaultAsync(
                    contribution => contribution.SpawnId == spawnId
                        && contribution.CharacterId == characterId,
                    cancellationToken);
                if (personal is null)
                {
                    personal = new WorldBossContribution(spawnId, characterId, now);
                    db.WorldBossContributions.Add(personal);
                }

                personal.AddDamage(appliedDamage, now);

                if (partyId is Guid actualPartyId)
                {
                    WorldBossPartyContribution? party = await db.WorldBossPartyContributions
                        .SingleOrDefaultAsync(
                            contribution => contribution.SpawnId == spawnId
                                && contribution.PartyId == actualPartyId,
                            cancellationToken);
                    if (party is null)
                    {
                        party = new WorldBossPartyContribution(spawnId, actualPartyId);
                        db.WorldBossPartyContributions.Add(party);
                    }

                    party.AddDamage(appliedDamage);
                }
            }

            bool phaseChanged = false;
            if (appliedDamage > 0 && spawn.CurrentHealth > 0 && contentProvider is not null)
            {
                GameContentSnapshot content = contentProvider.GetCurrent();
                if (content.Indexes.WorldBossesById.TryGetValue(
                        spawn.BossDefinitionId,
                        out WorldBossDefinition? definition))
                {
                    int resolvedPhase = WorldBossPhasePolicy.ResolvePhase(
                        definition,
                        spawn.CurrentHealth,
                        spawn.MaxHealth);
                    phaseChanged = spawn.TryChangePhase(resolvedPhase);
                }
            }

            bool defeatedNow = spawn.CurrentHealth == 0 && spawn.TryMarkDefeated(now);
            db.WorldBossDamageMutations.Add(new WorldBossDamageMutation(
                spawnId,
                mutationId,
                characterId,
                combatSessionId,
                partyId,
                requestedDamage,
                appliedDamage,
                now));

            await db.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None);

            return FromSpawn(
                spawn,
                succeeded: true,
                errorCode: null,
                replayed: false,
                appliedDamage,
                defeatedNow,
                phaseChanged);
        });

        if (result.DefeatedNow && logger is not null)
        {
            BossDefeated(
                logger,
                spawnId,
                characterId,
                combatSessionId,
                result.AppliedDamage,
                null);
        }

        if (result.DefeatedNow && updatePublisher is not null)
            await PublishDefeatedSafelyAsync(spawnId, cancellationToken);

        return result;
    }

    private async Task PublishDefeatedSafelyAsync(
        Guid spawnId,
        CancellationToken cancellationToken)
    {
        try
        {
            DateTimeOffset? defeatedAtUtc = await db.WorldBossSpawns
                .AsNoTracking()
                .Where(spawn => spawn.Id == spawnId)
                .Select(spawn => spawn.DefeatedAtUtc)
                .SingleOrDefaultAsync(cancellationToken);
            if (defeatedAtUtc is null)
                return;

            Guid[] participantAccountIds = await (
                from contribution in db.WorldBossContributions.AsNoTracking()
                join character in db.Characters.AsNoTracking()
                    on contribution.CharacterId equals character.Id
                where contribution.SpawnId == spawnId
                select character.AccountId)
                .Distinct()
                .ToArrayAsync(cancellationToken);

            await updatePublisher!.PublishDefeatedAsync(
                spawnId,
                defeatedAtUtc.Value,
                participantAccountIds,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Durable defeat already committed; authoritative reads recover missed realtime.
        }
        catch (Exception exception)
        {
            if (logger is not null)
                RealtimeDefeatDeliveryFailed(logger, spawnId, exception);
        }
    }

    private static void Validate(
        Guid spawnId,
        Guid characterId,
        Guid combatSessionId,
        Guid? partyId,
        decimal requestedDamage,
        Guid mutationId)
    {
        if (spawnId == Guid.Empty
            || characterId == Guid.Empty
            || combatSessionId == Guid.Empty
            || mutationId == Guid.Empty)
        {
            throw new ArgumentException("World boss damage identifiers cannot be empty.");
        }

        if (partyId == Guid.Empty)
            throw new ArgumentException("World boss party identifier cannot be empty.", nameof(partyId));

        ArgumentOutOfRangeException.ThrowIfNegative(requestedDamage);
    }

    private static bool Matches(
        WorldBossDamageMutation mutation,
        Guid characterId,
        Guid combatSessionId,
        Guid? partyId,
        decimal requestedDamage) =>
        mutation.CharacterId == characterId
        && mutation.CombatSessionId == combatSessionId
        && mutation.PartyId == partyId
        && mutation.RequestedDamage == requestedDamage;

    private static WorldBossDamageCommitResult Missing() =>
        new(
            Succeeded: false,
            WorldBossErrorCodes.SpawnNotFound,
            Replayed: false,
            AppliedDamage: 0,
            CurrentHealth: 0,
            MaxHealth: 0,
            Phase: 0,
            Status: null,
            DefeatedNow: false,
            PhaseChanged: false);

    private static WorldBossDamageCommitResult FromSpawn(
        WorldBossSpawn spawn,
        bool succeeded,
        string? errorCode,
        bool replayed,
        decimal appliedDamage,
        bool defeatedNow,
        bool phaseChanged) =>
        new(
            succeeded,
            errorCode,
            replayed,
            appliedDamage,
            spawn.CurrentHealth,
            spawn.MaxHealth,
            spawn.CurrentPhase,
            spawn.Status,
            defeatedNow,
            phaseChanged);
}
