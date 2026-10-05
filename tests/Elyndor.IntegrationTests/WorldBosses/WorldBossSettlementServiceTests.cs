using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.WorldBosses;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.WorldBosses;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class WorldBossSettlementServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 2, 14, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SettlementPublishesPersonalRealtimeDeliveryForIneligibleContributor()
    {
        GameContentPackage package = await LoadContentAsync();
        Seed seed = await SeedDefeatedAsync(package, 4_999m);
        var publisher = new CapturingPublisher();

        await using (GameDbContext db = postgres.CreateDbContext())
        {
            WorldBossSettlementBatchResult result =
                await CreateService(db, package, publisher).SettleAsync(seed.SpawnId, default);
            Assert.True(result.Succeeded, result.ErrorCode);
            Assert.Empty(result.Rewards);
        }

        Assert.Equal(seed.SpawnId, publisher.SettledSpawnId);
        Assert.Equal(Now.AddMinutes(1), publisher.SettledAtUtc);
        WorldBossRewardDelivery delivery = Assert.Single(publisher.Deliveries);
        Assert.Equal(seed.AccountId, delivery.AccountId);
        Assert.Equal(seed.CharacterId, delivery.CharacterId);
        Assert.Equal(4_999m, delivery.Contribution);
        Assert.Null(delivery.Reward);
    }

    [Fact]
    public async Task SettlementGrantsTopFiveRewardExactlyOnce()
    {
        GameContentPackage package = await LoadContentAsync();
        Seed seed = await SeedDefeatedAsync(package, 50_000m);

        WorldBossSettlementBatchResult first;
        await using (GameDbContext db = postgres.CreateDbContext())
        {
            first = await CreateService(db, package).SettleAsync(seed.SpawnId, default);
        }

        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.False(first.WasReplay);
        WorldBossSettlementCharacterResult reward = Assert.Single(first.Rewards);
        Assert.Equal(seed.CharacterId, reward.CharacterId);
        Assert.Equal(50_000m, reward.Contribution);
        Assert.Equal(WorldBossRewardTier.Top5, reward.Tier);
        Assert.Equal(1, reward.Rank);
        Assert.Equal(1, reward.EligibleParticipants);
        Assert.Equal(100m, reward.Percentile);
        Assert.Equal(0, reward.ChestCount);
        Assert.Equal(2, reward.EnhancedChestCount);
        Assert.Equal(200_000, reward.Experience);
        Assert.Equal(1_000, reward.BossGold);
        Assert.Equal(0, reward.ChestGold);
        WorldBossLootItemResult chest = Assert.Single(reward.Items);
        Assert.Equal("WORLD_BOSS_ASH_ARCHON_TOP5_CHEST", chest.ItemId);
        Assert.Equal(2, chest.Quantity);

        CharacterState afterFirst = await ReadCharacterStateAsync(seed.CharacterId);
        Assert.Equal(1_000, afterFirst.Gold);
        Assert.Equal(1, afterFirst.ItemCount);
        Assert.Equal(1, afterFirst.SettlementCount);
        Assert.Equal(WorldBossSpawnStatus.Settled, afterFirst.SpawnStatus);

        WorldBossSettlementBatchResult replay;
        await using (GameDbContext db = postgres.CreateDbContext())
        {
            replay = await CreateService(db, package).SettleAsync(seed.SpawnId, default);
        }

        Assert.True(replay.Succeeded, replay.ErrorCode);
        Assert.True(replay.WasReplay);
        Assert.Single(replay.Rewards);
        CharacterState afterReplay = await ReadCharacterStateAsync(seed.CharacterId);
        Assert.Equal(afterFirst, afterReplay);
    }

    [Fact]
    public async Task ConcurrentSettlementProducesOnePermanentReward()
    {
        GameContentPackage package = await LoadContentAsync();
        Seed seed = await SeedDefeatedAsync(package, 200_000m);

        async Task<WorldBossSettlementBatchResult> SettleAsync()
        {
            await using GameDbContext db = postgres.CreateDbContext();
            return await CreateService(db, package).SettleAsync(seed.SpawnId, default);
        }

        WorldBossSettlementBatchResult[] results =
            await Task.WhenAll(SettleAsync(), SettleAsync());

        Assert.All(results, result => Assert.True(result.Succeeded, result.ErrorCode));
        Assert.Contains(results, result => !result.WasReplay);
        Assert.Contains(results, result => result.WasReplay);

        CharacterState state = await ReadCharacterStateAsync(seed.CharacterId);
        Assert.Equal(1, state.SettlementCount);
        Assert.Equal(1, state.ItemCount);
        Assert.Equal(1_000, state.Gold);

        await using GameDbContext verify = postgres.CreateDbContext();
        WorldBossRewardSettlement settlement =
            await verify.WorldBossRewardSettlements.SingleAsync();
        Assert.Equal(WorldBossRewardTier.Top5, settlement.RewardTier);
        Assert.Equal(200_000, settlement.Experience);
    }

    [Fact]
    public async Task HealingOnlyContributionQualifiesForWorldBossReward()
    {
        GameContentPackage package = await LoadContentAsync();
        Seed seed = await SeedDefeatedAsync(
            package,
            contributionDamage: 0m,
            contributionHealing: 6_000m);

        await using (GameDbContext db = postgres.CreateDbContext())
        {
            WorldBossSettlementBatchResult result =
                await CreateService(db, package).SettleAsync(seed.SpawnId, default);

            Assert.True(result.Succeeded, result.ErrorCode);
            WorldBossSettlementCharacterResult reward = Assert.Single(result.Rewards);
            Assert.Equal(6_000m, reward.Contribution);
            Assert.Equal(WorldBossRewardTier.Top5, reward.Tier);
            Assert.Equal(2, reward.EnhancedChestCount);
            WorldBossLootItemResult chest = Assert.Single(reward.Items);
            Assert.Equal("WORLD_BOSS_ASH_ARCHON_TOP5_CHEST", chest.ItemId);
            Assert.Equal(2, chest.Quantity);
        }

        await using GameDbContext verify = postgres.CreateDbContext();
        WorldBossContribution contribution =
            await verify.WorldBossContributions.SingleAsync();
        Assert.Equal(0m, contribution.Damage);
        Assert.Equal(6_000m, contribution.Healing);
        Assert.Equal(6_000m, contribution.ContributionScore);
    }

    [Fact]
    public async Task ContributionBelowEligibilityReceivesNoPermanentReward()
    {
        GameContentPackage package = await LoadContentAsync();
        Seed seed = await SeedDefeatedAsync(package, 4_999m);

        await using (GameDbContext db = postgres.CreateDbContext())
        {
            WorldBossSettlementBatchResult result =
                await CreateService(db, package).SettleAsync(seed.SpawnId, default);
            Assert.True(result.Succeeded, result.ErrorCode);
            Assert.Empty(result.Rewards);
        }

        CharacterState state = await ReadCharacterStateAsync(seed.CharacterId);
        Assert.Equal(0, state.Gold);
        Assert.Equal(0, state.ItemCount);
        Assert.Equal(0, state.SettlementCount);
        Assert.Equal(WorldBossSpawnStatus.Settled, state.SpawnStatus);
    }

    [Fact]
    public async Task RewardReadReturnsPersistedChestResultWithoutRerolling()
    {
        GameContentPackage package = await LoadContentAsync();
        Seed seed = await SeedDefeatedAsync(package, 25_000m);

        WorldBossSettlementCharacterResult settled;
        await using (GameDbContext db = postgres.CreateDbContext())
        {
            WorldBossSettlementBatchResult result =
                await CreateService(db, package).SettleAsync(seed.SpawnId, default);
            settled = Assert.Single(result.Rewards);
        }

        await using GameDbContext readDb = postgres.CreateDbContext();
        var reader = new WorldBossReadService(
            readDb,
            new StaticContentSnapshotProvider(package),
            new FixedTime(Now.AddMinutes(1)));
        WorldBossRewardReadResult read = await reader.GetRewardAsync(
            seed.AccountId,
            seed.SpawnId,
            default);

        Assert.True(read.CharacterFound);
        Assert.True(read.SpawnFound);
        Assert.NotNull(read.Reward);
        Assert.Equal(settled.Tier, read.Reward!.Tier);
        Assert.Equal(settled.Experience, read.Reward.Experience);
        Assert.Equal(settled.BossGold, read.Reward.BossGold);
        Assert.Equal(settled.ChestGold, read.Reward.ChestGold);
        Assert.Equal(settled.Rank, read.Reward.Rank);
        Assert.Equal(settled.EligibleParticipants, read.Reward.EligibleParticipants);
        Assert.Equal(settled.Percentile, read.Reward.Percentile);
        Assert.Equal(settled.ChestCount, read.Reward.ChestCount);
        Assert.Equal(settled.EnhancedChestCount, read.Reward.EnhancedChestCount);
        Assert.Equal(
            JsonSerializer.Serialize(settled.Items),
            JsonSerializer.Serialize(read.Reward.Items));
    }

    private static WorldBossSettlementService CreateService(
        GameDbContext db,
        GameContentPackage package,
        IWorldBossUpdatePublisher? publisher = null)
    {
        var provider = new StaticContentSnapshotProvider(package);
        var derived = new CharacterDerivedStateService(db, provider, inventoryService: null);
        return new WorldBossSettlementService(
            db,
            provider,
            derived,
            new FixedTime(Now.AddMinutes(1)),
            publisher);
    }

    private async Task<Seed> SeedDefeatedAsync(
        GameContentPackage package,
        decimal contributionDamage,
        decimal contributionHealing = 0m)
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid spawnId = Guid.CreateVersion7();

        await using GameDbContext db = postgres.CreateDbContext();
        db.Accounts.Add(new Account(
            accountId,
            Random.Shared.NextInt64(1, long.MaxValue),
            Now));

        var character = new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "Settlement",
            $"SETTLE{characterId:N}"[..16].ToUpperInvariant(),
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);
        character.SetLevel(30);
        db.Characters.Add(character);

        var spawn = new WorldBossSpawn(
            spawnId,
            "WORLD_BOSS_ASH_ARCHON",
            100_000m,
            1,
            Now,
            Now.AddMinutes(30),
            package.ContentVersion,
            package.BalanceVersion);
        _ = spawn.ApplyDamage(spawn.MaxHealth);
        Assert.True(spawn.TryMarkDefeated(Now.AddMinutes(1)));
        db.WorldBossSpawns.Add(spawn);

        var contribution = new WorldBossContribution(
            spawnId,
            characterId,
            Now.AddSeconds(1));
        if (contributionDamage > 0)
        {
            contribution.AddDamage(
                contributionDamage,
                Now.AddMinutes(1));
        }
        if (contributionHealing > 0)
        {
            contribution.AddHealing(
                contributionHealing,
                Now.AddMinutes(1));
        }
        db.WorldBossContributions.Add(contribution);

        await db.SaveChangesAsync();
        return new Seed(accountId, characterId, spawnId);
    }

    private async Task<CharacterState> ReadCharacterStateAsync(Guid characterId)
    {
        await using GameDbContext db = postgres.CreateDbContext();
        Character character = await db.Characters.SingleAsync(
            candidate => candidate.Id == characterId);
        return new CharacterState(
            character.Gold,
            character.Level,
            character.Experience,
            await db.CharacterItems.CountAsync(item => item.CharacterId == characterId),
            await db.WorldBossRewardSettlements.CountAsync(
                settlement => settlement.CharacterId == characterId),
            (await db.WorldBossSpawns.SingleAsync()).Status);
    }

    private static Task<GameContentPackage> LoadContentAsync() =>
        GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

    private sealed record Seed(Guid AccountId, Guid CharacterId, Guid SpawnId);

    private sealed record CharacterState(
        long Gold,
        int Level,
        long Experience,
        int ItemCount,
        int SettlementCount,
        WorldBossSpawnStatus SpawnStatus);

    private sealed class CapturingPublisher : IWorldBossUpdatePublisher
    {
        public Guid? SettledSpawnId { get; private set; }
        public DateTimeOffset? SettledAtUtc { get; private set; }
        public IReadOnlyCollection<WorldBossRewardDelivery> Deliveries { get; private set; } = [];

        public Task PublishActivatedAsync(
            Guid spawnId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PublishProgressAsync(
            Guid spawnId,
            decimal currentHealth,
            decimal maxHealth,
            int currentPhase,
            bool phaseChanged,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PublishDefeatedAsync(
            Guid spawnId,
            DateTimeOffset defeatedAtUtc,
            IReadOnlyCollection<Guid> participantAccountIds,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PublishSettledAsync(
            Guid spawnId,
            DateTimeOffset settledAtUtc,
            IReadOnlyCollection<WorldBossRewardDelivery> deliveries,
            CancellationToken cancellationToken)
        {
            SettledSpawnId = spawnId;
            SettledAtUtc = settledAtUtc;
            Deliveries = deliveries;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
