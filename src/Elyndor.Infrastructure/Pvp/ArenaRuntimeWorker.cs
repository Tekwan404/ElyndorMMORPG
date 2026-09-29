using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.Pvp;

/// <summary>Ticks live arena matches, settles finished ones and pushes update notifications.</summary>
public sealed partial class ArenaRuntimeWorker(
    ArenaMatchRuntime runtime,
    IServiceScopeFactory scopeFactory,
    IArenaUpdatePublisher publisher,
    ILogger<ArenaRuntimeWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ArenaTickResult tick = runtime.Tick();
                foreach (ArenaFinishedMatch finished in tick.Finished)
                    await FinalizeAsync(finished, stoppingToken);
                foreach (ArenaMatchUpdate update in tick.Updates)
                    await NotifyAsync(update.AccountId, update.MatchId, update.Sequence, stoppingToken);
                runtime.PurgeFinalized();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogIterationFailed(logger, exception);
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task FinalizeAsync(ArenaFinishedMatch finished, CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            var settlement = scope.ServiceProvider.GetRequiredService<ArenaSettlementService>();
            await settlement.CompleteAndSettleAsync(finished.MatchId, finished.Outcome, null, cancellationToken);
            runtime.MarkFinalized(finished.MatchId);
        }
        catch (InvalidOperationException exception)
        {
            // Unsettleable (e.g. participant deleted): stop retrying, never guess a result.
            LogUnsettleable(logger, finished.MatchId, exception);
            runtime.MarkFinalized(finished.MatchId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Transient (DB) failure: the match stays unfinalized and is retried next tick.
            LogSettlementRetry(logger, finished.MatchId, exception);
            return;
        }

        // Balances are committed now; tell both players so the UI can refresh rating/Honor.
        await NotifyAsync(finished.FirstAccountId, finished.MatchId, finished.Sequence, cancellationToken);
        await NotifyAsync(finished.SecondAccountId, finished.MatchId, finished.Sequence, cancellationToken);
    }

    private async Task NotifyAsync(Guid accountId, Guid matchId, long sequence, CancellationToken cancellationToken)
    {
        try
        {
            await publisher.PublishMatchUpdatedAsync(accountId, matchId, sequence, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogNotificationFailed(logger, matchId, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Arena runtime iteration failed.")]
    private static partial void LogIterationFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Arena match {MatchId} cannot be settled.")]
    private static partial void LogUnsettleable(ILogger logger, Guid matchId, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Arena match {MatchId} settlement failed; will retry.")]
    private static partial void LogSettlementRetry(ILogger logger, Guid matchId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Arena update notification failed for match {MatchId}.")]
    private static partial void LogNotificationFailed(ILogger logger, Guid matchId, Exception exception);
}
