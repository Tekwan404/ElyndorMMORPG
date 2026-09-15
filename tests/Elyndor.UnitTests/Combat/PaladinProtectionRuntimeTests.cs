using Elyndor.Core.Combat.Paladin;

namespace Elyndor.UnitTests.Combat;

public sealed class PaladinProtectionRuntimeTests
{
    [Fact]
    public void HolyShieldBlockEffectsExistOnlyInsideTheActiveWindow()
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        PaladinProtectionRuntime runtime = new();

        runtime.ActivateHolyShield(now, TimeSpan.FromSeconds(6));

        Assert.True(runtime.CanTriggerHolyShieldBlockEffect(now));
        Assert.True(runtime.CanTriggerHolyShieldBlockEffect(now.AddSeconds(5.999)));
        Assert.False(runtime.CanTriggerHolyShieldBlockEffect(now.AddSeconds(6)));
    }

    [Fact]
    public void ArdentDefenderUsesTheApprovedBelowThirtyFivePercentThreshold()
    {
        Assert.True(PaladinProtectionRuntime.IsArdentDefenderActive(34, 100));
        Assert.False(PaladinProtectionRuntime.IsArdentDefenderActive(35, 100));
        Assert.Equal(15, PaladinProtectionRuntime.ResolveArdentDefenderReductionPercent(34, 100, 15));
        Assert.Equal(0, PaladinProtectionRuntime.ResolveArdentDefenderReductionPercent(35, 100, 15));
    }

    [Fact]
    public void ConsecratedProtectionAppliesOnlyWhileConsecrationIsActive()
    {
        DateTimeOffset now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
        PaladinProtectionRuntime runtime = new();
        runtime.ActivateConsecration(now, TimeSpan.FromSeconds(8));

        Assert.Equal(6, runtime.ResolveConsecratedProtectionReductionPercent(now.AddSeconds(7), 6));
        Assert.Equal(0, runtime.ResolveConsecratedProtectionReductionPercent(now.AddSeconds(8), 6));
    }

    [Fact]
    public void IntercessionRedirectIsCalculatedFromPostMitigationDamage()
    {
        decimal redirected = PaladinProtectionRuntime.ResolveIntercessionRedirectDamage(
            postMitigationDamage: 81,
            configuredRedirectPercent: 25);

        Assert.Equal(20, redirected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ConfigurableProtectionPercentagesAreValidated(decimal percent)
    {
        PaladinProtectionRuntime runtime = new();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaladinProtectionRuntime.ResolveArdentDefenderReductionPercent(10, 100, percent));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            runtime.ResolveConsecratedProtectionReductionPercent(DateTimeOffset.UnixEpoch, percent));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaladinProtectionRuntime.ResolveIntercessionRedirectDamage(10, percent));
    }
}
