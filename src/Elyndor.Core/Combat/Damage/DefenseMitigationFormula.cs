namespace Elyndor.Core.Combat.Damage;

public static class DefenseMitigationFormula
{
    public const decimal BaseMitigationConstant = 50m;
    public const decimal MitigationConstantPerLevel = 40m;
    public const decimal MaximumReductionPercent = 60m;

    public static decimal CalculateDamageMultiplier(decimal defense, int referenceLevel)
    {
        decimal uncappedReduction = CalculateUncappedReduction(defense, referenceLevel);
        decimal cappedReduction = Math.Min(MaximumReductionPercent / 100m, uncappedReduction);
        return 1m - cappedReduction;
    }

    public static decimal CalculateUncappedReductionPercent(decimal defense, int referenceLevel) =>
        CalculateUncappedReduction(defense, referenceLevel) * 100m;

    private static decimal CalculateUncappedReduction(decimal defense, int referenceLevel)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(referenceLevel, 1);

        decimal effectiveDefense = Math.Max(0, defense);
        decimal mitigationConstant = BaseMitigationConstant
            + (MitigationConstantPerLevel * referenceLevel);
        return effectiveDefense / (mitigationConstant + effectiveDefense);
    }

    public static decimal CalculateReductionPercent(decimal defense, int referenceLevel) =>
        (1 - CalculateDamageMultiplier(defense, referenceLevel)) * 100m;
}
