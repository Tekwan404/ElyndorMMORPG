using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Identity;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.IntegrationTests.Support;
using Elyndor.Server.Monitoring;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Elyndor.IntegrationTests.Identity;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class PresenceEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task HeartbeatRequiresAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/presence/heartbeat",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedHeartbeatMarksAccountOnlineWithoutDoubleCounting()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage authenticationResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/development",
            new { });
        authenticationResponse.EnsureSuccessStatusCode();
        AuthenticationResponse? authentication =
            await authenticationResponse.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(authentication);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authentication.AccessToken);

        HttpResponseMessage first = await client.PostAsync(
            "/api/v1/presence/heartbeat",
            content: null);
        HttpResponseMessage second = await client.PostAsync(
            "/api/v1/presence/heartbeat",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        IServerMetricsCollector metricsCollector =
            factory.Services.GetRequiredService<IServerMetricsCollector>();
        ServerMetricsSnapshot metrics =
            await metricsCollector.CollectAsync(CancellationToken.None);

        Assert.Equal(1, metrics.OnlinePlayers);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
                builder.UseTestAuthentication();
                builder.UseSetting("Authentication:Development:Enabled", "true");
                builder.UseSetting("Authentication:Development:TelegramUserId", "777");
            });
}
