using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elyndor.Infrastructure.WorldBosses;

public sealed class WorldBossCombatSessionSynchronizer(IServiceScopeFactory scopeFactory)
    : ICombatSessionSynchronizer
{
    public async Task<bool> SynchronizeAsync(
        CombatSession session,
        GameContentSnapshot? contentSnapshot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        GameDbContext db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var state = await (
            from binding in db.WorldBossCombatSessions.AsNoTracking()
            join spawn in db.WorldBossSpawns.AsNoTracking()
                on binding.SpawnId equals spawn.Id
            where binding.CombatSessionId == session.SessionId
            select new
            {
                spawn.CurrentHealth,
                spawn.MaxHealth,
                spawn.Status,
                spawn.ExpiresAtUtc,
                spawn.ContentVersion,
                spawn.BalanceVersion
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (state is null)
            return false;

        if (contentSnapshot is not null
            && (!string.Equals(
                    state.ContentVersion,
                    contentSnapshot.ContentVersion,
                    StringComparison.Ordinal)
                || !string.Equals(
                    state.BalanceVersion,
                    contentSnapshot.BalanceVersion,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "World boss spawn content identity does not match the pinned combat snapshot.");
        }

        if (state.MaxHealth != session.PrimaryEnemyMaxHp)
        {
            throw new InvalidOperationException(
                "World boss global max health does not match the bound combat session.");
        }

        bool expired = state.Status == WorldBossSpawnStatus.Expired
            || state.Status == WorldBossSpawnStatus.Active && now >= state.ExpiresAtUtc;
        decimal currentHealth = state.Status is WorldBossSpawnStatus.Defeated
                or WorldBossSpawnStatus.Settling
                or WorldBossSpawnStatus.Settled
            ? 0m
            : state.CurrentHealth;

        return session.SynchronizePrimaryEnemyFromAuthority(
            currentHealth,
            expired,
            now);
    }
}
