using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elyndor.Server.Administration;
using Elyndor.Server.Identity;
using Microsoft.Extensions.Options;

namespace Elyndor.IntegrationTests.Administration;

public sealed class TelegramWebhookRegistrationServiceTests
{
    [Fact]
    public async Task RegistersOnlyMessageUpdatesAgainstProductionWebhook()
    {
        RecordingHandler handler = new();
        using HttpClient httpClient = new(handler);
        TelegramWebhookRegistrationService service = CreateService(httpClient);

        await service.RegisterAsync(CancellationToken.None);

        Assert.NotNull(handler.RequestUri);
        Assert.Equal(
            "https://api.telegram.org/bot123:test-token/setWebhook",
            handler.RequestUri!.AbsoluteUri);

        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        JsonElement root = body.RootElement;
        Assert.Equal(
            TelegramAdminOptions.DefaultWebhookUrl,
            root.GetProperty("url").GetString());
        Assert.Equal(
            "test-webhook-secret-which-is-long-enough",
            root.GetProperty("secret_token").GetString());
        Assert.True(root.GetProperty("drop_pending_updates").GetBoolean());

        JsonElement.ArrayEnumerator allowedUpdates =
            root.GetProperty("allowed_updates").EnumerateArray();
        Assert.True(allowedUpdates.MoveNext());
        Assert.Equal("message", allowedUpdates.Current.GetString());
        Assert.False(allowedUpdates.MoveNext());
    }

    [Fact]
    public async Task DisabledRegistrationDoesNotCallTelegram()
    {
        RecordingHandler handler = new();
        using HttpClient httpClient = new(handler);
        TelegramWebhookRegistrationService service = CreateService(
            httpClient,
            registerWebhookOnStartup: false);

        await service.RegisterAsync(CancellationToken.None);

        Assert.Null(handler.RequestUri);
    }

    private static TelegramWebhookRegistrationService CreateService(
        HttpClient httpClient,
        bool registerWebhookOnStartup = true) =>
        new(
            httpClient,
            Options.Create(new AuthenticationOptions
            {
                Telegram = new TelegramAuthenticationOptions
                {
                    BotToken = "123:test-token"
                }
            }),
            Options.Create(new TelegramAdminOptions
            {
                Enabled = true,
                WebhookSecret = "test-webhook-secret-which-is-long-enough",
                RegisterWebhookOnStartup = registerWebhookOnStartup,
                AllowedUserIds = [42]
            }));

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { ok = true })
            };
        }
    }
}
