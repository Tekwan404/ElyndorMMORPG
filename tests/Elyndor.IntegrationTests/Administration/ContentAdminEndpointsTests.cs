using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Elyndor.Contracts.Administration;
using Elyndor.Contracts.Identity;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elyndor.IntegrationTests.Administration;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ContentAdminEndpointsTests(PostgresFixture postgres)
    : IAsyncLifetime
{
    private const string SigningKey =
        "content-admin-integration-test-signing-key-with-more-than-32-bytes";
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 20, 20, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ContentAdminRequiresAuthentication()
    {
        await using WebApplicationFactory<Program> factory =
            CreateFactory(777, adminAllowedUserId: 777);
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response =
            await client.GetAsync("/api/v1/admin/content/current");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedNonAdminIsForbidden()
    {
        await using WebApplicationFactory<Program> factory =
            CreateFactory(777, adminAllowedUserId: 999);
        using HttpClient client = factory.CreateClient();
        AuthenticationResponse authentication = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authentication.AccessToken);

        HttpResponseMessage response =
            await client.GetAsync("/api/v1/admin/content/current");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdminCanCreateValidateAndPublishRevision()
    {
        await using WebApplicationFactory<Program> factory =
            CreateFactory(777, adminAllowedUserId: 777);
        using HttpClient client = factory.CreateClient();
        AuthenticationResponse authentication = await AuthenticateAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authentication.AccessToken);

        ContentAdminCurrentResponse current =
            (await client.GetFromJsonAsync<ContentAdminCurrentResponse>(
                "/api/v1/admin/content/current"))!;

        JsonObject payload = JsonNode.Parse(current.PayloadJson)!.AsObject();
        payload["balanceVersion"] = "0.9.97";
        string changedPayload = payload.ToJsonString();

        HttpResponseMessage validationResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/content/validate",
            new ContentAdminValidateRequest(changedPayload));
        validationResponse.EnsureSuccessStatusCode();
        ContentAdminValidationResponse validation =
            (await validationResponse.Content
                .ReadFromJsonAsync<ContentAdminValidationResponse>())!;
        Assert.True(validation.IsValid);

        HttpResponseMessage draftResponse = await client.PostAsJsonAsync(
            "/api/v1/admin/content/revisions",
            new ContentAdminCreateRevisionRequest(
                changedPayload,
                current.PayloadSha256,
                "integration publish"));
        draftResponse.EnsureSuccessStatusCode();
        ContentAdminRevisionResponse revision =
            (await draftResponse.Content
                .ReadFromJsonAsync<ContentAdminRevisionResponse>())!;

        HttpResponseMessage publishResponse = await client.PostAsJsonAsync(
            $"/api/v1/admin/content/revisions/{revision.Id}/publish",
            new ContentAdminPublishRequest(
                current.PayloadSha256,
                "integration publish"));
        publishResponse.EnsureSuccessStatusCode();

        ContentAdminCurrentResponse after =
            (await client.GetFromJsonAsync<ContentAdminCurrentResponse>(
                "/api/v1/admin/content/current"))!;
        Assert.Equal("0.9.97", after.BalanceVersion);
        Assert.Equal(revision.Id, after.RevisionId);

        ContentAdminHistoryResponse history =
            (await client.GetFromJsonAsync<ContentAdminHistoryResponse>(
                "/api/v1/admin/content/history?limit=10"))!;
        Assert.Contains(history.Revisions, item => item.Id == revision.Id);
        Assert.Contains(history.Releases, item => item.RevisionId == revision.Id);
    }

    private async Task<AuthenticationResponse> AuthenticateAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/development",
            new { });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!;
    }

    private WebApplicationFactory<Program> CreateFactory(
        long developmentTelegramUserId,
        long adminAllowedUserId) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting(
                    "ConnectionStrings:game",
                    postgres.ConnectionString);
                builder.UseSetting("Authentication:Issuer", "Elyndor.Admin.Tests");
                builder.UseSetting(
                    "Authentication:Audience",
                    "Elyndor.Admin.Tests.Client");
                builder.UseSetting("Authentication:SigningKey", SigningKey);
                builder.UseSetting(
                    "Authentication:Telegram:BotToken",
                    "123456:TEST_TOKEN");
                builder.UseSetting(
                    "Authentication:Development:Enabled",
                    "true");
                builder.UseSetting(
                    "Authentication:Development:TelegramUserId",
                    developmentTelegramUserId.ToString(
                        global::System.Globalization.CultureInfo.InvariantCulture));
                builder.UseSetting(
                    "Administration:Telegram:AllowedUserIds:0",
                    adminAllowedUserId.ToString(
                        global::System.Globalization.CultureInfo.InvariantCulture));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(
                        new FixedTimeProvider(Now));
                });
            });

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
