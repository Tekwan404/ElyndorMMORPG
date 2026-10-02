using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
    bool DefeatedNow);

public sealed class WorldBossDamageService(GameDbContext db, TimeProvider time)
{
    public Task<WorldBossDamageCommitResult> ApplyDamageAsync(
        Guid spawnId,
        Guid characterId,
        Guid combatSessionId,
        Guid? partyId,
        decimal requestedDamage,
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        Validate(spawnId, characterId, combatSessionId, partyId, requestedDamage, mutationId);

        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
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
                        defeatedNow: false);
                }

                await transaction.RollbackAsync(CancellationToken.None);
                return FromSpawn(
                    spawn,
                    succeeded: true,
                    errorCode: null,
                    replayed: true,
                    existing.AppliedDamage,
                    defeatedNow: false);
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
                    defeatedNow: false);
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
                    defeatedNow: false);
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
                defeatedNow);
        });
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
            DefeatedNow: false);

    private static WorldBossDamageCommitResult FromSpawn(
        WorldBossSpawn spawn,
        bool succeeded,
        string? errorCode,
        bool replayed,
        decimal appliedDamage,
        bool defeatedNow) =>
        new(
            succeeded,
            errorCode,
            replayed,
            appliedDamage,
            spawn.CurrentHealth,
            spawn.MaxHealth,
            spawn.CurrentPhase,
            spawn.Status,
            defeatedNow);
}
