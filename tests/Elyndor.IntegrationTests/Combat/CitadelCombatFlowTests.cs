using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Characters;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elyndor.IntegrationTests.Combat;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CitadelCombatFlowTests(PostgresFixture postgres) : IAsyncLifetime
{
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SoloCitadelRunStartsFirstEliteEncounter()
    {
        Guid accountId = Guid.CreateVersion7();
        const long telegramUserId = 9751;
        DateTimeOffset seededAtUtc = DateTimeOffset.UtcNow;

        await using (GameDbContext seedContext = postgres.CreateDbContext())
        {
            seedContext.Accounts.Add(new Account(accountId, telegramUserId, seededAtUtc));
            await seedContext.SaveChangesAsync();
        }

        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = CreateAuthenticatedClient(factory, accountId, telegramUserId);
        CharacterResponse created = await CreateCharacterAsync(client);

        await using (GameDbContext setupContext = postgres.CreateDbContext())
        {
            Character character = await setupContext.Characters
                .SingleAsync(candidate => candidate.Id == created.Id);
            character.SetLevel(25);
            CharacterLocation location = await setupContext.CharacterLocations
                .SingleAsync(candidate => candidate.CharacterId == created.Id);
            location.Relocate("ECLIPSED_CITADEL", seededAtUtc);
            await setupContext.SaveChangesAsync();
        }

        using IServiceScope scope = factory.Services.CreateScope();
        DungeonService dungeons = scope.ServiceProvider.GetRequiredService<DungeonService>();
        CombatApplicationService combat = scope.ServiceProvider.GetRequiredService<CombatApplicationService>();

        DungeonOperationResult createdRun = await dungeons.CreateAsync(
            accountId,
            "ECLIPSED_CITADEL",
            Guid.NewGuid(),
            CancellationToken.None);
        Assert.True(createdRun.Succeeded, createdRun.ErrorCode);
        Assert.NotNull(createdRun.Run);

        CombatOperationResult started = await combat.StartDungeonEncounterAsync(
            accountId,
            createdRun.Run!.RunId,
            CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.NotNull(started.Snapshot);

        DungeonRunView? current = await dungeons.GetCurrentAsync(accountId, CancellationToken.None);
        Assert.NotNull(current);
        DungeonEncounterView first = Assert.Single(
            current!.Encounters,
            encounter => encounter.EncounterIndex == 0);
        Assert.Equal(DungeonEncounterState.Active, first.State);
        Assert.Equal("ECLIPSED_CITADEL_SENTINEL_L25", first.MonsterId);
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
                    "citadel-combat-flow-test-signing-key-with-more-than-32-bytes");
                builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
                builder.UseSetting("Authentication:Development:Enabled", "false");
            });

    private static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> factory,
        Guid accountId,
        long telegramUserId)
    {
        IssuedAccessToken token = factory.Services.GetRequiredService<JwtTokenIssuer>().Issue(
            accountId,
            telegramUserId);
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);
        return client;
    }

    private static async Task<CharacterResponse> CreateCharacterAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/character",
            new CreateCharacterRequest(
                Guid.CreateVersion7(),
                "CitadelElite",
                "HUMAN",
                "MALE",
                "WARRIOR"));
        response.EnsureSuccessStatusCode();
        CharacterResponse? character = await response.Content.ReadFromJsonAsync<CharacterResponse>();
        Assert.NotNull(character);
        return character;
    }
}
