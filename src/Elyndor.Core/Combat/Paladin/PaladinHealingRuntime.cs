using Elyndor.Core.Combat.Damage;

namespace Elyndor.Core.Combat.Paladin;

public sealed class PaladinHealingRuntime
{
    private int _heraldEligibleDirectHeals;
    private bool _divineFavorArmed;
    private bool _nextHolyShockFree;

    public int HeraldEligibleDirectHeals => _heraldEligibleDirectHeals;
    public bool DivineFavorArmed => _divineFavorArmed;
    public bool NextHolyShockFree => _nextHolyShockFree;

    public void ArmDivineFavor() => _divineFavorArmed = true;

    public bool ConsumeDivineFavorForDirectHeal()
    {
        if (!_divineFavorArmed)
        {
            return false;
        }

        _divineFavorArmed = false;
        return true;
    }

    public static decimal CalculateIlluminationRefund(
        HealingResult healing,
        decimal actualManaSpent,
        decimal refundPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actualManaSpent);
        ValidatePercent(refundPercent, nameof(refundPercent));

        if (!healing.IsCritical || healing.Origin != HealingOrigin.Direct)
        {
            return 0;
        }

        return decimal.Round(
            actualManaSpent * refundPercent / 100m,
            0,
            MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateAfterglowTotalHealing(
        HealingResult healing,
        decimal percentOfEffectiveHealing)
    {
        ValidatePercent(percentOfEffectiveHealing, nameof(percentOfEffectiveHealing));

        if (!healing.IsCritical
            || healing.Origin != HealingOrigin.Direct
            || healing.EffectiveHealing <= 0)
        {
            return 0;
        }

        return decimal.Round(
            healing.EffectiveHealing * percentOfEffectiveHealing / 100m,
            0,
            MidpointRounding.AwayFromZero);
    }

    public bool RecordHeraldEligibleDirectHeal(HealingResult healing)
    {
        if (healing.Origin != HealingOrigin.Direct
            || healing.EffectiveHealing <= 0)
        {
            return false;
        }

        _heraldEligibleDirectHeals++;
        if (_heraldEligibleDirectHeals < 3)
        {
            return false;
        }

        _heraldEligibleDirectHeals = 0;
        _nextHolyShockFree = true;
        return true;
    }

    public bool ConsumeFreeHolyShock()
    {
        if (!_nextHolyShockFree)
        {
            return false;
        }

        _nextHolyShockFree = false;
        return true;
    }

    private static void ValidatePercent(decimal percent, string parameterName)
    {
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
