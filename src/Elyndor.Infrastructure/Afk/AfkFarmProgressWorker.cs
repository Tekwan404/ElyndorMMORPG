using Elyndor.Core.Afk;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.Afk;

public sealed class AfkFarmProgressWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AfkFarmProgressWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private static readonly Action<ILogger, Exception?> ProgressPassFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2501, nameof(ProgressPassFailed)),
            "AFK farm progress pass failed.");

    private static readonly Action<ILogger, Guid, Exception?> AccountProgressFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2502, nameof(AccountProgressFailed)),
            "AFK farm progress failed for account {AccountId}.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessActiveSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                ProgressPassFailed(logger, exception);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessActiveSessionsAsync(CancellationToken cancellationToken)
    {
        Guid[] accountIds;
        await using (AsyncServiceScope discoveryScope = scopeFactory.CreateAsyncScope())
        {
            GameDbContext dbContext = discoveryScope.ServiceProvider.GetRequiredService<GameDbContext>();
            accountIds = await (
                    from session in dbContext.AfkFarmSessions.AsNoTracking()
                    join character in dbContext.Characters.AsNoTracking()
                        on session.CharacterId equals character.Id
                    where session.Status == AfkFarmStatus.Active
                    select character.AccountId)
                .Distinct()
                .ToArrayAsync(cancellationToken);
        }

        foreach (Guid accountId in accountIds)
        {
            try
            {
                await using AsyncServiceScope progressScope = scopeFactory.CreateAsyncScope();
                AfkFarmProgressService progress = progressScope.ServiceProvider
                    .GetRequiredService<AfkFarmProgressService>();
                await progress.ProcessAsync(accountId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                AccountProgressFailed(logger, accountId, exception);
            }
        }
    }
}
