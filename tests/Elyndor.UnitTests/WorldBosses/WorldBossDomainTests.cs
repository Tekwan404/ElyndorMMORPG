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
