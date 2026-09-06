using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Elyndor.Contracts.Identity;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Identity.Telegram;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Administration;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Elyndor.IntegrationTests.Identity;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class AuthenticationEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string BotToken = "123456:TEST_TOKEN";
    private const string SigningKey =
        "phase-one-integration-test-signing-key-with-more-than-32-bytes";
    private const string ValidInitData =
        "auth_date=1788048000&query_id=AAEAAAE&user=%7B%22id%22%3A42%2C%22first_name%22%3A%22Test%22%7D"
        + "&hash=b56cf8f51cc2cb391171b7dbbcac72e8f2aee00d6a7a284abdb3ee9caf016a0a";

    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 0, 2, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TelegramAuthenticationIssuesFifteenMinuteAccountToken()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory("PublicTest");
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/telegram",
            new TelegramAuthenticationRequest(ValidInitData));

        response.EnsureSuccessStatusCode();
        AuthenticationResponse? authentication =
            await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

        Assert.NotNull(authentication);
        Assert.Empty(authentication.Roles);
        Assert.Equal(Now.AddMinutes(15), authentication.ExpiresAtUtc);
        Guid accountId = Guid.Parse(ReadSubject(authentication.AccessToken));
        Assert.Equal(
            authentication.ExpiresAtUtc,
            ReadExpiration(authentication.AccessToken));

        await using GameDbContext context = postgres.CreateDbContext();
        Account account = await context.Accounts.SingleAsync();
        Assert.Equal(account.Id, accountId);
        Assert.Equal(42, account.TelegramUserId);
    }

    [Fact]
    public async Task TelegramAdminReceivesRoleInResponseAndJwt()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(
            "PublicTest",
            adminAllowedUserId: 42);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/telegram",
            new TelegramAuthenticationRequest(ValidInitData));

        response.EnsureSuccessStatusCode();
        AuthenticationResponse? authentication =
            await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

        Assert.NotNull(authentication);
        Assert.Equal([AdminAuthorization.SuperAdminRole], authentication.Roles);

        using JsonDocument payload = ReadPayload(authentication.AccessToken);
        Assert.Equal(
            AdminAuthorization.SuperAdminRole,
            payload.RootElement
                .GetProperty(AuthenticationClaimTypes.Role)
                .GetString());
        Assert.Equal(
            "42",
            payload.RootElement
                .GetProperty(AuthenticationClaimTypes.TelegramUserId)
                .GetString());
    }

    [Fact]
    public async Task TelegramAuthenticationRejectsInvalidHashWithStableCode()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory("PublicTest");
        using HttpClient client = factory.CreateClient();
        string invalidInitData = ValidInitData.Replace(
            "b56cf8",
            "a56cf8",
            StringComparison.Ordinal);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/telegram",
            new TelegramAuthenticationRequest(invalidInitData));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        ApiErrorResponse? error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(TelegramInitDataValidationErrorCodes.HashInvalid, error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.CorrelationId));
    }

    [Fact]
    public async Task TelegramAuthenticationRejectsExpiredInitData()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(
            "PublicTest",
            Now.AddMinutes(4));
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/telegram",
            new TelegramAuthenticationRequest(ValidInitData));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        ApiErrorResponse? error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal(TelegramInitDataValidationErrorCodes.Expired, error?.Code);
    }

    [Fact]
    public async Task AuthenticationRateLimitReturnsStableProblemAndRetryAfter()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory("PublicTest");
        using HttpClient client = factory.CreateClient();
        string invalidInitData = ValidInitData.Replace(
            "b56cf8",
            "a56cf8",
            StringComparison.Ordinal);

        HttpResponseMessage? response = null;
        for (int attempt = 0; attempt < 21; attempt++)
        {
            response?.Dispose();
            response = await client.PostAsJsonAsync(
                "/api/v1/auth/telegram",
                new TelegramAuthenticationRequest(invalidInitData));
        }

        using (response)
        {
            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values));
            Assert.NotEmpty(values);

            ApiErrorResponse? error =
                await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
            Assert.NotNull(error);
            Assert.Equal("rate_limited", error.Code);
            Assert.False(string.IsNullOrWhiteSpace(error.CorrelationId));
        }
    }

    [Fact]
    public void MissingBotTokenFailsApplicationStartup()
    {
        using WebApplicationFactory<Program> factory = CreateFactory(
            "PublicTest",
            botToken: string.Empty);

        Assert.Throws<OptionsValidationException>(factory.CreateClient);
    }

    [Fact]
    public async Task DevelopmentAuthenticationUsesOnlyConfiguredIdentity()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(
            "Development",
            developmentEnabled: true,
            developmentTelegramUserId: 777);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/development",
            new { telegramUserId = 999 });

        response.EnsureSuccessStatusCode();

        await using GameDbContext context = postgres.CreateDbContext();
        Account account = await context.Accounts.SingleAsync();
        Assert.Equal(777, account.TelegramUserId);
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("PublicTest", true)]
    public async Task DevelopmentAuthenticationIsNotMappedOutsideExplicitBoundary(
        string environment,
        bool developmentEnabled)
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(
            environment,
            developmentEnabled: developmentEnabled,
            developmentTelegramUserId: 777);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/development",
            new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory(
        string environment,
        DateTimeOffset? utcNow = null,
        string? botToken = BotToken,
        bool developmentEnabled = false,
        long developmentTelegramUserId = 0,
        long adminAllowedUserId = 0) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
                builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
                builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
                builder.UseSetting("Authentication:SigningKey", SigningKey);
                builder.UseSetting("Authentication:Telegram:BotToken", botToken);
                builder.UseSetting("Authentication:Telegram:InitDataMaxAgeSeconds", "300");
                builder.UseSetting("Authentication:Telegram:MaxFutureSkewSeconds", "30");
                builder.UseSetting(
                    "Authentication:Development:Enabled",
                    developmentEnabled.ToString());
                builder.UseSetting(
                    "Authentication:Development:TelegramUserId",
                    developmentTelegramUserId.ToString(
                        global::System.Globalization.CultureInfo.InvariantCulture));
                if (adminAllowedUserId > 0)
                {
                    builder.UseSetting(
                        "Administration:Telegram:AllowedUserIds:0",
                        adminAllowedUserId.ToString(
                            global::System.Globalization.CultureInfo.InvariantCulture));
                }

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(
                        new FixedTimeProvider(utcNow ?? Now));
                });
            });

    private static string ReadSubject(string token)
    {
        using JsonDocument document = ReadPayload(token);
        return document.RootElement.GetProperty("sub").GetString()!;
    }

    private static DateTimeOffset ReadExpiration(string token)
    {
        using JsonDocument document = ReadPayload(token);
        long seconds = document.RootElement.GetProperty("exp").GetInt64();
        return DateTimeOffset.FromUnixTimeSeconds(seconds);
    }

    private static JsonDocument ReadPayload(string token)
    {
        string payload = token.Split('.')[1]
            .Replace('-', '+')
            .Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        return JsonDocument.Parse(
            Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
