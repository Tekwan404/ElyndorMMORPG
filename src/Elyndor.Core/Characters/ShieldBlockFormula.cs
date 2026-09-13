namespace Elyndor.Core.Characters;

/// <summary>
/// Resolves the equipment shield layer after primary stats are known.
/// Block rating controls frequency; block value controls absorbed damage.
/// </summary>
public static class ShieldBlockFormula
{
    // First-pass balance values for the shield rework. Keep them isolated here so
    // they can move into balance content without changing combat resolution order.
    public const decimal BlockRatingPerPercent = 5m;
    public const decimal BlockValuePerStrength = 0.75m;

    public static ShieldBlockResult Resolve(
        decimal blockRating,
        decimal shieldBlockValueMin,
        decimal shieldBlockValueMax,
        decimal strength)
    {
        decimal effectiveRating = Math.Max(0, blockRating);
        decimal baseMin = Math.Max(0, shieldBlockValueMin);
        decimal baseMax = Math.Max(baseMin, shieldBlockValueMax);
        bool hasShieldProfile = effectiveRating > 0 || baseMax > 0;
        if (!hasShieldProfile)
            return ShieldBlockResult.Empty;

        decimal finalChance = decimal.Clamp(
            effectiveRating / BlockRatingPerPercent,
            0,
            100);
        decimal strengthContribution = Math.Max(0, strength) * BlockValuePerStrength;
        decimal finalMin = baseMin + strengthContribution;
        decimal finalMax = Math.Max(finalMin, baseMax + strengthContribution);

        return new ShieldBlockResult(
            finalChance,
            finalMin,
            finalMax,
            strengthContribution);
    }
}

public sealed record ShieldBlockResult(
    decimal BlockChancePercent,
    decimal BlockValueMin,
    decimal BlockValueMax,
    decimal StrengthContribution)
{
    public static ShieldBlockResult Empty { get; } = new(0, 0, 0, 0);
}
