using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Characters;
using Elyndor.Contracts.Combat;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.WorldBosses;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elyndor.IntegrationTests.WorldBosses;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class WorldBossEnterFlowTests(PostgresFixture postgres) : IAsyncLifetime
{
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConfiguredWorldBossEnterCreatesBoundPveSessionAndDamageHitsGlobalHealth()
    {
        GameContentPackage package = await ConfiguredPackageAsync();
        Guid accountId = Guid.CreateVersion7();
        const long telegramUserId = 9751;

        await SeedAccountAsync(accountId, telegramUserId);
        await using WebApplicationFactory<Program> factory = CreateFactory(package);
        using HttpClient client = CreateAuthenticatedClient(factory, accountId, telegramUserId);
        _ = await CreateCharacterAsync(client, "BossTester");

        Guid spawnId;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            WorldBossActivationResult activation = await scope.ServiceProvider
                .GetRequiredService<WorldBossLifecycleService>()
                .ActivateAsync("WORLD_BOSS_ASH_ARCHON", default);
            Assert.True(activation.Succeeded, activation.ErrorCode);
            spawnId = activation.Spawn!.Id;
        }

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/world-boss/{spawnId:D}/enter",
            content: null);
        response.EnsureSuccessStatusCode();
        CombatUpdateResponse entered =
            (await response.Content.ReadFromJsonAsync<CombatUpdateResponse>())!;
        Assert.True(entered.Succeeded, entered.ErrorCode);
        Assert.NotNull(entered.Snapshot);
        Assert.Equal(1_000_000m, entered.Snapshot!.Enemy.MaxHp);
        Assert.Equal(1_000_000m, entered.Snapshot.Enemy.Hp);

        IssuedAccessToken token = IssueToken(factory, accountId, telegramUserId);
        await using HubConnection hub = CreateHubConnection(factory, token);
        await hub.StartAsync();

        try
        {
            CombatUpdateResponse stopped = await hub.InvokeAsync<CombatUpdateResponse>(
                "StopAutoAttack",
                entered.Snapshot.SessionId,
                "world-boss-stop");
            Assert.True(stopped.Succeeded, stopped.ErrorCode);

            CombatUpdateResponse attacked = await hub.InvokeAsync<CombatUpdateResponse>(
                "StartAutoAttack",
                entered.Snapshot.SessionId,
                "world-boss-start");
            Assert.True(attacked.Succeeded, attacked.ErrorCode);
            CombatEventResponse hit = Assert.Single(
                attacked.Events,
                combatEvent => combatEvent.Type == "DamageDealt"
                    && combatEvent.SourceActorId == entered.Snapshot.Player.ActorId
                    && combatEvent.TargetActorId == entered.Snapshot.Enemy.ActorId
                    && combatEvent.Amount > 0);

            await using GameDbContext verify = postgres.CreateDbContext();
            WorldBossSpawn spawn = await verify.WorldBossSpawns.SingleAsync(
                candidate => candidate.Id == spawnId);
            WorldBossContribution contribution =
                await verify.WorldBossContributions.SingleAsync();
            WorldBossCombatSessionBinding binding =
                await verify.WorldBossCombatSessions.SingleAsync();

            Assert.Equal(1_000_000m - hit.Amount, spawn.CurrentHealth);
            Assert.Equal(hit.Amount, contribution.Damage);
            Assert.Equal(entered.Snapshot.SessionId, binding.CombatSessionId);
            Assert.Equal(spawnId, binding.SpawnId);
            Assert.Equal(entered.Snapshot.Enemy.ActorId, binding.BossActorId);
        }
        finally
        {
            await hub.InvokeAsync<CombatUpdateResponse>(
                "LeaveCombat",
                "world-boss-cleanup");
        }
    }

    [Fact]
    public async Task BundledAshArchonFailsClosedUntilRealEncounterProfileIsAuthored()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        Assert.DoesNotContain(
            package.Encounters ?? [],
            encounter => encounter.Id == "WB_ASH_ARCHON_V1");

        Guid accountId = Guid.CreateVersion7();
        const long telegramUserId = 9752;
        await SeedAccountAsync(accountId, telegramUserId);

        await using WebApplicationFactory<Program> factory = CreateFactory(package);
        using HttpClient client = CreateAuthenticatedClient(factory, accountId, telegramUserId);
        _ = await CreateCharacterAsync(client, "BossConfig");

        Guid spawnId;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            WorldBossActivationResult activation = await scope.ServiceProvider
                .GetRequiredService<WorldBossLifecycleService>()
                .ActivateAsync("WORLD_BOSS_ASH_ARCHON", default);
            Assert.True(activation.Succeeded, activation.ErrorCode);
            spawnId = activation.Spawn!.Id;
        }

        HttpResponseMessage response = await client.PostAsync(
            $"/api/v1/world-boss/{spawnId:D}/enter",
            content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains(
            WorldBossEnterErrorCodes.EncounterNotConfigured,
            body,
            StringComparison.Ordinal);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.WorldBossCombatSessions.ToArrayAsync());
        Assert.Empty(await verify.ActiveCombatSessions.ToArrayAsync());
    }

    private async Task SeedAccountAsync(Guid accountId, long telegramUserId)
    {
        await using GameDbContext db = postgres.CreateDbContext();
        db.Accounts.Add(new Account(accountId, telegramUserId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private static async Task<GameContentPackage> ConfiguredPackageAsync()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        GameContentIndexes indexes = GameContentIndexes.For(package);
        EncounterDefinition encounter = (package.Encounters ?? [])
            .First(candidate =>
                indexes.MonstersById.TryGetValue(candidate.MonsterId, out var monster)
                && monster.Level <= 5);
        WorldBossDefinition ash = package.WorldBosses!
            .Single(candidate => candidate.Id == "WORLD_BOSS_ASH_ARCHON");

        return package with
        {
            WorldBosses = package.WorldBosses!
                .Select(candidate => candidate.Id == ash.Id
                    ? candidate with { EncounterProfileId = encounter.Id }
                    : candidate)
                .ToArray()
        };
    }

    private WebApplicationFactory<Program> CreateFactory(GameContentPackage package) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
                builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
                builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
                builder.UseSetting(
                    "Authentication:SigningKey",
                    "world-boss-enter-test-signing-key-with-more-than-32-bytes");
                builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
                builder.UseSetting("Authentication:Development:Enabled", "false");
                builder.UseSetting("Database:MigrateOnStartup", "false");
                builder.UseSetting("Content:RestorePublishedOnStartup", "false");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IContentSnapshotProvider>();
                    services.AddSingleton<IContentSnapshotProvider>(
                        new StaticContentSnapshotProvider(package));
                });
            });

    private static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> factory,
        Guid accountId,
        long telegramUserId)
    {
        IssuedAccessToken token = IssueToken(factory, accountId, telegramUserId);
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken);
        return client;
    }

    private static async Task<CharacterResponse> CreateCharacterAsync(
        HttpClient client,
        string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/character",
            new CreateCharacterRequest(
                Guid.CreateVersion7(),
                name,
                "HUMAN",
                "MALE",
                "WARRIOR"));
        response.EnsureSuccessStatusCode();
        CharacterResponse? character =
            await response.Content.ReadFromJsonAsync<CharacterResponse>();
        Assert.NotNull(character);
        return character;
    }

    private static IssuedAccessToken IssueToken(
        WebApplicationFactory<Program> factory,
        Guid accountId,
        long telegramUserId) =>
        factory.Services.GetRequiredService<JwtTokenIssuer>().Issue(
            accountId,
            telegramUserId);

    private static HubConnection CreateHubConnection(
        WebApplicationFactory<Program> factory,
        IssuedAccessToken token) =>
        new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/hubs/combat",
                options =>
                {
                    options.AccessTokenProvider = () =>
                        Task.FromResult<string?>(token.AccessToken);
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();
}
