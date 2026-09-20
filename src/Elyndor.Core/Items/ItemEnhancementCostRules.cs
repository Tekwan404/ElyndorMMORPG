namespace Elyndor.Core.Items;

public sealed record ItemEnhancementCost(
    int Gold,
    string EnhancementMaterialItemId,
    int EnhancementMaterialQuantity,
    string? CatalystItemId,
    int CatalystQuantity);

/// <summary>
/// Centralizes the V2 enhancement cost interpretation while the content schema still uses the
/// legacy StarUpgrades field names for backward-compatible deserialization.
/// </summary>
public static class ItemEnhancementCostRules
{
    public const string EnhancementMaterialItemId = "ENHANCEMENT_ORE";

    public static bool TryResolve(
        ItemStarUpgradeProfileDefinition profile,
        int targetEnhancementLevel,
        out ItemEnhancementCost cost)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (targetEnhancementLevel is < ItemEnhancementRules.MinimumLevel + 1 or > ItemEnhancementRules.MaximumLevel)
        {
            cost = default!;
            return false;
        }

        // Compatibility mapping: old target-star keys 2..5 become +1..+4. +5 intentionally
        // reuses the old high-end key until the content document itself is migrated to V2 names.
        int legacyCostKey = Math.Min(5, targetEnhancementLevel + 1);
        if (!profile.GoldByTargetStars.TryGetValue(legacyCostKey, out int gold)
            || !profile.ReforgeStoneQuantityByTargetStars.TryGetValue(legacyCostKey, out int materialQuantity))
        {
            cost = default!;
            return false;
        }

        bool catalystRequired = targetEnhancementLevel >= 4 && profile.HighEndCatalystQuantity > 0;
        cost = new ItemEnhancementCost(
            gold,
            EnhancementMaterialItemId,
            materialQuantity,
            catalystRequired ? profile.HighEndCatalystItemId : null,
            catalystRequired ? profile.HighEndCatalystQuantity : 0);
        return true;
    }

    public static ItemEnhancementInvestment ResolveInvestment(
        ItemStarUpgradeProfileDefinition profile,
        int enhancementLevel)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (enhancementLevel is < ItemEnhancementRules.MinimumLevel or > ItemEnhancementRules.MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(enhancementLevel));

        int materialQuantity = 0;
        int catalystQuantity = 0;
        string? catalystItemId = null;
        for (int level = 1; level <= enhancementLevel; level++)
        {
            if (!TryResolve(profile, level, out ItemEnhancementCost cost))
                throw new InvalidOperationException($"Enhancement cost is missing for +{level}.");
            materialQuantity = checked(materialQuantity + cost.EnhancementMaterialQuantity);
            if (cost.CatalystQuantity > 0)
            {
                if (catalystItemId is not null
                    && !string.Equals(catalystItemId, cost.CatalystItemId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Enhancement catalyst changed inside one progression table.");
                }
                catalystItemId = cost.CatalystItemId;
                catalystQuantity = checked(catalystQuantity + cost.CatalystQuantity);
            }
        }

        return new ItemEnhancementInvestment(
            EnhancementMaterialItemId,
            materialQuantity,
            catalystItemId,
            catalystQuantity);
    }
}

public sealed record ItemEnhancementInvestment(
    string EnhancementMaterialItemId,
    int EnhancementMaterialQuantity,
    string? CatalystItemId,
    int CatalystQuantity);
