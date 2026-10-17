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
        await using var host = CreateFactory();
        using var first = Client(host, attacker.Account);
        using var second = Client(host, target.Account);
        using var third = Client(host, ally.Account);
        await second.GetAsync("/api/v1/world/pvp/players");
        var players = await first.GetFromJsonAsync<OpenWorldPlayersResult>("/api/v1/world/pvp/players");
        Assert.Contains(players!.Players, player => player.CharacterId == target.Character);
        var started = await EnterAsync(first, target.Character);
        Assert.True(started.Succeeded, started.ErrorCode);
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
        var joined = await EnterAsync(third, attacker.Character, attack: false);
        Assert.True(joined.Succeeded, joined.ErrorCode);
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
    public async Task RestartRecoversIndividualVitalsAndDefeatWithoutPvpLootOrReplayHealing()
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
        Assert.Empty(await db.ActiveCombatSessions.ToArrayAsync());
        var vitals = await db.CharacterVitals.SingleAsync(row => row.CharacterId == human.Character);
        vitals.BeginContext(10, 0, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<ICombatSessionFinalizer>().FinalizeAsync(human.Character, winner, content, CancellationToken.None);
        Assert.Equal(10, (await db.CharacterVitals.AsNoTracking().SingleAsync(row => row.CharacterId == human.Character)).CurrentHp);
    }

    private static async Task<CombatUpdateResponse> EnterAsync(HttpClient client, Guid target, bool attack = true)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/world/pvp/enter", new { targetCharacterId = target, attackPlayer = attack });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CombatUpdateResponse>())!;
    }

    private WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
            builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
            builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
            builder.UseSetting("Authentication:SigningKey", "open-world-pvp-test-signing-key-with-more-than-32-bytes");
            builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
            builder.UseSetting("Authentication:Development:Enabled", "false");
        });

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
        await db.SaveChangesAsync();
        return (account, id);
    }
}
