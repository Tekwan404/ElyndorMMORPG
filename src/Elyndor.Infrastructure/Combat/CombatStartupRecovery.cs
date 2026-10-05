using Elyndor.Infrastructure.Dungeons;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.Combat;

public static class CombatStartupRecovery
{
    private static readonly Action<ILogger, int, Exception?> RecoveryAttemptFailed =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(2103, nameof(RecoveryAttemptFailed)),
            "Combat startup recovery attempt {Attempt} failed; durable evidence remains pending for retry.");

    public static async Task RecoverAsync(
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        const int maximumAttempts = 3;
        for (int attempt = 1; ; attempt++)
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            EnsureBeforeAdmission(scope.ServiceProvider.GetService<IHostApplicationLifetime>());
            try
            {
                CombatDurabilityService durability = scope.ServiceProvider.GetRequiredService<CombatDurabilityService>();
                await durability.RecoverInterruptedAsync(cancellationToken);
                DungeonService dungeons = scope.ServiceProvider.GetRequiredService<DungeonService>();
                await dungeons.ReconcileOrphanedEncountersAtStartupAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt < maximumAttempts && exception is not OperationCanceledException)
            {
                ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Elyndor.CombatStartupRecovery");
                RecoveryAttemptFailed(logger, attempt, exception);
                // A new scope on the next attempt avoids reusing failed tracked database state.
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    internal static void EnsureBeforeAdmission(IHostApplicationLifetime? applicationLifetime)
    {
        if (applicationLifetime?.ApplicationStarted.IsCancellationRequested == true)
            throw new InvalidOperationException("Interrupted combat recovery is only allowed before the host admits players.");
    }
}
