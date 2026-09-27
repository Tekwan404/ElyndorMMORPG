using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Progression;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Progression;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CombatRewardAtomicityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 27, 18, 15, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LootRollsBackEverythingWhenFailureOccursBeforeCommitThenSameSessionGrantsOnce()
    {
        Guid characterId = await CreateCharacterAsync();
        Guid sessionId = Guid.CreateVersion7();
        CombatSessionSnapshot snapshot = VictorySnapshot(sessionId);

        await using (GameDbContext faultingContext = CreateFaultingContext())
        {
            CombatRewardService service = await CreateServiceAsync(faultingContext);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyVictoryAsync(
                characterId,
                snapshot,
                CancellationToken.None));
        }

        await using (GameDbContext afterFailure = postgres.CreateDbContext())
        {
            Character character = await afterFailure.Characters.AsNoTracking()
                .SingleAsync(item => item.Id == characterId);
            Assert.Equal(0, character.Experience);
            Assert.Equal(0, character.Gold);
            Assert.Equal(0, await afterFailure.CombatRewardGrants.AsNoTracking()
                .CountAsync(item => item.CharacterId == characterId));
            Assert.Equal(0, await afterFailure.CharacterItems.AsNoTracking()
                .CountAsync(item => item.CharacterId == characterId));
            Assert.Equal(0, await afterFailure.PendingLootItems.AsNoTracking()
                .CountAsync(item => item.CharacterId == characterId));
        }

        CombatRewardApplicationResult granted;
        await using (GameDbContext retryContext = postgres.CreateDbContext())
        {
            CombatRewardService service = await CreateServiceAsync(retryContext);
            granted = await service.ApplyVictoryAsync(
                characterId,
                snapshot,
                CancellationToken.None);
            CombatRewardApplicationResult replay = await service.ApplyVictoryAsync(
                characterId,
                snapshot,
                CancellationToken.None);

            Assert.True(granted.Granted);
            Assert.False(replay.Granted);
            Assert.Equal(granted.XpEarned, replay.XpEarned);
            Assert.Equal(granted.GoldEarned, replay.GoldEarned);
        }

        await using GameDbContext verify = postgres.CreateDbContext();
        Character persisted = await verify.Characters.AsNoTracking()
            .SingleAsync(item => item.Id == characterId);
        Assert.Equal(granted.XpEarned, persisted.Experience);
        Assert.Equal(granted.GoldEarned, persisted.Gold);
        Assert.Equal(1, await verify.CombatRewardGrants.AsNoTracking()
            .CountAsync(item =>
                item.CharacterId == characterId
                && item.CombatSessionId == sessionId));

        int persistedLootQuantity = await verify.CharacterItems.AsNoTracking()
            .Where(item => item.CharacterId == characterId)
            .SumAsync(item => item.Quantity);
        int pendingLootQuantity = await verify.PendingLootItems.AsNoTracking()
            .Where(item => item.CharacterId == characterId)
            .SumAsync(item => item.Quantity);
        Assert.Equal(
            granted.Items.Sum(item => item.Quantity),
            persistedLootQuantity + pendingLootQuantity);
    }

    private async Task<Guid> CreateCharacterAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(
            accountId,
            Random.Shared.NextInt64(1, long.MaxValue),
            Now));
        context.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "LootAtomic",
            $"LOOT{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        context.CharacterVitals.Add(new CharacterVitals(
            characterId,
            100,
            0,
            Now.AddMinutes(-1),
            Now.AddMinutes(-1)));
        await context.SaveChangesAsync();
        return characterId;
    }

    private static async Task<CombatRewardService> CreateServiceAsync(GameDbContext context)
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        TimeProvider timeProvider = new FixedTimeProvider(Now);
        InventoryEquipmentService inventory = new(context, content, timeProvider);
        CharacterDerivedStateService derived = new(context, content, inventory);
        return new CombatRewardService(
            context,
            content,
            derived,
            new FixedRandomFactory(),
            timeProvider);
    }

    private GameDbContext CreateFaultingContext()
    {
        DbContextOptions<GameDbContext> options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(postgres.ConnectionString, builder => builder.EnableRetryOnFailure())
            .AddInterceptors(new ThrowOnceAfterSaveChangesInterceptor())
            .Options;
        return new GameDbContext(options);
    }

    private static CombatSessionSnapshot VictorySnapshot(Guid sessionId)
    {
        CombatActorSnapshot player = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Player,
            "WARRIOR",
            "LootAtomic");
        CombatActorSnapshot enemy = Actor(
            Guid.CreateVersion7(),
            CombatActorKind.Monster,
            "FOREST_WOLF_L1",
            "Forest Wolf");
        return new CombatSessionSnapshot(
            sessionId,
            1,
            CombatSessionStatus.Victory,
            Now,
            player,
            enemy);
    }

    private static CombatActorSnapshot Actor(
        Guid id,
        CombatActorKind kind,
        string definitionId,
        string name) =>
        new(
            id,
            kind,
            definitionId,
            name,
            0,
            100,
            "NONE",
            0,
            0,
            false,
            null,
            new Dictionary<string, DateTimeOffset>(),
            new HashSet<string>(),
            [],
            []);

    private sealed class FixedRandomFactory : IGameRandomFactory
    {
        public IGameRandom Create() =>
            new SequenceGameRandom(Enumerable.Repeat(0m, 64).ToArray());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}