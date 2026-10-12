using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Characters;
using Elyndor.Contracts.World;
using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Elyndor.IntegrationTests.World;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class LocationSceneTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private const string Forest = "WHISPERING_FOREST";
    private const string Wolf = "WHISPERING_FOREST_LESNOI_VOLK_L3";
    private long _nextTelegramUserId = 987600;
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SelectedEnemyStartsExistingCombatAndSelectionPreservesProgress()
    {
        await using var factory = CreateFactory();
        using HttpClient client = await CreatePlayerAsync(factory);
        BootstrapResponse before = (await client.GetFromJsonAsync<BootstrapResponse>("/api/v1/bootstrap"))!;
        var response = await client.PostAsJsonAsync("/api/v1/world/select-encounter",
            new SelectWorldEncounterRequest(Forest, Wolf));
        response.EnsureSuccessStatusCode();
        var encounter = (await response.Content.ReadFromJsonAsync<WorldEncounterResponse>())!;
        Assert.Equal(Wolf, encounter.MonsterId);
        var after = (await client.GetFromJsonAsync<BootstrapResponse>("/api/v1/bootstrap"))!;
        Assert.Equal(before.Character!.Experience, after.Character!.Experience);
        Assert.Equal(before.Character.Gold, after.Character.Gold);
        Assert.Equal(before.Character.Inventory.Items.Count, after.Character.Inventory.Items.Count);
        Assert.Equal(before.World!.Version, after.World!.Version);
        using var scope = factory.Services.CreateScope();
        var combat = scope.ServiceProvider.GetRequiredService<CombatApplicationService>();
        var started = await combat.StartAsync(before.AccountId, encounter.EncounterId, CancellationToken.None);
        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.Equal(Wolf, started.Snapshot!.Enemy.DefinitionId);
        var replay = await combat.StartAsync(before.AccountId, encounter.EncounterId, CancellationToken.None);
        Assert.False(replay.Succeeded);
        Assert.Equal(started.Snapshot.SessionId, combat.Resume(before.AccountId).Snapshot?.SessionId);
        await combat.LeaveAsync(before.AccountId, "scene-test-cleanup", CancellationToken.None);
        var reused = await combat.StartAsync(before.AccountId, encounter.EncounterId, CancellationToken.None);
        Assert.False(reused.Succeeded);
    }

    [Theory]
    [InlineData("STARTER_TOWN", Wolf)]
    [InlineData(Forest, "TRAINING_DUMMY")]
    [InlineData(Forest, "MISSING")]
    [InlineData(Forest, "")]
    public async Task InvalidSelectionDoesNotReplacePendingEncounter(string locationId, string monsterId)
    {
        await using var factory = CreateFactory();
        using HttpClient client = await CreatePlayerAsync(factory);
        var bootstrap = (await client.GetFromJsonAsync<BootstrapResponse>("/api/v1/bootstrap"))!;
        using var scope = factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<WorldEncounterRegistry>();
        var pending = registry.Register(bootstrap.AccountId, Forest, Wolf);
        var response = await client.PostAsJsonAsync("/api/v1/world/select-encounter",
            new SelectWorldEncounterRequest(locationId, monsterId));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.True(registry.TryConsume(bootstrap.AccountId, pending.EncounterId, out _));
    }

    [Fact]
    public async Task SceneIsServerAuthoredAndCannotBeReadAnonymously()
    {
        await using var factory = CreateFactory();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/world/scene")).StatusCode);
        using var client = await CreatePlayerAsync(factory);
        var response = await client.GetAsync("/api/v1/world/scene");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var scene = (await response.Content.ReadFromJsonAsync<WorldLocationSceneResponse>())!;
        Assert.Equal(Forest, scene.LocationId);
        Assert.Equal("Calm", scene.State);
        Assert.Contains(scene.Objects, entry => entry.Resident?.MonsterId == Wolf);
        Assert.Contains(scene.Objects, entry => entry.Kind == "Landmark");
        Assert.Contains(scene.Objects, entry => entry.Kind == "Npc" && entry.QuestId is not null);
        Assert.All(scene.Objects, entry =>
        {
            Assert.InRange(entry.X, 0, 100);
            Assert.InRange(entry.Y, 0, 100);
        });
    }

    [Fact]
    public async Task EncounterFromOldLocationCannotStartAfterMoving()
    {
        await using var factory = CreateFactory();
        using HttpClient client = await CreatePlayerAsync(factory);
        var bootstrap = (await client.GetFromJsonAsync<BootstrapResponse>("/api/v1/bootstrap"))!;
        var response = await client.PostAsJsonAsync("/api/v1/world/select-encounter",
            new SelectWorldEncounterRequest(Forest, Wolf));
        var encounter = (await response.Content.ReadFromJsonAsync<WorldEncounterResponse>())!;
        await using (var db = postgres.CreateDbContext())
        {
            var location = await db.CharacterLocations.SingleAsync();
            location.Relocate("STARTER_TOWN", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<CombatApplicationService>()
            .StartAsync(bootstrap.AccountId, encounter.EncounterId, CancellationToken.None);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RareWindowIsSharedBetweenAccountsAndUnavailableTargetsAreRejected()
    {
        var absentAt = new DateTimeOffset(2026, 10, 11, 0, 15, 0, TimeSpan.Zero);
        await using var factory = CreateFactory(absentAt);
        using var first = await CreatePlayerAsync(factory, "FirstVisitor");
        using var second = await CreatePlayerAsync(factory, "OtherVisitor");
        var a = (await first.GetFromJsonAsync<WorldLocationSceneResponse>("/api/v1/world/scene"))!;
        var b = (await second.GetFromJsonAsync<WorldLocationSceneResponse>("/api/v1/world/scene"))!;
        Assert.Equal(a.Objects.Select(entry => entry.Id), b.Objects.Select(entry => entry.Id));
        Assert.Equal(a.NextChangeAtUtc, b.NextChangeAtUtc);
        const string rare = "WHISPERING_FOREST_DIKII_KABAN_VOZHAK_L4";
        Assert.DoesNotContain(a.Objects, entry => entry.Resident?.MonsterId == rare);
        var rejected = await first.PostAsJsonAsync("/api/v1/world/select-encounter",
            new SelectWorldEncounterRequest(Forest, rare));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }

    [Fact]
    public async Task RareEliteCanBeChosenWhileItsWindowIsOpen()
    {
        await using var factory = CreateFactory(new DateTimeOffset(2026, 10, 11, 0, 5, 0, TimeSpan.Zero));
        using var client = await CreatePlayerAsync(factory);
        var scene = (await client.GetFromJsonAsync<WorldLocationSceneResponse>("/api/v1/world/scene"))!;
        var rare = Assert.Single(scene.Objects, entry => entry.IsRare);
        Assert.Equal("Elite", rare.Resident!.Rank);
        var response = await client.PostAsJsonAsync("/api/v1/world/select-encounter",
            new SelectWorldEncounterRequest(Forest, rare.Resident.MonsterId));
        response.EnsureSuccessStatusCode();
        Assert.Equal(rare.Resident.MonsterId,
            (await response.Content.ReadFromJsonAsync<WorldEncounterResponse>())!.MonsterId);
    }

    [Fact]
    public async Task TravelAndActiveCombatBlockManualSelection()
    {
        await using var factory = CreateFactory();
        using var client = await CreatePlayerAsync(factory);
        var bootstrap = (await client.GetFromJsonAsync<BootstrapResponse>("/api/v1/bootstrap"))!;
        await using (var db = postgres.CreateDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            db.CharacterTravelStates.Add(new CharacterTravelState(bootstrap.Character!.Id,
                Guid.CreateVersion7(), Forest, "DEEP_FOREST", now, now.AddMinutes(5)));
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/world/select-encounter",
            new SelectWorldEncounterRequest(Forest, Wolf))).StatusCode);
        await using (var db = postgres.CreateDbContext())
        {
            db.CharacterTravelStates.RemoveRange(await db.CharacterTravelStates.ToArrayAsync());
            await db.SaveChangesAsync();
        }
        var response = await client.PostAsJsonAsync("/api/v1/world/select-encounter",
            new SelectWorldEncounterRequest(Forest, Wolf));
        var encounter = (await response.Content.ReadFromJsonAsync<WorldEncounterResponse>())!;
        using var scope = factory.Services.CreateScope();
        var combat = scope.ServiceProvider.GetRequiredService<CombatApplicationService>();
        Assert.True((await combat.StartAsync(bootstrap.AccountId, encounter.EncounterId, CancellationToken.None)).Succeeded);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/world/select-encounter",
            new SelectWorldEncounterRequest(Forest, Wolf))).StatusCode);
        await combat.LeaveAsync(bootstrap.AccountId, "scene-travel-cleanup", CancellationToken.None);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompletedQuestWithoutModernStateDisappearsFromScene(bool legacyCompletion)
    {
        await using var factory = CreateFactory();
        using var client = await CreatePlayerAsync(factory);
        var bootstrap = (await client.GetFromJsonAsync<BootstrapResponse>("/api/v1/bootstrap"))!;
        var before = (await client.GetFromJsonAsync<WorldLocationSceneResponse>("/api/v1/world/scene"))!;
        var questId = before.Objects.First(item => item.QuestId is not null).QuestId!;
        await using (var db = postgres.CreateDbContext())
        {
            if (legacyCompletion)
                db.CharacterContractCompletions.Add(new CharacterContractCompletion(
                    bootstrap.Character!.Id, questId, Wolf, Guid.CreateVersion7(), Now));
            else
                db.QuestRewardGrants.Add(new Elyndor.Core.Quests.QuestRewardGrant(
                    bootstrap.Character!.Id, questId, Guid.CreateVersion7(), 0, 0, "[]", Now));
            await db.SaveChangesAsync();
        }
        var after = (await client.GetFromJsonAsync<WorldLocationSceneResponse>("/api/v1/world/scene"))!;
        Assert.DoesNotContain(after.Objects, item => item.QuestId == questId);
    }

    [Fact]
    public async Task CompletedQuestDisappearsButReadyRewardRemainsInScene()
    {
        await using var factory = CreateFactory();
        using var client = await CreatePlayerAsync(factory);
        var bootstrap = (await client.GetFromJsonAsync<BootstrapResponse>("/api/v1/bootstrap"))!;
        var before = (await client.GetFromJsonAsync<WorldLocationSceneResponse>("/api/v1/world/scene"))!;
        var quests = before.Objects.Where(item => item.QuestId is not null).Take(2).ToArray();
        Assert.Equal(2, quests.Length);
        await using (var db = postgres.CreateDbContext())
        {
            var completed = new Elyndor.Core.Quests.CharacterQuestState(bootstrap.Character!.Id, quests[0].QuestId!, Now);
            completed.MarkCompleted(Now);
            var ready = new Elyndor.Core.Quests.CharacterQuestState(bootstrap.Character.Id, quests[1].QuestId!, Now);
            ready.UpdateProgress("{}", true, Now);
            db.CharacterQuestStates.AddRange(completed, ready);
            await db.SaveChangesAsync();
        }
        var after = (await client.GetFromJsonAsync<WorldLocationSceneResponse>("/api/v1/world/scene"))!;
        Assert.DoesNotContain(after.Objects, item => item.QuestId == quests[0].QuestId);
        Assert.Contains(after.Objects, item => item.QuestId == quests[1].QuestId);
    }

    [Fact]
    public async Task EliteVictoryStartsSharedFiveMinuteRespawnAndStaleEncounterCannotStart()
    {
        const string elite = "WHISPERING_FOREST_DIKII_KABAN_VOZHAK_L4";
        var at = DateTimeOffset.FromUnixTimeSeconds(Now.ToUnixTimeSeconds() / 3600 * 3600 + 60);
        await using var factory = CreateFactory(at);
        using var first = await CreatePlayerAsync(factory);
        using var second = await CreatePlayerAsync(factory, "OtherHunter");
        var firstBootstrap = (await first.GetFromJsonAsync<BootstrapResponse>("/api/v1/bootstrap"))!;
        var secondBootstrap = (await second.GetFromJsonAsync<BootstrapResponse>("/api/v1/bootstrap"))!;
        var prepared = await first.PostAsJsonAsync("/api/v1/world/select-encounter", new SelectWorldEncounterRequest(Forest, elite));
        prepared.EnsureSuccessStatusCode();
        var encounter = (await prepared.Content.ReadFromJsonAsync<WorldEncounterResponse>())!;
        await using (var db = postgres.CreateDbContext())
        {
            db.CombatRewardGrants.Add(new Elyndor.Core.Progression.CombatRewardGrant(Guid.NewGuid(), secondBootstrap.Character!.Id, elite, 0, 0, at));
            await db.SaveChangesAsync();
        }
        var denied = await first.PostAsJsonAsync("/api/v1/world/select-encounter", new SelectWorldEncounterRequest(Forest, elite));
        Assert.False(denied.IsSuccessStatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var combat = scope.ServiceProvider.GetRequiredService<CombatApplicationService>();
            var stale = await combat.StartAsync(firstBootstrap.AccountId, encounter.EncounterId, CancellationToken.None);
            Assert.False(stale.Succeeded);
            Assert.Equal(WorldEncounterErrorCodes.TargetUnavailable, stale.ErrorCode);
        }
        var sceneJson = await first.GetStringAsync("/api/v1/world/scene");
        using var document = global::System.Text.Json.JsonDocument.Parse(sceneJson);
        var marker = document.RootElement.GetProperty("objects").EnumerateArray().Single(item => item.GetProperty("id").GetString() == $"ENEMY_{elite}");
        Assert.Equal(at.AddMinutes(5), marker.GetProperty("respawnAtUtc").GetDateTimeOffset());
        await using (var restartedFactory = CreateFactory(at))
        {
            using var restarted = restartedFactory.CreateClient();
            restarted.DefaultRequestHeaders.Authorization = first.DefaultRequestHeaders.Authorization;
            Assert.False((await restarted.PostAsJsonAsync("/api/v1/world/select-encounter", new SelectWorldEncounterRequest(Forest, elite))).IsSuccessStatusCode);
        }
        ((SceneTimeProvider)factory.Services.GetRequiredService<TimeProvider>()).Now = at.AddMinutes(5);
        Assert.True((await first.PostAsJsonAsync("/api/v1/world/select-encounter", new SelectWorldEncounterRequest(Forest, elite))).IsSuccessStatusCode);
        Assert.True((await second.PostAsJsonAsync("/api/v1/world/select-encounter", new SelectWorldEncounterRequest(Forest, elite))).IsSuccessStatusCode);
    }

    [Fact]
    public async Task EveryOpenWorldEliteHasFiveMinuteRespawnContent()
    {
        var content = await Elyndor.Infrastructure.Content.GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var monsters = content.Monsters!.ToDictionary(item => item.Id);
        var elites = content.Locations.Where(location => !(content.Dungeons ?? []).Any(dungeon => dungeon.Id == location.Id || dungeon.EntryLocationId == location.Id))
            .SelectMany(location => location.Encounters ?? []).Where(encounter => monsters[encounter.MonsterId].Rank == Elyndor.Core.Monsters.MonsterRank.Elite).ToArray();
        Assert.NotEmpty(elites);
        Assert.All(elites, encounter => Assert.Equal(300, encounter.RespawnSeconds));
    }

    private WebApplicationFactory<Program> CreateFactory(DateTimeOffset? at = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
            builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
            builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
            builder.UseSetting("Authentication:SigningKey", "location-scene-test-signing-key-with-more-than-32-bytes");
            builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
            builder.UseSetting("Authentication:Development:Enabled", "false");
            if (at is { } now)
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(new SceneTimeProvider(now));
                });
        });

    private async Task<HttpClient> CreatePlayerAsync(WebApplicationFactory<Program> factory, string name = "SceneTester")
    {
        var accountId = Guid.CreateVersion7();
        var telegramUserId = ++_nextTelegramUserId;
        await using (var db = postgres.CreateDbContext())
        {
            db.Accounts.Add(new Account(accountId, telegramUserId, Now));
            await db.SaveChangesAsync();
        }
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<JwtTokenIssuer>().Issue(accountId, telegramUserId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var created = await client.PostAsJsonAsync("/api/v1/character",
            new CreateCharacterRequest(Guid.CreateVersion7(), name, "HUMAN", "MALE", "WARRIOR"));
        created.EnsureSuccessStatusCode();
        var character = (await created.Content.ReadFromJsonAsync<CharacterResponse>())!;
        await using (var db = postgres.CreateDbContext())
        {
            var location = await db.CharacterLocations.SingleAsync(entry => entry.CharacterId == character.Id);
            location.Relocate(Forest, factory.Services.GetRequiredService<TimeProvider>().GetUtcNow());
            await db.SaveChangesAsync();
        }
        return client;
    }

    private sealed class SceneTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
