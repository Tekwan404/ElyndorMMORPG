using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Characters;
using Elyndor.Contracts.Identity;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elyndor.IntegrationTests.Characters;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CharacterEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 11, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CharacterEndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/character");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedCreationPersistsAndReplaysCharacter()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        await AuthenticateDevelopmentAsync(client);
        Guid requestId = Guid.CreateVersion7();
        CreateCharacterRequest request = new(
            requestId,
            "Arthas",
            "HUMAN",
            "MALE",
            "WARRIOR");

        HttpResponseMessage firstResponse = await client.PostAsJsonAsync(
            "/api/v1/character",
            request);
        HttpResponseMessage retryResponse = await client.PostAsJsonAsync(
            "/api/v1/character",
            request);
        CharacterResponse? first =
            await firstResponse.Content.ReadFromJsonAsync<CharacterResponse>();
        CharacterResponse? retry =
            await retryResponse.Content.ReadFromJsonAsync<CharacterResponse>();
        CharacterResponse? restored =
            await client.GetFromJsonAsync<CharacterResponse>("/api/v1/character");

        firstResponse.EnsureSuccessStatusCode();
        retryResponse.EnsureSuccessStatusCode();
        Assert.NotNull(first);
        Assert.Equal(first, retry);
        Assert.Equal(first, restored);

        await using GameDbContext context = postgres.CreateDbContext();
        Assert.Equal("STARTER_TOWN", (await context.CharacterLocations.SingleAsync()).LocationId);
    }

    [Fact]
    public async Task InvalidNameReturnsStableValidationProblem()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        await AuthenticateDevelopmentAsync(client);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/character",
            new CreateCharacterRequest(
                Guid.CreateVersion7(),
                "Aртас",
                "HUMAN",
                "MALE",
                "WARRIOR"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        ApiErrorResponse? error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("character_name_mixed_scripts", error?.Code);
        Assert.False(string.IsNullOrWhiteSpace(error?.CorrelationId));
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
                builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
                builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
                builder.UseSetting(
                    "Authentication:SigningKey",
                    "character-endpoint-test-signing-key-with-more-than-32-bytes");
                builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
                builder.UseSetting("Authentication:Development:Enabled", "true");
                builder.UseSetting("Authentication:Development:TelegramUserId", "9001");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
                });
            });

    private static async Task AuthenticateDevelopmentAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/development",
            new { });
        response.EnsureSuccessStatusCode();
        AuthenticationResponse? authentication =
            await response.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(authentication);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authentication.AccessToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
