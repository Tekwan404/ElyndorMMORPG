using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.WorldBosses;

public sealed class WorldBossSettlementWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<WorldBossSettlementWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);
    private static readonly Action<ILogger, Guid, Exception?> SettlementFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(4101, nameof(SettlementFailed)),
            "World boss settlement recovery failed for spawn {SpawnId}.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        await RecoverAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RecoverAsync(stoppingToken);
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        GameDbContext db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        Guid[] spawnIds = await db.WorldBossSpawns
            .AsNoTracking()
            .Where(spawn => spawn.Status == WorldBossSpawnStatus.Defeated
                || spawn.Status == WorldBossSpawnStatus.Settling)
            .OrderBy(spawn => spawn.DefeatedAtUtc)
            .ThenBy(spawn => spawn.SpawnedAtUtc)
            .Select(spawn => spawn.Id)
            .Take(8)
            .ToArrayAsync(cancellationToken);

        WorldBossSettlementService settlement =
            scope.ServiceProvider.GetRequiredService<WorldBossSettlementService>();
        foreach (Guid spawnId in spawnIds)
        {
            try
            {
                WorldBossSettlementBatchResult result =
                    await settlement.SettleAsync(spawnId, cancellationToken);
                if (!result.Succeeded
                    && result.ErrorCode != WorldBossSettlementErrorCodes.NotDefeated)
                {
                    throw new InvalidOperationException(
                        $"World boss settlement failed with '{result.ErrorCode}'.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                SettlementFailed(logger, spawnId, exception);
            }
        }
    }
}
