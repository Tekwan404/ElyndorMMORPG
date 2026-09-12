namespace Elyndor.Core.Combat.Damage;

public static class DefenseMitigationFormula
{
    public const decimal MitigationConstant = 100m;

    public static decimal CalculateDamageMultiplier(decimal defense)
    {
        decimal effectiveDefense = Math.Max(0, defense);
        return MitigationConstant / (MitigationConstant + effectiveDefense);
    }

    public static decimal CalculateReductionPercent(decimal defense) =>
        (1 - CalculateDamageMultiplier(defense)) * 100m;
}
