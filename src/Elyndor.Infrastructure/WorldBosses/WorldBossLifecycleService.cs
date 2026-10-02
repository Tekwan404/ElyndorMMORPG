using Elyndor.Core.Content;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.WorldBosses;

public static class WorldBossLifecycleErrorCodes
{
    public const string DefinitionNotFound = "world_boss_definition_not_found";
    public const string DefinitionDisabled = "world_boss_definition_disabled";
}

public sealed record WorldBossActivationResult(
    bool Succeeded,
    string? ErrorCode,
    WorldBossSpawn? Spawn,
    bool Created);

public sealed class WorldBossLifecycleService(
    GameDbContext db,
    IContentSnapshotProvider contentProvider,
    TimeProvider time)
{
    private const long LifecycleAdvisoryLockId = 5_723_901_441L;

    public Task<WorldBossActivationResult> ActivateAsync(
        string bossDefinitionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bossDefinitionId);

        GameContentSnapshot content = contentProvider.GetCurrent();
        if (!content.Indexes.WorldBossesById.TryGetValue(
                bossDefinitionId,
                out WorldBossDefinition? definition))
        {
            return Task.FromResult(new WorldBossActivationResult(
                false,
                WorldBossLifecycleErrorCodes.DefinitionNotFound,
                null,
                false));
        }

        if (!definition.IsEnabled)
        {
            return Task.FromResult(new WorldBossActivationResult(
                false,
                WorldBossLifecycleErrorCodes.DefinitionDisabled,
                null,
                false));
        }

        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await AcquireLifecycleLockAsync(cancellationToken);

            DateTimeOffset now = time.GetUtcNow();
            WorldBossSpawn? active = await ActiveForUpdateAsync(cancellationToken);
            if (active is not null && now >= active.ExpiresAtUtc)
            {
                active.TryExpire(now);
                await db.SaveChangesAsync(cancellationToken);
                active = null;
            }

            if (active is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await transaction.CommitAsync(CancellationToken.None);
                return new WorldBossActivationResult(true, null, active, false);
            }

            WorldBossPhaseDefinition initialPhase = definition.Phases
                .OrderBy(phase => phase.Phase)
                .First();
            var spawn = new WorldBossSpawn(
                Guid.CreateVersion7(),
                definition.Id,
                definition.BaseMaxHealth,
                initialPhase.Phase,
                now,
                now.AddSeconds(definition.DurationSeconds),
                content.ContentVersion,
                content.BalanceVersion);
            db.WorldBossSpawns.Add(spawn);
            await db.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None);
            return new WorldBossActivationResult(true, null, spawn, true);
        });
    }

    public Task<bool> ExpireDueAsync(CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await AcquireLifecycleLockAsync(cancellationToken);

            WorldBossSpawn? active = await ActiveForUpdateAsync(cancellationToken);
            bool expired = active is not null
                && active.TryExpire(time.GetUtcNow());
            if (expired)
                await db.SaveChangesAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None);
            return expired;
        });

    private Task<int> AcquireLifecycleLockAsync(CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({LifecycleAdvisoryLockId})",
            cancellationToken);

    private Task<WorldBossSpawn?> ActiveForUpdateAsync(CancellationToken cancellationToken) =>
        db.WorldBossSpawns
            .FromSqlRaw(
                "SELECT * FROM game.world_boss_spawns WHERE \"Status\" = 'Active' FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
}
