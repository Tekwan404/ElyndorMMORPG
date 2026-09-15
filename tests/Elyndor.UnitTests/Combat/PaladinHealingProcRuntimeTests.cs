using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Paladin;

namespace Elyndor.UnitTests.Combat;

public sealed class PaladinHealingProcRuntimeTests
{
    [Fact]
    public void IlluminationRefundUsesManaActuallySpentAndOnlyDirectCriticalHealing()
    {
        HealingResult directCritical = Healing(
            HealingOrigin.Direct,
            effective: 120,
            critical: true);

        decimal refund = PaladinHealingRuntime.CalculateIlluminationRefund(
            directCritical,
            actualManaSpent: 80,
            refundPercent: 75);

        Assert.Equal(60, refund);
        Assert.Equal(0, PaladinHealingRuntime.CalculateIlluminationRefund(
            Healing(HealingOrigin.Direct, 120, critical: false), 80, 75));
        Assert.Equal(0, PaladinHealingRuntime.CalculateIlluminationRefund(
            Healing(HealingOrigin.Copied, 120, critical: true), 80, 75));
        Assert.Equal(0, PaladinHealingRuntime.CalculateIlluminationRefund(
            Healing(HealingOrigin.Periodic, 120, critical: true), 80, 75));
    }

    [Fact]
    public void PerfectIlluminationCanUseTheApprovedNinetyPercentCapWithoutSpecialCaseCode()
    {
        decimal refund = PaladinHealingRuntime.CalculateIlluminationRefund(
            Healing(HealingOrigin.Direct, 100, critical: true),
            actualManaSpent: 100,
            refundPercent: 90);

        Assert.Equal(90, refund);
    }

    [Fact]
    public void DivineFavorIsConsumedExactlyOnceByTheNextDirectHealAttempt()
    {
        PaladinHealingRuntime runtime = new();

        Assert.False(runtime.ConsumeDivineFavorForDirectHeal());
        runtime.ArmDivineFavor();

        Assert.True(runtime.DivineFavorArmed);
        Assert.True(runtime.ConsumeDivineFavorForDirectHeal());
        Assert.False(runtime.DivineFavorArmed);
        Assert.False(runtime.ConsumeDivineFavorForDirectHeal());
    }

    [Fact]
    public void AfterglowUsesEffectiveCriticalHealingAndRejectsCopiedOrPeriodicSources()
    {
        Assert.Equal(8, PaladinHealingRuntime.CalculateAfterglowTotalHealing(
            Healing(HealingOrigin.Direct, 100, critical: true), 8));
        Assert.Equal(0, PaladinHealingRuntime.CalculateAfterglowTotalHealing(
            Healing(HealingOrigin.Direct, 0, critical: true), 8));
        Assert.Equal(0, PaladinHealingRuntime.CalculateAfterglowTotalHealing(
            Healing(HealingOrigin.Copied, 100, critical: true), 8));
        Assert.Equal(0, PaladinHealingRuntime.CalculateAfterglowTotalHealing(
            Healing(HealingOrigin.Periodic, 100, critical: true), 8));
    }

    [Fact]
    public void HeraldCountsOnlyEffectiveDirectHealingAndArmsEveryThirdHeal()
    {
        PaladinHealingRuntime runtime = new();

        Assert.False(runtime.RecordHeraldEligibleDirectHeal(
            Healing(HealingOrigin.Copied, 50)));
        Assert.False(runtime.RecordHeraldEligibleDirectHeal(
            Healing(HealingOrigin.Periodic, 50)));
        Assert.False(runtime.RecordHeraldEligibleDirectHeal(
            Healing(HealingOrigin.Secondary, 50)));
        Assert.False(runtime.RecordHeraldEligibleDirectHeal(
            Healing(HealingOrigin.Direct, 0)));
        Assert.Equal(0, runtime.HeraldEligibleDirectHeals);

        Assert.False(runtime.RecordHeraldEligibleDirectHeal(
            Healing(HealingOrigin.Direct, 10)));
        Assert.False(runtime.RecordHeraldEligibleDirectHeal(
            Healing(HealingOrigin.Direct, 10)));
        Assert.True(runtime.RecordHeraldEligibleDirectHeal(
            Healing(HealingOrigin.Direct, 10)));

        Assert.Equal(0, runtime.HeraldEligibleDirectHeals);
        Assert.True(runtime.NextHolyShockFree);
        Assert.True(runtime.ConsumeFreeHolyShock());
        Assert.False(runtime.ConsumeFreeHolyShock());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void PercentageBasedHolyMechanicsRejectInvalidValues(decimal percent)
    {
        HealingResult healing = Healing(HealingOrigin.Direct, 100, critical: true);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaladinHealingRuntime.CalculateIlluminationRefund(healing, 10, percent));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaladinHealingRuntime.CalculateAfterglowTotalHealing(healing, percent));
    }

    private static HealingResult Healing(
        HealingOrigin origin,
        decimal effective,
        bool critical = false) =>
        new(
            AttemptedAmount: effective,
            ModifiedAmount: effective,
            EffectiveHealing: effective,
            Overheal: 0,
            ResultingHp: 100,
            Events: [],
            IsCritical: critical,
            Origin: origin);
}
