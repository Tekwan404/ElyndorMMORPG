using Microsoft.Extensions.Options;

namespace Elyndor.Server.Administration;

public sealed class TelegramWebhookRegistrationWorker(
    TelegramWebhookRegistrationService registrationService,
    IOptions<TelegramAdminOptions> adminOptions,
    IHostEnvironment environment,
    ILogger<TelegramWebhookRegistrationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TelegramAdminOptions options = adminOptions.Value;
        if (!environment.IsProduction()
            || !options.Enabled
            || options.UseLongPolling
            || !options.RegisterWebhookOnStartup)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await registrationService.RegisterAsync(stoppingToken);
                TelegramWebhookLogMessages.Registered(logger);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                TelegramWebhookLogMessages.RegistrationFailed(logger, exception);
            }

            await Task.Delay(RetryDelay, stoppingToken);
        }
    }
}

internal static class TelegramWebhookLogMessages
{
    private static readonly Action<ILogger, Exception?> WebhookRegistered =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1101, nameof(WebhookRegistered)),
            "Telegram admin webhook registration completed.");

    private static readonly Action<ILogger, Exception?> WebhookRegistrationFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1102, nameof(WebhookRegistrationFailed)),
            "Telegram admin webhook registration failed; retrying in one minute.");

    public static void Registered(ILogger logger) =>
        WebhookRegistered(logger, null);

    public static void RegistrationFailed(ILogger logger, Exception exception) =>
        WebhookRegistrationFailed(logger, exception);
}
