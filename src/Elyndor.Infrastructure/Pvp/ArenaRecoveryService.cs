using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.Pvp;

/// <summary>
/// Combat runtime is in-memory, so after a process start every Active match row is orphaned.
/// Cancel them without rating/Honor changes so characters are not locked out of the queue.
/// </summary>
public sealed partial class ArenaRecoveryService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ArenaRecoveryService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        ArenaMatch[] orphaned = await db.ArenaMatches
            .Where(x => x.Outcome == ArenaMatchOutcome.Active)
            .ToArrayAsync(cancellationToken);
        if (orphaned.Length == 0) return;

        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (ArenaMatch match in orphaned)
        {
            match.Complete(ArenaMatchOutcome.Cancelled, now, eligibleForProgression: false);
            match.MarkSettled(now);
        }
        await db.SaveChangesAsync(cancellationToken);
        LogOrphanedMatches(logger, orphaned.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cancelled {Count} orphaned arena matches after restart.")]
    private static partial void LogOrphanedMatches(ILogger logger, int count);
}
