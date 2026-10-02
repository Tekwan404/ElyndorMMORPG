using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.WorldBosses;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elyndor.IntegrationTests.WorldBosses;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class WorldBossCombatDamageObserverTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BoundCombatDamageFlowsIntoGlobalHealthAndReplayIsSafe()
    {
        Seed seed = await SeedAsync();

        await using ServiceProvider provider = Services();
        WorldBossCombatDamageObserver observer = new(
            provider.GetRequiredService<IServiceScopeFactory>());
        CombatEvent damage = Damage(
            seed.PlayerActorId,
            seed.BossActorId,
            amount: 4_000m,
            sequence: 42);

        await observer.ObserveAsync(
            CreateSession(seed),
            [Participant(seed)],
            [damage],
            CancellationToken.None);
        await observer.ObserveAsync(
            CreateSession(seed),
            [Participant(seed)],
            [damage],
            CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(6_000m, (await verify.WorldBossSpawns.SingleAsync()).CurrentHealth);
        Assert.Equal(4_000m, (await verify.WorldBossContributions.SingleAsync()).Damage);
        Assert.Single(await verify.WorldBossDamageMutations.ToListAsync());
    }

    [Fact]
    public async Task OnlyPlayerDamageAgainstBoundBossActorCounts()
    {
        Seed seed = await SeedAsync();
        Guid addActorId = Guid.CreateVersion7();
        Guid monsterActorId = Guid.CreateVersion7();

        await using ServiceProvider provider = Services();
        WorldBossCombatDamageObserver observer = new(
            provider.GetRequiredService<IServiceScopeFactory>());

        await observer.ObserveAsync(
            CreateSession(seed),
            [Participant(seed)],
            [
                Damage(seed.PlayerActorId, addActorId, 1_000m, 1),
                Damage(monsterActorId, seed.BossActorId, 2_000m, 2),
                Damage(seed.PlayerActorId, seed.BossActorId, 3_000m, 3)
            ],
            CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(7_000m, (await verify.WorldBossSpawns.SingleAsync()).CurrentHealth);
        Assert.Equal(3_000m, (await verify.WorldBossContributions.SingleAsync()).Damage);
        Assert.Single(await verify.WorldBossDamageMutations.ToListAsync());
    }

    [Fact]
    public async Task PartyContributionUsesMembershipAtDamageTimeInsteadOfBindingSnapshot()
    {
        Seed seed = await SeedAsync();
        Guid partyId = Guid.CreateVersion7();

        await using (GameDbContext arrange = postgres.CreateDbContext())
        {
            arrange.Parties.Add(Party.Create(
                partyId,
                Guid.CreateVersion7(),
                seed.CharacterId,
                Now));
            await arrange.SaveChangesAsync();
        }

        await using ServiceProvider provider = Services();
        WorldBossCombatDamageObserver observer = new(
            provider.GetRequiredService<IServiceScopeFactory>());

        await observer.ObserveAsync(
            CreateSession(seed),
            [Participant(seed)],
            [Damage(seed.PlayerActorId, seed.BossActorId, 2_500m, 7)],
            CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        WorldBossPartyContribution party = await verify.WorldBossPartyContributions.SingleAsync();
        Assert.Equal(partyId, party.PartyId);
        Assert.Equal(2_500m, party.Damage);
    }

    [Fact]
    public async Task CompanionDamageIsAttributedToOwningCharacter()
    {
        Seed seed = await SeedAsync();
        Guid companionActorId = Guid.CreateVersion7();

        await using ServiceProvider provider = Services();
        WorldBossCombatDamageObserver observer = new(
            provider.GetRequiredService<IServiceScopeFactory>());
        CombatSession session = CreateSession(seed, companionActorId);

        await observer.ObserveAsync(
            session,
            [Participant(seed)],
            [Damage(companionActorId, seed.BossActorId, 1_750m, 11)],
            CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        WorldBossSpawn spawn = await verify.WorldBossSpawns.SingleAsync();
        WorldBossContribution contribution =
            await verify.WorldBossContributions.SingleAsync();

        Assert.Equal(8_250m, spawn.CurrentHealth);
        Assert.Equal(seed.CharacterId, contribution.CharacterId);
        Assert.Equal(1_750m, contribution.Damage);
    }

    [Fact]
    public async Task UnboundOrdinaryCombatIsIgnored()
    {
        Seed seed = await SeedAsync();

        await using ServiceProvider provider = Services();
        WorldBossCombatDamageObserver observer = new(
            provider.GetRequiredService<IServiceScopeFactory>());
        await observer.ObserveAsync(
            Guid.CreateVersion7(),
            [Participant(seed)],
            [Damage(seed.PlayerActorId, seed.BossActorId, 5_000m, 1)],
            CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(10_000m, (await verify.WorldBossSpawns.SingleAsync()).CurrentHealth);
        Assert.Empty(await verify.WorldBossContributions.ToListAsync());
        Assert.Empty(await verify.WorldBossDamageMutations.ToListAsync());
    }

    private ServiceProvider Services()
    {
        ServiceCollection services = new();
        services.AddScoped<GameDbContext>(_ => postgres.CreateDbContext());
        services.AddSingleton<TimeProvider>(new FixedTime(Now.AddMinutes(1)));
        services.AddScoped<WorldBossDamageService>();
        return services.BuildServiceProvider();
    }

    private async Task<Seed> SeedAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid spawnId = Guid.CreateVersion7();
        Guid sessionId = Guid.CreateVersion7();
        Guid bossActorId = Guid.CreateVersion7();

        await using GameDbContext db = postgres.CreateDbContext();
        db.Accounts.Add(new Account(
            accountId,
            Random.Shared.NextInt64(1, long.MaxValue),
            Now));
        db.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "BridgeTester",
            $"WB{characterId:N}"[..16].ToUpperInvariant(),
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        db.WorldBossSpawns.Add(new WorldBossSpawn(
            spawnId,
            "WORLD_BOSS_ASH_ARCHON",
            10_000m,
            1,
            Now,
            Now.AddMinutes(30),
            "test-content",
            "test-balance"));
        db.WorldBossCombatSessions.Add(new WorldBossCombatSessionBinding(
            sessionId,
            spawnId,
            bossActorId,
            null,
            Now));
        await db.SaveChangesAsync();

        return new Seed(accountId, characterId, sessionId, characterId, bossActorId);
    }

    private static CombatSession CreateSession(
        Seed seed,
        Guid? companionActorId = null)
    {
        CombatStats stats = new(
            30,
            Accuracy: 100,
            Dodge: 0,
            CriticalChance: 0,
            CriticalDamage: 1.5m,
            Armor: 0,
            MagicResistance: 0,
            ArmorPenetration: 0,
            MagicPenetration: 0,
            AttackPower: 100,
            SpellPower: 0);
        CombatParticipantDefinition player = new(
            new CombatActorState(
                seed.PlayerActorId,
                1_000,
                1_000,
                100,
                100,
                stats),
            CombatActorKind.Player,
            "WARRIOR",
            "BridgeTester",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromSeconds(30), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal));
        CombatParticipantDefinition boss = new(
            new CombatActorState(
                seed.BossActorId,
                10_000,
                10_000,
                0,
                0,
                stats),
            CombatActorKind.Monster,
            "WORLD_BOSS_ASH_ARCHON_L30",
            "Архон Пепла",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(30), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
            MonsterRank: MonsterRank.Boss);
        CombatParticipantDefinition? companion = companionActorId is { } actorId
            ? new CombatParticipantDefinition(
                new CombatActorState(actorId, 500, 500, 0, 0, stats),
                CombatActorKind.Companion,
                "ARCHER_STARTER_PREDATOR",
                "Companion",
                "NONE",
                new AutoAttackProfile(TimeSpan.FromSeconds(30), 10, 0, 0),
                new HashSet<string>(StringComparer.Ordinal))
            : null;

        return new CombatSession(
            seed.SessionId,
            player,
            boss,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            new MonsterAiProfile("WB_TEST_AI", []),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 64).ToArray()),
            Now,
            "test-content",
            "test-balance",
            companion: companion,
            playerAccountId: seed.AccountId);
    }

    private static CombatParticipantSnapshot Participant(Seed seed) =>
        new(
            seed.AccountId,
            seed.CharacterId,
            seed.PlayerActorId,
            CombatParticipantStatus.Active,
            Now,
            Now,
            null,
            null);

    private static CombatEvent Damage(
        Guid sourceActorId,
        Guid targetActorId,
        decimal amount,
        long sequence) =>
        new(
            CombatEventType.DamageDealt,
            Now.AddSeconds(sequence),
            targetActorId,
            Amount: amount,
            SourceActorId: sourceActorId,
            TargetActorId: targetActorId,
            Sequence: sequence);

    private sealed record Seed(
        Guid AccountId,
        Guid CharacterId,
        Guid SessionId,
        Guid PlayerActorId,
        Guid BossActorId);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
