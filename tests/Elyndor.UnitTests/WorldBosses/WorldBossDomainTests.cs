using Elyndor.Core.WorldBosses;

namespace Elyndor.UnitTests.WorldBosses;

public sealed class WorldBossDomainTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DamageIsClampedToRemainingGlobalHealth()
    {
        WorldBossSpawn spawn = CreateSpawn(10_000m);

        Assert.Equal(7_500m, spawn.ApplyDamage(7_500m));
        Assert.Equal(2_500m, spawn.CurrentHealth);

        Assert.Equal(2_500m, spawn.ApplyDamage(50_000m));
        Assert.Equal(0m, spawn.CurrentHealth);
        Assert.True(spawn.TryMarkDefeated(Start.AddMinutes(1)));
        Assert.Equal(WorldBossSpawnStatus.Defeated, spawn.Status);

        Assert.Equal(0m, spawn.ApplyDamage(1_000m));
        Assert.False(spawn.TryMarkDefeated(Start.AddMinutes(2)));
    }

    [Fact]
    public void PhaseOnlyMovesForwardWhileSpawnIsActive()
    {
        WorldBossSpawn spawn = CreateSpawn();

        Assert.True(spawn.TryChangePhase(2));
        Assert.False(spawn.TryChangePhase(2));
        Assert.False(spawn.TryChangePhase(1));
        Assert.Equal(2, spawn.CurrentPhase);

        Assert.True(spawn.TryExpire(Start.AddMinutes(30)));
        Assert.False(spawn.TryChangePhase(3));
        Assert.Equal(WorldBossSpawnStatus.Expired, spawn.Status);
    }

    [Fact]
    public void SettlementLifecycleIsTerminalAndIdempotent()
    {
        WorldBossSpawn spawn = CreateSpawn();
        spawn.ApplyDamage(spawn.MaxHealth);
        Assert.True(spawn.TryMarkDefeated(Start.AddMinutes(1)));

        Assert.True(spawn.TryBeginSettlement());
        Assert.False(spawn.TryBeginSettlement());
        Assert.True(spawn.TryMarkSettled(Start.AddMinutes(2)));
        Assert.False(spawn.TryMarkSettled(Start.AddMinutes(3)));
        Assert.Equal(WorldBossSpawnStatus.Settled, spawn.Status);
    }

    [Fact]
    public void PersonalContributionAccumulatesOnlyPositiveAppliedDamage()
    {
        Guid spawnId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        WorldBossContribution contribution = new(spawnId, characterId, Start);

        contribution.AddDamage(2_500m, Start.AddSeconds(1));
        contribution.AddDamage(1_250.5m, Start.AddSeconds(2));

        Assert.Equal(3_750.5m, contribution.Damage);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            contribution.AddDamage(0m, Start.AddSeconds(3)));
    }

    [Fact]
    public void DamageMutationRejectsOverReportedAppliedDamage()
    {
        Assert.Throws<ArgumentException>(() => new WorldBossDamageMutation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            requestedDamage: 100m,
            appliedDamage: 101m,
            committedAtUtc: Start));
    }

    [Theory]
    [InlineData(1000, 1000, WorldBossRewardTier.Qualified, 0, 0)]
    [InlineData(501, 1000, WorldBossRewardTier.Top50, 0, 0)]
    [InlineData(251, 1000, WorldBossRewardTier.Top75, 0, 0)]
    [InlineData(151, 1000, WorldBossRewardTier.Top85, 1, 0)]
    [InlineData(51, 1000, WorldBossRewardTier.Top95, 1, 0)]
    [InlineData(11, 1000, WorldBossRewardTier.Top99, 2, 0)]
    [InlineData(5, 1000, WorldBossRewardTier.Top5, 0, 2)]
    public void LeaderboardRewardPolicyUsesPercentilesAndTopFiveOverride(
        int rank,
        int eligibleParticipants,
        WorldBossRewardTier expectedTier,
        int expectedChestCount,
        int expectedEnhancedChestCount)
    {
        WorldBossLeaderboardRewardResolution reward =
            WorldBossLeaderboardRewardPolicy.Resolve(
                CreateRewardProfile(),
                rank,
                eligibleParticipants);

        Assert.Equal(expectedTier, reward.Tier);
        Assert.Equal(expectedChestCount, reward.ChestCount);
        Assert.Equal(expectedEnhancedChestCount, reward.EnhancedChestCount);
    }

    private static WorldBossRewardProfileDefinition CreateRewardProfile() =>
        new(
            "TEST_WORLD_BOSS_REWARD",
            5_000m,
            200_000,
            1_000,
            250,
            500,
            [],
            [
                new(WorldBossRewardTier.Top50, 50m, 0),
                new(WorldBossRewardTier.Top75, 75m, 0),
                new(WorldBossRewardTier.Top85, 85m, 1),
                new(WorldBossRewardTier.Top95, 95m, 1),
                new(WorldBossRewardTier.Top99, 99m, 2),
                new(
                    WorldBossRewardTier.Top5,
                    0m,
                    2,
                    MaxRank: 5,
                    LootTableId: "TOP5",
                    Enhanced: true)
            ]);

    private static WorldBossSpawn CreateSpawn(decimal maxHealth = 1_000_000m) =>
        new(
            Guid.NewGuid(),
            "WORLD_BOSS_ASH_ARCHON",
            maxHealth,
            initialPhase: 1,
            spawnedAtUtc: Start,
            expiresAtUtc: Start.AddMinutes(30),
            contentVersion: "test-content",
            balanceVersion: "test-balance");
}
