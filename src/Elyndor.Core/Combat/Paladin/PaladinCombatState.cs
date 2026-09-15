using Elyndor.Core.Combat.Damage;

namespace Elyndor.Core.Combat.Paladin;

public sealed class PaladinCombatState
{
    public string? ActiveSealId { get; private set; }
    public string? ActiveAuraId { get; private set; }
    public Guid? BeaconTargetId { get; private set; }

    public void ActivateSeal(string sealId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sealId);
        ActiveSealId = sealId;
    }

    public void ClearSeal(string? expectedSealId = null)
    {
        if (expectedSealId is not null
            && !string.Equals(ActiveSealId, expectedSealId, StringComparison.Ordinal))
        {
            return;
        }

        ActiveSealId = null;
    }

    public void ActivateAura(string auraId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(auraId);
        ActiveAuraId = auraId;
    }

    public void ClearAura(string? expectedAuraId = null)
    {
        if (expectedAuraId is not null
            && !string.Equals(ActiveAuraId, expectedAuraId, StringComparison.Ordinal))
        {
            return;
        }

        ActiveAuraId = null;
    }

    public void SetBeacon(Guid targetId)
    {
        if (targetId == Guid.Empty)
        {
            throw new ArgumentException("Beacon target must be a real combat actor.", nameof(targetId));
        }

        BeaconTargetId = targetId;
    }

    public void ClearBeacon(Guid? expectedTargetId = null)
    {
        if (expectedTargetId is not null && BeaconTargetId != expectedTargetId)
        {
            return;
        }

        BeaconTargetId = null;
    }

    public bool CanCopyHealingToBeacon(
        HealingResult sourceHealing,
        Guid originalTargetId)
    {
        if (BeaconTargetId is not { } beaconTargetId
            || beaconTargetId == originalTargetId
            || sourceHealing.EffectiveHealing <= 0)
        {
            return false;
        }

        return sourceHealing.Origin == HealingOrigin.Direct;
    }
}
