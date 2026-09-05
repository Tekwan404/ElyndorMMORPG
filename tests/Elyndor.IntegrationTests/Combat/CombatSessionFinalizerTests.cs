using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
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
    public async Task DefeatedLevel60MageRespawnsWithScaledMana()
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

        await using ServiceProvider provider = services.BuildServiceProvider();
        CombatSessionFinalizer finalizer = new(
            provider.GetRequiredService<IServiceScopeFactory>());

        await finalizer.FinalizeAsync(
            characterId,
            DefeatSnapshot(),
            CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        CharacterVitals vitals = await verify.CharacterVitals.AsNoTracking().SingleAsync();
        CharacterLocation location = await verify.CharacterLocations.AsNoTracking().SingleAsync();
        Assert.Equal(1040, vitals.CurrentResource);
        Assert.True(vitals.CurrentHp > 0);
        Assert.Equal("STARTER_TOWN", location.LocationId);
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
            maxResource: 100);
        CombatActorSnapshot enemy = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Monster,
            "WOLF",
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
            "WOLF",
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
        decimal maxResource) =>
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
            new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(),
            [],
            []);

    private sealed class FixedRandomFactory : IGameRandomFactory
    {
        public IGameRandom Create() =>
            new SequenceGameRandom(0, 0, 0, 0, 0, 0);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
