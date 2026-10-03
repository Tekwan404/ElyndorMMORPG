using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Characters;
using Elyndor.Contracts.Combat;
using Elyndor.Contracts.WorldBosses;
using Elyndor.Core.Content;
using Elyndor.Core.Combat.Randomness;
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
    public async Task BundledAshArchonEnterCreatesBoundPveSessionAndDamageHitsGlobalHealth()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
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
        Assert.Equal("WORLD_BOSS_ASH_ARCHON_L60", entered.Snapshot!.Enemy.DefinitionId);
        Assert.Equal(1_000_000m, entered.Snapshot.Enemy.MaxHp);
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

            decimal globalDamage = 1_000_000m - spawn.CurrentHealth;
            Assert.True(globalDamage >= hit.Amount);
            Assert.Equal(globalDamage, contribution.Damage);
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
    public async Task MissingWorldBossEncounterProfileStillFailsClosed()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        WorldBossDefinition ash = package.WorldBosses!.Single(
            candidate => candidate.Id == "WORLD_BOSS_ASH_ARCHON");
        package = package with
        {
            WorldBosses = package.WorldBosses!
                .Select(candidate => candidate.Id == ash.Id
                    ? candidate with { EncounterProfileId = "WB_MISSING_PROFILE" }
                    : candidate)
                .ToArray()
        };

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

    [Fact]
    public async Task LiveWorldBossSessionPullsGlobalHealthPhaseAndDefeatBeforeCommand()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        Guid accountId = Guid.CreateVersion7();
        const long telegramUserId = 9753;
        await SeedAccountAsync(accountId, telegramUserId);

        await using WebApplicationFactory<Program> factory = CreateFactory(package);
        using HttpClient client = CreateAuthenticatedClient(factory, accountId, telegramUserId);
        CharacterResponse character = await CreateCharacterAsync(client, "BossSync");

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
        Assert.NotNull(entered.Snapshot);

        IssuedAccessToken token = IssueToken(factory, accountId, telegramUserId);
        await using HubConnection hub = CreateHubConnection(factory, token);
        await hub.StartAsync();

        try
        {
            CombatUpdateResponse stopped = await hub.InvokeAsync<CombatUpdateResponse>(
                "StopAutoAttack",
                entered.Snapshot!.SessionId,
                "world-boss-sync-stop");
            Assert.True(stopped.Succeeded, stopped.ErrorCode);

            decimal currentGlobalHealth;
            await using (GameDbContext read = postgres.CreateDbContext())
            {
                currentGlobalHealth = (await read.WorldBossSpawns.SingleAsync(
                    spawn => spawn.Id == spawnId)).CurrentHealth;
            }

            decimal damageToPhaseTwo = currentGlobalHealth - 740_000m;
            Assert.True(damageToPhaseTwo > 0);
            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                WorldBossDamageCommitResult phaseDamage = await scope.ServiceProvider
                    .GetRequiredService<WorldBossDamageService>()
                    .ApplyDamageAsync(
                        spawnId,
                        character.Id,
                        entered.Snapshot.SessionId,
                        partyId: null,
                        requestedDamage: damageToPhaseTwo,
                        mutationId: Guid.CreateVersion7(),
                        cancellationToken: default);
                Assert.True(phaseDamage.Succeeded, phaseDamage.ErrorCode);
                Assert.Equal(2, phaseDamage.Phase);
                Assert.Equal(740_000m, phaseDamage.CurrentHealth);
            }

            CombatUpdateResponse phaseSynced = await hub.InvokeAsync<CombatUpdateResponse>(
                "StopAutoAttack",
                entered.Snapshot.SessionId,
                "world-boss-sync-phase");
            Assert.True(phaseSynced.Succeeded, phaseSynced.ErrorCode);
            Assert.Equal(740_000m, phaseSynced.Snapshot!.Enemy.Hp);
            Assert.Contains("ARCHON_STAR_FRACTURE", phaseSynced.Snapshot.Enemy.KnownAbilityIds);

            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                WorldBossDamageCommitResult defeat = await scope.ServiceProvider
                    .GetRequiredService<WorldBossDamageService>()
                    .ApplyDamageAsync(
                        spawnId,
                        character.Id,
                        entered.Snapshot.SessionId,
                        partyId: null,
                        requestedDamage: 2_000_000m,
                        mutationId: Guid.CreateVersion7(),
                        cancellationToken: default);
                Assert.True(defeat.Succeeded, defeat.ErrorCode);
                Assert.True(defeat.DefeatedNow);
            }

            CombatUpdateResponse defeated = await hub.InvokeAsync<CombatUpdateResponse>(
                "StopAutoAttack",
                entered.Snapshot.SessionId,
                "world-boss-sync-defeat");
            Assert.True(defeated.Succeeded, defeated.ErrorCode);
            Assert.Equal("Victory", defeated.Snapshot!.Status);
            Assert.Equal(0m, defeated.Snapshot.Enemy.Hp);
            Assert.Null(defeated.Reward);

            await using GameDbContext verify = postgres.CreateDbContext();
            Assert.Empty(await verify.CombatRewardGrants.ToArrayAsync());
        }
        finally
        {
            if (hub.State == HubConnectionState.Connected)
            {
                await hub.InvokeAsync<CombatUpdateResponse>(
                    "LeaveCombat",
                    "world-boss-sync-cleanup");
            }
        }
    }

    [Fact]
    public async Task SettledWorldBossRewardsAreReturnedByPersonalRewardEndpoint()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        Guid accountId = Guid.CreateVersion7();
        const long telegramUserId = 9754;
        await SeedAccountAsync(accountId, telegramUserId);

        await using WebApplicationFactory<Program> factory = CreateFactory(package);
        using HttpClient client = CreateAuthenticatedClient(factory, accountId, telegramUserId);
        CharacterResponse character = await CreateCharacterAsync(client, "BossReward");

        Guid spawnId;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            WorldBossActivationResult activation = await scope.ServiceProvider
                .GetRequiredService<WorldBossLifecycleService>()
                .ActivateAsync("WORLD_BOSS_ASH_ARCHON", default);
            Assert.True(activation.Succeeded, activation.ErrorCode);
            spawnId = activation.Spawn!.Id;

            GameDbContext db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            WorldBossSpawn spawn = await db.WorldBossSpawns.SingleAsync(
                candidate => candidate.Id == spawnId);
            DateTimeOffset now = scope.ServiceProvider
                .GetRequiredService<TimeProvider>()
                .GetUtcNow();
            _ = spawn.ApplyDamage(spawn.MaxHealth);
            Assert.True(spawn.TryMarkDefeated(now));

            var contribution = new WorldBossContribution(
                spawnId,
                character.Id,
                now);
            contribution.AddDamage(50_000m, now);
            db.WorldBossContributions.Add(contribution);
            await db.SaveChangesAsync();

            WorldBossSettlementBatchResult settlement = await scope.ServiceProvider
                .GetRequiredService<WorldBossSettlementService>()
                .SettleAsync(spawnId, default);
            Assert.True(settlement.Succeeded, settlement.ErrorCode);
        }

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/world-boss/{spawnId:D}/rewards/me");
        response.EnsureSuccessStatusCode();
        WorldBossRewardResponse? reward =
            await response.Content.ReadFromJsonAsync<WorldBossRewardResponse>();

        Assert.NotNull(reward);
        Assert.Equal(spawnId, reward.SpawnId);
        Assert.Equal(50_000m, reward.Contribution);
        Assert.Equal("Top5", reward.Tier);
        Assert.Equal(1, reward.Rank);
        Assert.Equal(1, reward.EligibleParticipants);
        Assert.Equal(100m, reward.Percentile);
        Assert.Equal(0, reward.ChestCount);
        Assert.Equal(2, reward.EnhancedChestCount);
        Assert.Equal(200_000, reward.Experience);
        Assert.Equal(1_000, reward.BossGold);
        Assert.Equal(0, reward.ChestGold);
        Assert.Equal(reward.BossGold, reward.TotalGold);
        WorldBossRewardItemResponse chest = Assert.Single(reward.Items);
        Assert.Equal("WORLD_BOSS_ASH_ARCHON_TOP5_CHEST", chest.ItemId);
        Assert.Equal(2, chest.Quantity);
    }

    private async Task SeedAccountAsync(Guid accountId, long telegramUserId)
    {
        await using GameDbContext db = postgres.CreateDbContext();
        db.Accounts.Add(new Account(accountId, telegramUserId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
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
                    services.RemoveAll<IGameRandomFactory>();
                    services.AddSingleton<IGameRandomFactory>(
                        new HighRollGameRandomFactory());
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

    private sealed class HighRollGameRandomFactory : IGameRandomFactory
    {
        public IGameRandom Create() => new HighRollGameRandom();
    }

    private sealed class HighRollGameRandom : IGameRandom
    {
        public decimal NextUnit() => 0.99m;
    }
}
