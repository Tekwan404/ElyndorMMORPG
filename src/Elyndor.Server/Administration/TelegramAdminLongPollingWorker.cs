using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Elyndor.Server.Identity;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Administration;

public sealed class TelegramAdminLongPollingWorker(
    HttpClient httpClient,
    IOptions<AuthenticationOptions> authenticationOptions,
    IOptions<TelegramAdminOptions> adminOptions,
    TelegramAdminUpdateProcessor processor,
    IHostEnvironment environment,
    ILogger<TelegramAdminLongPollingWorker> logger) : BackgroundService
{
    private static readonly string[] AllowedUpdates = ["message"];
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private const int PollTimeoutSeconds = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TelegramAdminOptions options = adminOptions.Value;
        if (!environment.IsProduction() || !options.Enabled || !options.UseLongPolling)
            return;

        string token = authenticationOptions.Value.Telegram.BotToken;
        bool webhookRemoved = false;
        long offset = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!webhookRemoved)
                {
                    await DeleteWebhookAsync(token, stoppingToken);
                    webhookRemoved = true;
                    TelegramPollingLogMessages.Started(logger);
                }

                TelegramGetUpdatesResponse response =
                    await GetUpdatesAsync(token, offset, stoppingToken);
                if (!response.Ok)
                    throw new HttpRequestException("Telegram rejected getUpdates.");

                foreach (TelegramUpdate update in response.Result.OrderBy(item => item.UpdateId))
                {
                    await processor.ProcessAsync(update, stoppingToken);
                    offset = Math.Max(offset, update.UpdateId + 1);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                TelegramPollingLogMessages.PollFailed(logger, exception);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task DeleteWebhookAsync(
        string token,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/deleteWebhook",
            new { drop_pending_updates = true },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        TelegramBotApiResponse? result =
            await response.Content.ReadFromJsonAsync<TelegramBotApiResponse>(
                cancellationToken: cancellationToken);
        if (result is not { Ok: true })
            throw new HttpRequestException("Telegram rejected deleteWebhook.");
    }

    private async Task<TelegramGetUpdatesResponse> GetUpdatesAsync(
        string token,
        long offset,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/getUpdates",
            new
            {
                offset,
                timeout = PollTimeoutSeconds,
                allowed_updates = AllowedUpdates
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TelegramGetUpdatesResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new HttpRequestException("Telegram returned an empty getUpdates response.");
    }

    private sealed record TelegramBotApiResponse(
        [property: JsonPropertyName("ok")] bool Ok);

    private sealed record TelegramGetUpdatesResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] TelegramUpdate[] Result);
}

internal static class TelegramPollingLogMessages
{
    private static readonly Action<ILogger, Exception?> PollingStarted =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1111, nameof(PollingStarted)),
            "Telegram admin long polling started; webhook removed.");

    private static readonly Action<ILogger, Exception?> PollingFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(1113, nameof(PollingFailed)),
            "Telegram admin long polling failed; retrying in five seconds.");

    public static void Started(ILogger logger) =>
        PollingStarted(logger, null);

    public static void PollFailed(ILogger logger, Exception exception) =>
        PollingFailed(logger, exception);
}
