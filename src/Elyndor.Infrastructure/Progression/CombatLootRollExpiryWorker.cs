using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.Progression;

public sealed class CombatLootRollExpiryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CombatLootRollExpiryWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> ExpiryPassFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2401, nameof(ExpiryPassFailed)),
            "Combat loot roll expiry pass failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                CombatLootRollService service = scope.ServiceProvider
                    .GetRequiredService<CombatLootRollService>();
                await service.ResolveExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                ExpiryPassFailed(logger, exception);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
