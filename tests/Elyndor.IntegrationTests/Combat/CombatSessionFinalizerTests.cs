using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Progression;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elyndor.IntegrationTests.Combat;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CombatSessionFinalizerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReplayedVictoryFinalizationDoesNotUndoLevelUpHealing()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid sessionId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(
                accountId,
                Random.Shared.NextInt64(1, long.MaxValue),
                Now));
            Character character = new(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "Arthas",
                $"ARTHAS{characterId:N}"[..16],
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now);
            character.SetExperience(90);
            setup.Characters.Add(character);
            setup.CharacterVitals.Add(new CharacterVitals(
                characterId,
                40,
                0,
                Now,
                Now));
            setup.CharacterLocations.Add(new CharacterLocation(
                characterId,
                "WHISPERING_FOREST",
                1,
                Now));
            await setup.SaveChangesAsync();
        }

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ServiceCollection services = new();
        services.AddScoped<GameDbContext>(_ => postgres.CreateDbContext());
        services.AddSingleton(content);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddSingleton<IGameRandomFactory>(new FixedRandomFactory());
        services.AddScoped<InventoryEquipmentService>();
        services.AddScoped<CharacterDerivedStateService>();
        services.AddScoped<CharacterAbilityCooldownStore>();
        services.AddScoped<CombatRewardService>();

        await using ServiceProvider provider = services.BuildServiceProvider();
        CombatSessionFinalizer finalizer = new(
            provider.GetRequiredService<IServiceScopeFactory>());
        CombatSessionSnapshot snapshot = VictorySnapshot(sessionId);

        CombatRewardApplicationResult? first = await finalizer.FinalizeAsync(
            characterId,
            snapshot,
            CancellationToken.None);

        Assert.NotNull(first);
        Assert.True(first.Granted);

        await using (GameDbContext verifyFirst = postgres.CreateDbContext())
        {
            Character firstCharacter = await verifyFirst.Characters.AsNoTracking().SingleAsync();
            CharacterVitals firstVitals = await verifyFirst.CharacterVitals.AsNoTracking().SingleAsync();
            Assert.Equal(2, firstCharacter.Level);
            Assert.Equal(170, firstVitals.CurrentHp);
            CharacterAbilityCooldown cooldown = await verifyFirst.CharacterAbilityCooldowns
                .AsNoTracking()
                .SingleAsync();
            Assert.Equal("STRIKE", cooldown.AbilityId);
            Assert.Equal(Now.AddSeconds(20), cooldown.ReadyAtUtc);
        }

        CombatRewardApplicationResult? replay = await finalizer.FinalizeAsync(
            characterId,
            snapshot,
            CancellationToken.None);

        Assert.NotNull(replay);
        Assert.False(replay.Granted);

        await using GameDbContext verifyReplay = postgres.CreateDbContext();
        CharacterVitals replayVitals = await verifyReplay.CharacterVitals.AsNoTracking().SingleAsync();
        Assert.Equal(170, replayVitals.CurrentHp);
    }

    [Fact]
    public async Task TrainingEnemyAnywhereInCollectionSkipsDurableFinalization()
    {
        ServiceCollection services = new();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CombatSessionFinalizer finalizer = new(
            provider.GetRequiredService<IServiceScopeFactory>());

        CombatActorSnapshot player = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Player,
            "WARRIOR",
            "Arthas",
            hp: 100,
            maxHp: 100,
            resource: 0,
            maxResource: 100,
            cooldowns: new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
            {
                ["STRIKE"] = Now.AddSeconds(20)
            });
        CombatActorSnapshot wolf = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Monster,
            "FOREST_WOLF_L1",
            "Wolf",
            hp: 100,
            maxHp: 100,
            resource: 0,
            maxResource: 0);
        CombatActorSnapshot training = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Monster,
            CombatSessionFactory.TrainingDummyId,
            "Training Dummy",
            hp: 10_000,
            maxHp: 10_000,
            resource: 0,
            maxResource: 0);
        CombatSessionSnapshot snapshot = new(
            Guid.CreateVersion7(),
            1,
            CombatSessionStatus.Cancelled,
            Now,
            player,
            wolf,
            Enemies: [wolf, training],
            SelectedTargetActorId: wolf.ActorId);

        CombatRewardApplicationResult? result = await finalizer.FinalizeAsync(
            Guid.CreateVersion7(),
            snapshot,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefeatRespawnsButFleeKeepsLocationAndVitals(bool fled)
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(
                accountId,
                Random.Shared.NextInt64(1, long.MaxValue),
                Now));
            Character character = new(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "Jaina",
                $"JAINA{characterId:N}"[..16],
                "HUMAN",
                "FEMALE",
                "MAGE",
                Now);
            character.SetLevel(60);
            setup.Characters.Add(character);
            setup.CharacterVitals.Add(new CharacterVitals(
                characterId,
                0,
                0,
                Now,
                Now));
            setup.CharacterLocations.Add(new CharacterLocation(
                characterId,
                "WHISPERING_FOREST",
                1,
                Now));
            await setup.SaveChangesAsync();
        }

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ServiceCollection services = new();
        services.AddScoped<GameDbContext>(_ => postgres.CreateDbContext());
        services.AddSingleton(content);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddScoped<InventoryEquipmentService>();
        services.AddScoped<CharacterDerivedStateService>();
        services.AddScoped<CharacterAbilityCooldownStore>();

        await using ServiceProvider provider = services.BuildServiceProvider();
        CombatSessionFinalizer finalizer = new(
            provider.GetRequiredService<IServiceScopeFactory>());

        CombatSessionSnapshot terminal = DefeatSnapshot();
        if (fled)
        {
            terminal = terminal with
            {
                Player = terminal.Player with { ActorId = characterId, Hp = 50, Resource = 20 },
                ParticipantRoster = [new CombatParticipantSnapshot(accountId, characterId, characterId,
                    CombatParticipantStatus.Fled, Now, Now, Now, null)]
            };
        }
        await finalizer.FinalizeAsync(
            characterId,
            terminal,
            CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterVitals vitals = await verify.CharacterVitals.AsNoTracking().SingleAsync();
        CharacterLocation location = await verify.CharacterLocations.AsNoTracking().SingleAsync();
        Assert.Equal(fled ? 20 : 1040, vitals.CurrentResource);
        Assert.True(vitals.CurrentHp > 0);
        Assert.Equal(fled ? "WHISPERING_FOREST" : "STARTER_TOWN", location.LocationId);
        if (fled) Assert.Equal(50, vitals.CurrentHp);
    }

    private static CombatSessionSnapshot VictorySnapshot(Guid sessionId)
    {
        CombatActorSnapshot player = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Player,
            "WARRIOR",
            "Arthas",
            hp: 25,
            maxHp: 150,
            resource: 0,
            maxResource: 100,
            cooldowns: new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
            {
                ["STRIKE"] = Now.AddSeconds(20)
            });
        CombatActorSnapshot enemy = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Monster,
            "FOREST_WOLF_L1",
            "Wolf",
            hp: 0,
            maxHp: 100,
            resource: 0,
            maxResource: 0);
        return new CombatSessionSnapshot(
            sessionId,
            1,
            CombatSessionStatus.Victory,
            Now,
            player,
            enemy);
    }

    private static CombatSessionSnapshot DefeatSnapshot()
    {
        CombatActorSnapshot player = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Player,
            "MAGE",
            "Jaina",
            hp: 0,
            maxHp: 100,
            resource: 0,
            maxResource: 100);
        CombatActorSnapshot enemy = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Monster,
            "FOREST_WOLF_L1",
            "Wolf",
            hp: 100,
            maxHp: 100,
            resource: 0,
            maxResource: 0);
        return new CombatSessionSnapshot(
            Guid.CreateVersion7(),
            1,
            CombatSessionStatus.Defeat,
            Now,
            player,
            enemy);
    }

    private static CombatActorSnapshot Actor(
        Guid id,
        CombatActorKind kind,
        string definitionId,
        string name,
        decimal hp,
        decimal maxHp,
        decimal resource,
        decimal maxResource,
        IReadOnlyDictionary<string, DateTimeOffset>? cooldowns = null) =>
        new(
            id,
            kind,
            definitionId,
            name,
            hp,
            maxHp,
            "NONE",
            resource,
            maxResource,
            false,
            null,
            cooldowns ?? new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(),
            [],
            []);

    private sealed class FixedRandomFactory : IGameRandomFactory
    {
        public IGameRandom Create() =>
            new SequenceGameRandom(Enumerable.Repeat(0m, 256).ToArray());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
