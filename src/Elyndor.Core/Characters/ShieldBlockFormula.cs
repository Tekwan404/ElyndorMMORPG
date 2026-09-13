using Elyndor.Core.Combat.Damage;

namespace Elyndor.Core.Characters;

/// <summary>
/// Resolves the final equipment block profile after primary stats and talent modifiers are known.
/// Strength improves block value only while a real shield profile is equipped; it never grants
/// Armor, block chance, or shieldless block by itself.
/// </summary>
public static class ShieldBlockFormula
{
    public const decimal BlockValuePerStrength = 0.75m;

    public static ShieldBlockResult Resolve(
        decimal baseBlockChancePercent,
        decimal shieldBlockValueMin,
        decimal shieldBlockValueMax,
        decimal strength,
        decimal talentBlockChancePercent = 0,
        decimal talentBlockValueFlat = 0)
    {
        decimal baseChance = Math.Max(0, baseBlockChancePercent);
        decimal baseMin = Math.Max(0, shieldBlockValueMin);
        decimal baseMax = Math.Max(baseMin, shieldBlockValueMax);
        bool hasShieldProfile = baseChance > 0 && baseMax > 0;
        if (!hasShieldProfile)
            return ShieldBlockResult.Empty;

        decimal strengthContribution = Math.Max(0, strength) * BlockValuePerStrength;
        decimal finalMin = Math.Max(
            0,
            baseMin + strengthContribution + talentBlockValueFlat);
        decimal finalMax = Math.Max(
            finalMin,
            baseMax + strengthContribution + talentBlockValueFlat);
        decimal finalChance = decimal.Clamp(
            baseChance + talentBlockChancePercent,
            0,
            DamagePipeline.MaximumBlockChancePercent);

        if (finalChance <= 0 || finalMax <= 0)
            return ShieldBlockResult.Empty;

        return new ShieldBlockResult(
            finalChance,
            finalMin,
            finalMax,
            strengthContribution,
            HasShieldProfile: true);
    }
}

public sealed record ShieldBlockResult(
    decimal BlockChancePercent,
    decimal BlockValueMin,
    decimal BlockValueMax,
    decimal StrengthContribution,
    bool HasShieldProfile)
{
    public static ShieldBlockResult Empty { get; } = new(0, 0, 0, 0, false);
}
