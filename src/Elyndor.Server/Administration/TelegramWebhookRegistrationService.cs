using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Elyndor.Server.Identity;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Administration;

public sealed class TelegramWebhookRegistrationService(
    HttpClient httpClient,
    IOptions<AuthenticationOptions> authenticationOptions,
    IOptions<TelegramAdminOptions> adminOptions)
{
    private static readonly string[] AllowedUpdates = ["message"];

    public async Task RegisterAsync(CancellationToken cancellationToken)
    {
        TelegramAdminOptions options = adminOptions.Value;
        if (!options.Enabled
            || options.UseLongPolling
            || !options.RegisterWebhookOnStartup
            || !options.TryGetWebhookUri(out Uri? webhookUri))
        {
            return;
        }

        string token = authenticationOptions.Value.Telegram.BotToken;
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{token}/setWebhook",
            new
            {
                url = webhookUri!.AbsoluteUri,
                secret_token = options.WebhookSecret,
                allowed_updates = AllowedUpdates,
                drop_pending_updates = true
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        TelegramBotApiResponse? result =
            await response.Content.ReadFromJsonAsync<TelegramBotApiResponse>(
                cancellationToken: cancellationToken);
        if (result is not { Ok: true })
        {
            throw new HttpRequestException("Telegram rejected webhook registration.");
        }
    }

    private sealed record TelegramBotApiResponse(
        [property: JsonPropertyName("ok")] bool Ok);
}
