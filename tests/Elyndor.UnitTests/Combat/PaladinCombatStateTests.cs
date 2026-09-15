using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Paladin;

namespace Elyndor.UnitTests.Combat;

public sealed class PaladinCombatStateTests
{
    [Fact]
    public void ActivatingSealReplacesThePreviousSeal()
    {
        PaladinCombatState state = new();

        state.ActivateSeal("SEAL_OF_RIGHTEOUSNESS");
        state.ActivateSeal("SEAL_OF_COMMAND");

        Assert.Equal("SEAL_OF_COMMAND", state.ActiveSealId);
    }

    [Fact]
    public void ActivatingAuraReplacesThePreviousAura()
    {
        PaladinCombatState state = new();

        state.ActivateAura("DEVOTION_AURA");
        state.ActivateAura("SANCTITY_AURA");

        Assert.Equal("SANCTITY_AURA", state.ActiveAuraId);
    }

    [Fact]
    public void ExpectedClearCannotRemoveAReplacementState()
    {
        PaladinCombatState state = new();
        state.ActivateSeal("SEAL_OF_RIGHTEOUSNESS");
        state.ActivateSeal("SEAL_OF_COMMAND");
        state.ActivateAura("DEVOTION_AURA");
        state.ActivateAura("CONCENTRATION_AURA");

        state.ClearSeal("SEAL_OF_RIGHTEOUSNESS");
        state.ClearAura("DEVOTION_AURA");

        Assert.Equal("SEAL_OF_COMMAND", state.ActiveSealId);
        Assert.Equal("CONCENTRATION_AURA", state.ActiveAuraId);
    }

    [Fact]
    public void BeaconCopiesOnlyEffectiveDirectHealingAppliedToAnotherTarget()
    {
        PaladinCombatState state = new();
        Guid beaconTarget = Guid.NewGuid();
        Guid healedTarget = Guid.NewGuid();
        state.SetBeacon(beaconTarget);

        HealingResult direct = Result(HealingOrigin.Direct, 30);
        HealingResult periodic = Result(HealingOrigin.Periodic, 30);
        HealingResult copied = Result(HealingOrigin.Copied, 30);
        HealingResult secondary = Result(HealingOrigin.Secondary, 30);
        HealingResult zero = Result(HealingOrigin.Direct, 0);

        Assert.True(state.CanCopyHealingToBeacon(direct, healedTarget));
        Assert.False(state.CanCopyHealingToBeacon(direct, beaconTarget));
        Assert.False(state.CanCopyHealingToBeacon(periodic, healedTarget));
        Assert.False(state.CanCopyHealingToBeacon(copied, healedTarget));
        Assert.False(state.CanCopyHealingToBeacon(secondary, healedTarget));
        Assert.False(state.CanCopyHealingToBeacon(zero, healedTarget));
    }

    [Fact]
    public void ReplacingBeaconMakesThePreviousTargetIneligible()
    {
        PaladinCombatState state = new();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        Guid healedTarget = Guid.NewGuid();

        state.SetBeacon(first);
        state.SetBeacon(second);

        Assert.Equal(second, state.BeaconTargetId);
        Assert.True(state.CanCopyHealingToBeacon(Result(HealingOrigin.Direct, 10), healedTarget));
        state.ClearBeacon(first);
        Assert.Equal(second, state.BeaconTargetId);
        state.ClearBeacon(second);
        Assert.Null(state.BeaconTargetId);
    }

    private static HealingResult Result(HealingOrigin origin, decimal effective) =>
        new(
            AttemptedAmount: effective,
            ModifiedAmount: effective,
            EffectiveHealing: effective,
            Overheal: 0,
            ResultingHp: 100,
            Events: [],
            Origin: origin);
}
