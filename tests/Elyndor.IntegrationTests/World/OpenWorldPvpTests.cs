using System.Net.Http.Headers;
using System.Net.Http.Json;
using Elyndor.Contracts.Combat;
using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elyndor.IntegrationTests.World;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class OpenWorldPvpTests(PostgresFixture postgres) : IAsyncLifetime
{
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("WHISPERING_FOREST", 60)]
    [InlineData("DEAD_REACHES", 59)]
    public async Task ServerRejectsCombatOutsidePvpZoneOrBelowLevel(string location, int level)
    {
        var attacker = await SeedAsync("HUMAN", location, level);
        var target = await SeedAsync("UNDEAD", location, level);
        await using var host = CreateFactory();
        using var first = Client(host, attacker.Account);
        using var second = Client(host, target.Account);
        var players = await first.GetFromJsonAsync<OpenWorldPlayersResult>("/api/v1/world/pvp/players");
        Assert.False(players!.Available);
        await second.GetAsync("/api/v1/world/pvp/players");
        var result = await EnterAsync(first, target.Character);
        Assert.False(result.Succeeded);
        await using var db = postgres.CreateDbContext();
        Assert.Empty(await db.ActiveCombatSessions.ToArrayAsync());
    }

    [Fact]
    public async Task OpponentsStartOneSharedFightAndRetriesCannotCreateAnother()
    {
        var attacker = await SeedAsync("HUMAN");
        var target = await SeedAsync("UNDEAD");
        var ally = await SeedAsync("HUMAN");
        await using var host = CreateFactory(freezeCombat: true);
        using (var healthScope = host.Services.CreateScope())
        {
            var healthDb = healthScope.ServiceProvider.GetRequiredService<GameDbContext>();
            var derivedService = healthScope.ServiceProvider.GetRequiredService<Elyndor.Infrastructure.Characters.CharacterDerivedStateService>();
            foreach (var id in new[] { attacker.Character, target.Character, ally.Character })
            {
                var character = await healthDb.Characters.SingleAsync(c => c.Id == id);
                var derived = await derivedService.ResolveAsync(id, character.ClassId, character.Level, CancellationToken.None);
                var vitals = await healthDb.CharacterVitals.SingleAsync(v => v.CharacterId == id);
                vitals.BeginContext(derived.Stats.MaxHp, 0, healthScope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow());
            }
            await healthDb.SaveChangesAsync();
        }
        using var first = Client(host, attacker.Account);
        using var second = Client(host, target.Account);
        using var third = Client(host, ally.Account);
        await second.GetAsync("/api/v1/world/pvp/players");
        var players = await first.GetFromJsonAsync<OpenWorldPlayersResult>("/api/v1/world/pvp/players");
        Assert.Contains(players!.Players, player => player.CharacterId == target.Character);
        var started = await EnterAsync(first, target.Character);
        Assert.True(started.Succeeded, $"Starting open-world duel failed: {started.ErrorCode}");
        Assert.True(started.Snapshot!.OpenWorldPvp);
        Assert.Equal("Player", started.Snapshot.Enemy.Kind);
        Assert.DoesNotContain(started.Snapshot.Enemies!, actor => actor.Kind == "Monster");
        var registry = host.Services.GetRequiredService<CombatSessionRegistry>();
        var defender = await registry.ResumeAsync(target.Account, 0, CancellationToken.None);
        Assert.Equal(started.Snapshot.SessionId, defender.Snapshot!.SessionId);
        Assert.Equal(target.Character, defender.Snapshot.Player.ActorId);
        Assert.Equal(attacker.Character, defender.Snapshot.Enemy.ActorId);
        var retry = await EnterAsync(first, target.Character);
        Assert.False(retry.Succeeded);
        Assert.True(registry.IsOpenWorldCombat(attacker.Account), "Attacker must still have an active fight before ally admission.");
        var joined = await EnterAsync(third, attacker.Character, attack: false);
        Assert.True(joined.Succeeded, $"Joining open-world duel failed: {joined.ErrorCode}");
        Assert.Equal(started.Snapshot.SessionId, joined.Snapshot!.SessionId);
        Assert.Contains(joined.Snapshot.Players!, player => player.ActorId == attacker.Character);
        Assert.DoesNotContain(joined.Snapshot.Players!, player => player.ActorId == target.Character);
        await using var db = postgres.CreateDbContext();
        Assert.Equal(3, await db.ActiveCombatSessions.CountAsync());
        Assert.Single(await db.ActiveCombatSessions.Select(row => row.SessionId).Distinct().ToArrayAsync());
    }

    [Fact]
    public async Task OfflineEnemyAndSameFactionCannotBeAttacked()
    {
        var attacker = await SeedAsync("HUMAN");
        var offline = await SeedAsync("UNDEAD");
        var ally = await SeedAsync("HUMAN");
        await using var host = CreateFactory();
        using var first = Client(host, attacker.Account);
        Assert.False((await EnterAsync(first, offline.Character)).Succeeded);
        using var third = Client(host, ally.Account);
        await third.GetAsync("/api/v1/world/pvp/players");
        Assert.Equal("world_pvp_friendly_target", (await EnterAsync(first, ally.Character)).ErrorCode);
    }

    [Fact]
    public async Task RestartRecoversIndividualVitalsAndPvpRewardsWithoutPveLootOrReplayHealing()
    {
        var human = await SeedAsync("HUMAN");
        var undead = await SeedAsync("UNDEAD");
        await using var host = CreateFactory();
        using var client = host.CreateClient();
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IContentSnapshotProvider>();
        var content = provider.GetCurrent();
        var bootstrap = scope.ServiceProvider.GetRequiredService<BootstrapService>();
        var factory = scope.ServiceProvider.GetRequiredService<CombatSessionFactory>();
        var first = await factory.CreateArenaPlayerAsync(await bootstrap.GetAsync(human.Account, CancellationToken.None), content, CancellationToken.None);
        var second = await factory.CreateArenaPlayerAsync(await bootstrap.GetAsync(undead.Account, CancellationToken.None), content, CancellationToken.None);
        var session = factory.CreateOpenWorldDuel(first, second, content);
        var winner = session.Snapshot(human.Character) with { Status = CombatSessionStatus.Victory };
        winner = winner with { Player = winner.Player with { Hp = 37 } };
        var loser = session.Snapshot(undead.Character) with { Status = CombatSessionStatus.Defeat };
        loser = loser with { Player = loser.Player with { Hp = 0 } };
        WorldPvpDefeat[] defeats = [new(undead.Character, winner.ServerTimeUtc, [new(human.Character, 100, 0)])];
        winner = winner with { WorldPvpDefeats = defeats };
        loser = loser with { WorldPvpDefeats = defeats };
        await using var db = postgres.CreateDbContext();
        var durability = new CombatDurabilityService(db, NullLogger<CombatDurabilityService>.Instance,
            host.Services.GetRequiredService<IServiceScopeFactory>(), contentProvider: provider);
        await durability.BeginAsync(human.Character, winner, CancellationToken.None);
        await durability.BeginAsync(undead.Character, loser, CancellationToken.None);
        await durability.RecordOpenWorldSnapshotsAsync([winner, loser], content, CancellationToken.None);
        Assert.Equal(2, await durability.RecoverInterruptedAsync(CancellationToken.None));
        Assert.Equal(37, (await db.CharacterVitals.AsNoTracking().SingleAsync(row => row.CharacterId == human.Character)).CurrentHp);
        Assert.True((await db.CharacterVitals.AsNoTracking().SingleAsync(row => row.CharacterId == undead.Character)).CurrentHp > 0);
        Assert.Equal("UNDEAD_STARTER_TOWN", (await db.CharacterLocations.AsNoTracking().SingleAsync(row => row.CharacterId == undead.Character)).LocationId);
        Assert.Empty(await db.CombatRewardGrants.ToArrayAsync());
        Assert.Equal(5, (await db.WorldPvpWallets.AsNoTracking().SingleAsync()).FieldHonor);
        Assert.Single(await db.WorldPvpRewardReceipts.ToArrayAsync());
        Assert.Empty(await db.ActiveCombatSessions.ToArrayAsync());
        var vitals = await db.CharacterVitals.SingleAsync(row => row.CharacterId == human.Character);
        vitals.BeginContext(10, 0, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<ICombatSessionFinalizer>().FinalizeAsync(human.Character, winner, content, CancellationToken.None);
        Assert.Equal(10, (await db.CharacterVitals.AsNoTracking().SingleAsync(row => row.CharacterId == human.Character)).CurrentHp);
        Assert.Single(await db.WorldPvpRewardReceipts.ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentSettlementIsIdempotentAndReversePairCannotFarmRewards()
    {
        var human = await SeedAsync("HUMAN");
        var undead = await SeedAsync("UNDEAD");
        var healer = await SeedAsync("HUMAN");
        await using var host = CreateFactory();
        using var client = Client(host, human.Account);
        var snapshot = await ProgressionSnapshotAsync(host, human, undead);
        var now = snapshot.ServerTimeUtc;
        snapshot = snapshot with { WorldPvpDefeats = [new(undead.Character, now,
            [new(human.Character, 100, 0), new(healer.Character, 0, 40)])] };
        await using var first = postgres.CreateDbContext();
        await using var second = postgres.CreateDbContext();
        await Task.WhenAll(new WorldPvpSettlementService(first, TimeProvider.System).ApplyAsync(snapshot, CancellationToken.None),
            new WorldPvpSettlementService(second, TimeProvider.System).ApplyAsync(snapshot, CancellationToken.None));
        await using var verify = postgres.CreateDbContext();
        Assert.Single(await verify.WorldPvpDeathReceipts.ToArrayAsync());
        Assert.Equal(2, await verify.WorldPvpRewardReceipts.CountAsync());
        Assert.Equal(5, (await verify.WorldPvpWallets.SingleAsync(w => w.CharacterId == human.Character)).FieldHonor);
        Assert.Equal(1, (await verify.WorldPvpStandings.SingleAsync(w => w.CharacterId == healer.Character)).Assists);
        var reverse = snapshot with { SessionId = Guid.NewGuid(), WorldPvpDefeats = [new(human.Character, now, [new(undead.Character, 100, 0)])] };
        await new WorldPvpSettlementService(first, TimeProvider.System).ApplyAsync(reverse, CancellationToken.None);
        Assert.False(await verify.WorldPvpWallets.AnyAsync(w => w.CharacterId == undead.Character));
        var repeated = snapshot with { SessionId = Guid.NewGuid() };
        await new WorldPvpSettlementService(first, TimeProvider.System).ApplyAsync(repeated, CancellationToken.None);
        Assert.Equal(2, await verify.WorldPvpRewardReceipts.CountAsync());
        await new WorldPvpReadService(verify, TimeProvider.System).GetAsync(human.Account, "HUMAN", CancellationToken.None);
        var overview = await client.GetFromJsonAsync<WorldPvpOverview>("/api/v1/world/pvp/progression?raceId=HUMAN");
        Assert.Equal(2, overview!.Leaders.Count);
        Assert.All(overview.Leaders, row => Assert.Equal("HUMAN", row.RaceId));
        Assert.Equal(5, overview.FieldHonor);
        Assert.Empty(await verify.ArenaStandings.ToArrayAsync());
        Assert.Empty(await verify.ArenaHonorWallets.ToArrayAsync());
    }

    [Fact]
    public async Task FastKillCountsOnceWithoutAwardingRatingOrCurrency()
    {
        var human = await SeedAsync("HUMAN");
        var undead = await SeedAsync("UNDEAD");
        await using var host = CreateFactory();
        var snapshot = await ProgressionSnapshotAsync(host, human, undead);
        snapshot = snapshot with { WorldPvpDefeats = [
            new(undead.Character, snapshot.ServerTimeUtc, [], human.Character)] };
        await using var db = postgres.CreateDbContext();
        var service = new WorldPvpSettlementService(db, TimeProvider.System);

        await service.ApplyAsync(snapshot, CancellationToken.None);
        await service.ApplyAsync(snapshot, CancellationToken.None);

        var killer = await db.WorldPvpStandings.AsNoTracking().SingleAsync(s => s.CharacterId == human.Character);
        var victim = await db.WorldPvpStandings.AsNoTracking().SingleAsync(s => s.CharacterId == undead.Character);
        Assert.Equal(1, killer.Victories);
        Assert.Equal(0, killer.Assists);
        Assert.Equal(0, killer.Rating);
        Assert.Equal(1, victim.Deaths);
        Assert.Single(await db.WorldPvpDeathReceipts.ToArrayAsync());
        Assert.Empty(await db.WorldPvpRewardReceipts.ToArrayAsync());
        Assert.Empty(await db.WorldPvpWallets.ToArrayAsync());
    }

    [Fact]
    public async Task RewardedAndRepeatKillsCountWithoutDuplicatingHonorOrVictory()
    {
        var human = await SeedAsync("HUMAN");
        var undead = await SeedAsync("UNDEAD");
        await using var host = CreateFactory();
        var snapshot = await ProgressionSnapshotAsync(host, human, undead);
        var defeat = new WorldPvpDefeat(undead.Character, snapshot.ServerTimeUtc,
            [new(human.Character, 100, 0)], human.Character);
        await using var db = postgres.CreateDbContext();
        var service = new WorldPvpSettlementService(db, TimeProvider.System);
        var first = snapshot with { WorldPvpDefeats = [defeat] };

        await service.ApplyAsync(first, CancellationToken.None);
        await service.ApplyAsync(first, CancellationToken.None);
        await service.ApplyAsync(first with { SessionId = Guid.NewGuid() }, CancellationToken.None);

        var killer = await db.WorldPvpStandings.AsNoTracking().SingleAsync(s => s.CharacterId == human.Character);
        Assert.Equal(2, killer.Victories);
        Assert.Equal(0, killer.Assists);
        Assert.Equal(10, killer.Rating);
        Assert.Single(await db.WorldPvpRewardReceipts.ToArrayAsync());
        Assert.Equal(5, (await db.WorldPvpWallets.AsNoTracking().SingleAsync()).FieldHonor);
    }

    [Fact]
    public async Task RatedVictoryReducesVictimRatingButRepeatPairCannotReduceAgain()
    {
        var human = await SeedAsync("HUMAN");
        var undead = await SeedAsync("UNDEAD");
        await using var host = CreateFactory();
        var snapshot = await ProgressionSnapshotAsync(host, human, undead);
        await using var db = postgres.CreateDbContext();
        var defender = new WorldPvpStanding(undead.Character, WorldPvpBalance.Season(snapshot.ServerTimeUtc));
        defender.Credit(true, 9);
        db.WorldPvpStandings.Add(defender);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new WorldPvpSettlementService(db, TimeProvider.System);
        var fight = snapshot with { WorldPvpDefeats = [
            new WorldPvpDefeat(undead.Character, snapshot.ServerTimeUtc,
                [new(human.Character, 100, 0)], human.Character)] };

        await service.ApplyAsync(fight, CancellationToken.None);
        await service.ApplyAsync(fight with { SessionId = Guid.NewGuid() }, CancellationToken.None);
        var saved = await db.WorldPvpStandings.AsNoTracking().SingleAsync(s => s.CharacterId == undead.Character);
        Assert.Equal(4, saved.Rating);
        Assert.Equal(2, saved.Deaths);
        Assert.Single(await db.WorldPvpRewardReceipts.ToArrayAsync());
    }

    [Fact]
    public async Task FuturePvPEventTimestampIsClampedInsteadOfBreakingFinalization()
    {
        var human = await SeedAsync("HUMAN");
        var undead = await SeedAsync("UNDEAD");
        await using var host = CreateFactory();
        var snapshot = await ProgressionSnapshotAsync(host, human, undead);
        var now = snapshot.ServerTimeUtc;
        var fight = snapshot with { WorldPvpDefeats = [
            new WorldPvpDefeat(undead.Character, now.AddSeconds(3),
                [new(human.Character, 100, 0)], human.Character)] };
        await using var db = postgres.CreateDbContext();

        await new WorldPvpSettlementService(db, new FixedTime(now)).ApplyAsync(fight, CancellationToken.None);

        // PostgreSQL timestamps have microsecond precision, while .NET DateTimeOffset has 100-ns ticks.
        Assert.InRange(((await db.WorldPvpDeathReceipts.AsNoTracking().SingleAsync()).AtUtc - now).Duration(),
            TimeSpan.Zero, TimeSpan.FromTicks(10));
        Assert.InRange(((await db.WorldPvpRewardReceipts.AsNoTracking().SingleAsync()).AtUtc - now).Duration(),
            TimeSpan.Zero, TimeSpan.FromTicks(10));
    }

    [Fact]
    public async Task GroupAssistsPayLessHonorAndRatingThanSoloKill()
    {
        var human = await SeedAsync("HUMAN");
        var healer = await SeedAsync("HUMAN");
        var undead = await SeedAsync("UNDEAD");
        await using var host = CreateFactory();
        var snapshot = await ProgressionSnapshotAsync(host, human, undead);
        var fight = snapshot with { WorldPvpDefeats = [
            new WorldPvpDefeat(undead.Character, snapshot.ServerTimeUtc,
                [new(human.Character, 100, 0), new(healer.Character, 0, 40)], human.Character)] };
        await using var db = postgres.CreateDbContext();

        await new WorldPvpSettlementService(db, TimeProvider.System).ApplyAsync(fight, CancellationToken.None);

        var killer = await db.WorldPvpStandings.AsNoTracking().SingleAsync(s => s.CharacterId == human.Character);
        var assist = await db.WorldPvpStandings.AsNoTracking().SingleAsync(s => s.CharacterId == healer.Character);
        Assert.Equal(10, killer.Rating);
        Assert.Equal(5, assist.Rating);
        Assert.Equal(5, (await db.WorldPvpWallets.AsNoTracking().SingleAsync(w => w.CharacterId == human.Character)).FieldHonor);
        Assert.Equal(3, (await db.WorldPvpWallets.AsNoTracking().SingleAsync(w => w.CharacterId == healer.Character)).FieldHonor);
    }

    [Fact]
    public async Task WorldPvpShopRetriesCannotDuplicateItemsOrOverspendEitherCurrency()
    {
        var human = await SeedAsync("HUMAN");
        await using var host = CreateFactory();
        using var client = Client(host, human.Account);
        var offers = await client.GetFromJsonAsync<WorldPvpOffer[]>("/api/v1/world/pvp/shop");
        var offer = offers!.First(o => o.RequiredLevel <= 60);
        await using (var db = postgres.CreateDbContext())
        {
            var wallet = new WorldPvpWallet(human.Character);
            wallet.Grant(offer.FieldHonorPrice, offer.TokenPrice);
            db.WorldPvpWallets.Add(wallet);
            await db.SaveChangesAsync();
        }
        Guid mutation = Guid.NewGuid();
        var request = new { itemId = offer.Id, tokens = false, mutationId = mutation };
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/world/pvp/shop/buy", request),
            client.PostAsJsonAsync("/api/v1/world/pvp/shop/buy", request));
        foreach (var response in responses)
        {
            response.EnsureSuccessStatusCode();
            Assert.True((await response.Content.ReadFromJsonAsync<WorldPvpPurchaseResult>())!.Succeeded);
            response.Dispose();
        }
        using var conflict = await client.PostAsJsonAsync("/api/v1/world/pvp/shop/buy", new { itemId = offer.Id, tokens = true, mutationId = mutation });
        Assert.Equal("mutation_conflict", (await conflict.Content.ReadFromJsonAsync<WorldPvpPurchaseResult>())!.ErrorCode);
        using var tokenBuy = await client.PostAsJsonAsync("/api/v1/world/pvp/shop/buy", new { itemId = offer.Id, tokens = true, mutationId = Guid.NewGuid() });
        Assert.True((await tokenBuy.Content.ReadFromJsonAsync<WorldPvpPurchaseResult>())!.Succeeded);
        using var poor = await client.PostAsJsonAsync("/api/v1/world/pvp/shop/buy", new { itemId = offer.Id, tokens = false, mutationId = Guid.NewGuid() });
        Assert.Equal("not_enough_currency", (await poor.Content.ReadFromJsonAsync<WorldPvpPurchaseResult>())!.ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(2, await verify.CharacterItems.CountAsync(i => i.CharacterId == human.Character));
        var remaining = await verify.WorldPvpWallets.SingleAsync();
        Assert.Equal(0, remaining.FieldHonor);
        Assert.Equal(0, remaining.BloodyTokens);
    }

    [Fact]
    public async Task DailyCapAndPairCooldownPersistAcrossSessionsAndResetAfterUtcDay()
    {
        var human = await SeedAsync("HUMAN");
        var enemy = await SeedAsync("UNDEAD");
        await using var host = CreateFactory();
        using var client = Client(host, human.Account);
        var snapshot = await ProgressionSnapshotAsync(host, human, enemy);
        var now = snapshot.ServerTimeUtc;
        await using var db = postgres.CreateDbContext();
        var service = new WorldPvpSettlementService(db, TimeProvider.System);
        for (int i = 0; i < WorldPvpBalance.DailyRewardedOpponents + 1; i++)
        {
            var target = i == 0 ? enemy : await SeedAsync("UNDEAD");
            await service.ApplyAsync(snapshot with { SessionId = Guid.NewGuid(),
                WorldPvpDefeats = [new(target.Character, now, [new(human.Character, 100, 0)])] }, CancellationToken.None);
        }
        Assert.Equal(20, await db.WorldPvpRewardReceipts.CountAsync());
        Assert.Equal(100, (await db.WorldPvpWallets.AsNoTracking().SingleAsync()).FieldHonor);
        var next = now.AddHours(25);
        await new WorldPvpSettlementService(db, new FixedTime(next)).ApplyAsync(snapshot with {
            SessionId = Guid.NewGuid(), ServerTimeUtc = next,
            WorldPvpDefeats = [new(enemy.Character, next, [new(human.Character, 100, 0)])] }, CancellationToken.None);
        Assert.Equal(21, await db.WorldPvpRewardReceipts.CountAsync());
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static async Task<CombatSessionSnapshot> ProgressionSnapshotAsync(WebApplicationFactory<Program> host,
        (Guid Account, Guid Character) human, (Guid Account, Guid Character) undead)
    {
        using var scope = host.Services.CreateScope();
        var content = scope.ServiceProvider.GetRequiredService<IContentSnapshotProvider>().GetCurrent();
        var bootstrap = scope.ServiceProvider.GetRequiredService<BootstrapService>();
        var factory = scope.ServiceProvider.GetRequiredService<CombatSessionFactory>();
        var first = await factory.CreateArenaPlayerAsync(await bootstrap.GetAsync(human.Account, CancellationToken.None), content, CancellationToken.None);
        var second = await factory.CreateArenaPlayerAsync(await bootstrap.GetAsync(undead.Account, CancellationToken.None), content, CancellationToken.None);
        return factory.CreateOpenWorldDuel(first, second, content).Snapshot(human.Character)
            with { Status = CombatSessionStatus.Victory };
    }

    private static async Task<CombatUpdateResponse> EnterAsync(HttpClient client, Guid target, bool attack = true)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/world/pvp/enter", new { targetCharacterId = target, attackPlayer = attack });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CombatUpdateResponse>())!;
    }

    private WebApplicationFactory<Program> CreateFactory(bool freezeCombat = false) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            // Admission assertions must not race live auto-attacks on slow CI hosts.
            if (freezeCombat)
                builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new FixedTime(DateTimeOffset.UtcNow)));
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
            builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
            builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
            builder.UseSetting("Authentication:SigningKey", "open-world-pvp-test-signing-key-with-more-than-32-bytes");
            builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
            builder.UseSetting("Authentication:Development:Enabled", "false");
        });

    [Fact]
    public async Task ConfiguredKillAndAssistGoldAreAtomicAndCannotBeReplayedOrPairFarmed()
    {
        var human = await SeedAsync("HUMAN");
        var enemy = await SeedAsync("UNDEAD");
        var ally = await SeedAsync("HUMAN");
        await using var host = CreateFactory();
        using var client = host.CreateClient();
        var snapshot = await ProgressionSnapshotAsync(host, human, enemy);
        snapshot = snapshot with { WorldPvpDefeats = [new(enemy.Character, snapshot.ServerTimeUtc,
            [new(human.Character, 100, 0), new(ally.Character, 0, 40)], human.Character)] };
        var rewards = new WorldPvpRewardProfile(25000, 5000);
        await using var first = postgres.CreateDbContext();
        await using var second = postgres.CreateDbContext();
        await Task.WhenAll(new WorldPvpSettlementService(first, TimeProvider.System).ApplyAsync(snapshot, CancellationToken.None, rewards),
            new WorldPvpSettlementService(second, TimeProvider.System).ApplyAsync(snapshot, CancellationToken.None, rewards));
        await new WorldPvpSettlementService(first, TimeProvider.System).ApplyAsync(snapshot with { SessionId = Guid.NewGuid() }, CancellationToken.None, rewards);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(25000, (await verify.Characters.SingleAsync(c => c.Id == human.Character)).Gold);
        Assert.Equal(5000, (await verify.Characters.SingleAsync(c => c.Id == ally.Character)).Gold);
        Assert.Equal(30000, await verify.WorldPvpRewardReceipts.SumAsync(r => r.GoldEarned));
    }

    private static HttpClient Client(WebApplicationFactory<Program> host, Guid account)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            host.Services.GetRequiredService<JwtTokenIssuer>().Issue(account, 1).AccessToken);
        return client;
    }

    private async Task<(Guid Account, Guid Character)> SeedAsync(string race, string location = "DEAD_REACHES", int level = 60)
    {
        await using var db = postgres.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        Guid account = Guid.CreateVersion7(), id = Guid.CreateVersion7();
        db.Accounts.Add(new Account(account, Random.Shared.NextInt64(1, long.MaxValue), now));
        var character = new Character(id, account, Guid.CreateVersion7(), "Traveler", $"PVP{id:N}"[..16], race, "MALE", "WARRIOR", now);
        character.SetLevel(level);
        db.Characters.Add(character);
        db.CharacterLocations.Add(new CharacterLocation(id, location, 1, now));
        db.CharacterVitals.Add(new CharacterVitals(id, 100, 0, now, now));
        await db.SaveChangesAsync();
        return (account, id);
    }
}
