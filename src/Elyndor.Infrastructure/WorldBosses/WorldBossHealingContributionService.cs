using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.WorldBosses;

public sealed record WorldBossHealingCommitResult(
    bool Succeeded,
    string? ErrorCode,
    bool Replayed,
    decimal AppliedHealing);

public sealed class WorldBossHealingContributionService(
    GameDbContext db,
    TimeProvider time)
{
    public async Task<WorldBossHealingCommitResult> ApplyHealingAsync(
        Guid spawnId,
        Guid characterId,
        Guid combatSessionId,
        Guid? partyId,
        decimal effectiveHealing,
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        Validate(
            spawnId,
            characterId,
            combatSessionId,
            partyId,
            effectiveHealing,
            mutationId);

        WorldBossHealingCommitResult result = await db.Database
            .CreateExecutionStrategy()
            .ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);

            WorldBossSpawn? spawn = await db.WorldBossSpawns
                .FromSqlInterpolated(
                    $"SELECT * FROM game.world_boss_spawns WHERE \"Id\" = {spawnId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (spawn is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new WorldBossHealingCommitResult(false, WorldBossErrorCodes.SpawnNotFound, false, 0);
            }

            WorldBossHealingMutation? existing =
                await db.WorldBossHealingMutations.FindAsync(
                    [spawnId, mutationId],
                    cancellationToken);
            if (existing is not null)
            {
                bool matches =
                    existing.CharacterId == characterId
                    && existing.CombatSessionId == combatSessionId
                    && existing.PartyId == partyId
                    && existing.EffectiveHealing == effectiveHealing;
                await transaction.RollbackAsync(CancellationToken.None);
                return matches
                    ? new WorldBossHealingCommitResult(
                        true,
                        null,
                        true,
                        existing.EffectiveHealing)
                    : new WorldBossHealingCommitResult(
                        false,
                        WorldBossErrorCodes.MutationConflict,
                        false,
                        0);
            }

            DateTimeOffset now = time.GetUtcNow();
            if (spawn.Status == WorldBossSpawnStatus.Active
                && now >= spawn.ExpiresAtUtc)
            {
                spawn.TryExpire(now);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(CancellationToken.None);
                return new WorldBossHealingCommitResult(false, WorldBossErrorCodes.Expired, false, 0);
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
                return new WorldBossHealingCommitResult(false, inactiveError, false, 0);
            }

            WorldBossContribution? personal =
                await db.WorldBossContributions.SingleOrDefaultAsync(
                    contribution => contribution.SpawnId == spawnId
                        && contribution.CharacterId == characterId,
                    cancellationToken);
            if (personal is null)
            {
                personal = new WorldBossContribution(spawnId, characterId, now);
                db.WorldBossContributions.Add(personal);
            }
            personal.AddHealing(effectiveHealing, now);

            if (partyId is Guid actualPartyId)
            {
                WorldBossPartyContribution? party =
                    await db.WorldBossPartyContributions.SingleOrDefaultAsync(
                        contribution => contribution.SpawnId == spawnId
                            && contribution.PartyId == actualPartyId,
                        cancellationToken);
                if (party is null)
                {
                    party = new WorldBossPartyContribution(spawnId, actualPartyId);
                    db.WorldBossPartyContributions.Add(party);
                }
                party.AddHealing(effectiveHealing);
            }

            db.WorldBossHealingMutations.Add(new WorldBossHealingMutation(
                spawnId,
                mutationId,
                characterId,
                combatSessionId,
                partyId,
                effectiveHealing,
                now));

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(CancellationToken.None);
            return new WorldBossHealingCommitResult(
                true,
                null,
                false,
                effectiveHealing);
        });

        return result;
    }

    private static void Validate(
        Guid spawnId,
        Guid characterId,
        Guid combatSessionId,
        Guid? partyId,
        decimal effectiveHealing,
        Guid mutationId)
    {
        if (spawnId == Guid.Empty
            || characterId == Guid.Empty
            || combatSessionId == Guid.Empty
            || mutationId == Guid.Empty)
        {
            throw new ArgumentException(
                "World boss healing identifiers cannot be empty.");
        }

        if (partyId == Guid.Empty)
        {
            throw new ArgumentException(
                "World boss party identifier cannot be empty.",
                nameof(partyId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effectiveHealing);
    }
}
