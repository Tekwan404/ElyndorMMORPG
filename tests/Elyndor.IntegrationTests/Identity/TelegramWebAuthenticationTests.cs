using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Elyndor.Contracts.Identity;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Elyndor.IntegrationTests.Identity;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class TelegramWebAuthenticationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string BotToken = "123456:TEST_TOKEN";
    private const string SigningKey =
        "telegram-web-integration-test-signing-key-with-more-than-32-bytes";
    private const string ClientId = "123456789";
    private const string ClientSecret = "telegram-web-integration-test-client-secret";
    private const string RedirectUri = "https://elyndor.test/world";
    private const string ValidInitData =
        "auth_date=1788048000&query_id=AAEAAAE&user=%7B%22id%22%3A42%2C%22first_name%22%3A%22Test%22%7D"
        + "&hash=b56cf8f51cc2cb391171b7dbbcac72e8f2aee00d6a7a284abdb3ee9caf016a0a";

    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 0, 2, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TelegramWebConfigurationIsDisabledByDefault()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        TelegramWebAuthenticationConfigResponse? response =
            await client.GetFromJsonAsync<TelegramWebAuthenticationConfigResponse>(
                "/api/v1/auth/telegram-web/config");

        Assert.NotNull(response);
        Assert.False(response.Enabled);
        Assert.Null(response.ClientId);
        Assert.Null(response.RedirectUri);
    }

    [Fact]
    public async Task TelegramWebConfigurationExposesOnlyPublicConfigurationWhenEnabled()
    {
        await using WebApplicationFactory<Program> factory =
            CreateFactory(webAuthenticationEnabled: true);
        using HttpClient client = factory.CreateClient();

        TelegramWebAuthenticationConfigResponse? response =
            await client.GetFromJsonAsync<TelegramWebAuthenticationConfigResponse>(
                "/api/v1/auth/telegram-web/config");

        Assert.NotNull(response);
        Assert.True(response.Enabled);
        Assert.Equal(ClientId, response.ClientId);
        Assert.Equal(RedirectUri, response.RedirectUri);
    }

    [Fact]
    public async Task TelegramWebAuthenticationRejectsInvalidPkceBeforeProviderCall()
    {
        await using WebApplicationFactory<Program> factory =
            CreateFactory(webAuthenticationEnabled: true);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/telegram-web",
            new TelegramWebAuthenticationRequest("code", "too-short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ApiErrorResponse? error =
            await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("telegram_web_request_invalid", error.Code);
    }

    [Fact]
    public async Task TelegramWebCredentialResolvesSameAccountAsMiniAppIdentity()
    {
        await using WebApplicationFactory<Program> factory =
            CreateFactory(webAuthenticationEnabled: true);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage miniAppResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/telegram",
            new TelegramAuthenticationRequest(ValidInitData));
        miniAppResponse.EnsureSuccessStatusCode();
        AuthenticationResponse? miniAppAuthentication =
            await miniAppResponse.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(miniAppAuthentication);

        AuthenticationOptions options = CreateAuthenticationOptions();
        IssuedTelegramWebCredential credential =
            TelegramWebAuthenticationService.IssueCredential(
                options,
                new FixedTimeProvider(Now),
                new TelegramWebIdentity(42, "same_player"));

        HttpResponseMessage webResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/telegram",
            new TelegramAuthenticationRequest(credential.Value));
        webResponse.EnsureSuccessStatusCode();
        AuthenticationResponse? webAuthentication =
            await webResponse.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(webAuthentication);

        Assert.Equal(
            ReadSubject(miniAppAuthentication.AccessToken),
            ReadSubject(webAuthentication.AccessToken));

        await using GameDbContext context = postgres.CreateDbContext();
        Account account = await context.Accounts.SingleAsync();
        Assert.Equal(42, account.TelegramUserId);
        Assert.Equal("same_player", account.TelegramUsername);
    }

    [Fact]
    public async Task DisabledTelegramWebAuthenticationRejectsPreviouslyIssuedCredential()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        AuthenticationOptions options = CreateAuthenticationOptions();
        IssuedTelegramWebCredential credential =
            TelegramWebAuthenticationService.IssueCredential(
                options,
                new FixedTimeProvider(Now),
                new TelegramWebIdentity(42, null));

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/telegram",
            new TelegramAuthenticationRequest(credential.Value));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        ApiErrorResponse? error =
            await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("telegram_web_credential_invalid", error?.Code);
    }

    [Fact]
    public void EnabledTelegramWebAuthenticationWithoutSecretFailsStartup()
    {
        using WebApplicationFactory<Program> factory = CreateFactory(
            webAuthenticationEnabled: true,
            webClientSecret: string.Empty);

        Assert.Throws<OptionsValidationException>(factory.CreateClient);
    }

    private WebApplicationFactory<Program> CreateFactory(
        bool webAuthenticationEnabled = false,
        string webClientSecret = ClientSecret) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("PublicTest");
                builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
                builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
                builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
                builder.UseSetting("Authentication:SigningKey", SigningKey);
                builder.UseSetting("Authentication:Telegram:BotToken", BotToken);
                builder.UseSetting("Authentication:Telegram:InitDataMaxAgeSeconds", "300");
                builder.UseSetting("Authentication:Telegram:MaxFutureSkewSeconds", "30");
                builder.UseSetting(
                    "Authentication:Telegram:Web:Enabled",
                    webAuthenticationEnabled.ToString());
                builder.UseSetting("Authentication:Telegram:Web:ClientId", ClientId);
                builder.UseSetting(
                    "Authentication:Telegram:Web:ClientSecret",
                    webClientSecret);
                builder.UseSetting(
                    "Authentication:Telegram:Web:RedirectUri",
                    RedirectUri);
                builder.UseSetting(
                    "Authentication:Telegram:Web:SessionLifetimeHours",
                    "12");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
                });
            });

    private static AuthenticationOptions CreateAuthenticationOptions() =>
        new()
        {
            Issuer = "Elyndor.Tests",
            Audience = "Elyndor.Tests.Client",
            SigningKey = SigningKey,
            Telegram = new TelegramAuthenticationOptions
            {
                BotToken = BotToken,
                InitDataMaxAgeSeconds = 300,
                MaxFutureSkewSeconds = 30,
                Web = new TelegramWebAuthenticationOptions
                {
                    Enabled = true,
                    ClientId = ClientId,
                    ClientSecret = ClientSecret,
                    RedirectUri = RedirectUri,
                    SessionLifetimeHours = 12
                }
            }
        };

    private static string ReadSubject(string token)
    {
        string payload = token.Split('.')[1]
            .Replace('-', '+')
            .Replace('_', '/');
        payload = payload.PadRight(
            payload.Length + ((4 - (payload.Length % 4)) % 4),
            '=');
        using JsonDocument document = JsonDocument.Parse(
            Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        return document.RootElement.GetProperty("sub").GetString()!;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
