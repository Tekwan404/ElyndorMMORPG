using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

/// <summary>
/// Post-acquisition progression for equipment. Enhancement is deliberately independent from
/// birth-time quality: it never changes stars, RollQuality, affixes or Perfect status.
/// </summary>
public static class ItemEnhancementRules
{
    public const int MinimumLevel = 0;
    public const int MaximumLevel = 5;
    public const decimal BonusPerLevel = 0.02m;

    public static decimal ResolveBonusPercent(int enhancementLevel)
    {
        ValidateLevel(enhancementLevel);
        return enhancementLevel * BonusPerLevel;
    }

    public static decimal ResolveMultiplier(int enhancementLevel) =>
        1m + ResolveBonusPercent(enhancementLevel);

    /// <summary>
    /// Applies enhancement only to structural equipment stats. Random affixes and rating-style
    /// secondary stats are intentionally left untouched.
    /// </summary>
    public static ItemDefinition ApplyStructuralEnhancement(
        ItemDefinition item,
        int enhancementLevel)
    {
        ArgumentNullException.ThrowIfNull(item);
        ValidateLevel(enhancementLevel);
        if (enhancementLevel == 0 || item.Type != ItemType.Equipment)
            return item;

        decimal multiplier = ResolveMultiplier(enhancementLevel);
        bool spellOriented = item.WeaponCategory is EquipmentCategoryIds.Staff or EquipmentCategoryIds.Wand
            || item.OffHandCategory == EquipmentCategoryIds.Focus;

        return item with
        {
            ArmorFlat = Scale(item.ArmorFlat, multiplier),
            BlockValueMin = Scale(item.BlockValueMin, multiplier),
            BlockValueMax = Scale(item.BlockValueMax, multiplier),
            WeaponDamageMin = item.WeaponDamageMin.HasValue
                ? Scale(item.WeaponDamageMin.Value, multiplier)
                : null,
            WeaponDamageMax = item.WeaponDamageMax.HasValue
                ? Scale(item.WeaponDamageMax.Value, multiplier)
                : null,
            SpellPowerFlat = spellOriented
                ? Scale(item.SpellPowerFlat, multiplier)
                : item.SpellPowerFlat
        };
    }

    /// <summary>
    /// Returns display/comparison power after enhancement. Intrinsic power remains persisted
    /// separately in CharacterItem.ActualItemPower.
    /// </summary>
    public static decimal CalculateEnhancedItemPower(
        ItemDefinition template,
        ItemizationDefinition itemization,
        decimal intrinsicItemPower,
        int enhancementLevel)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(itemization);
        ValidateLevel(enhancementLevel);
        if (enhancementLevel == 0)
            return decimal.Round(intrinsicItemPower, 2, MidpointRounding.AwayFromZero);

        ItemDefinition enhanced = ApplyStructuralEnhancement(template, enhancementLevel);
        decimal delta = StructuralPower(enhanced, itemization) - StructuralPower(template, itemization);
        return decimal.Round(
            intrinsicItemPower + decimal.Max(0m, delta),
            2,
            MidpointRounding.AwayFromZero);
    }

    private static decimal StructuralPower(ItemDefinition item, ItemizationDefinition itemization)
    {
        decimal Weighted(string statId, decimal value) =>
            value == 0m || !itemization.StatPowerWeights.TryGetValue(statId, out decimal weight)
                ? 0m
                : decimal.Abs(value) * weight;

        decimal weaponAverage = item.WeaponDamageMin.HasValue && item.WeaponDamageMax.HasValue
            ? (item.WeaponDamageMin.Value + item.WeaponDamageMax.Value) / 2m
            : 0m;
        decimal blockAverage = item.BlockValueMin > 0m && item.BlockValueMax > 0m
            ? (item.BlockValueMin + item.BlockValueMax) / 2m
            : 0m;

        return Weighted(ItemStatIds.Armor, item.ArmorFlat)
            + Weighted(ItemStatIds.BlockValue, blockAverage)
            + Weighted(ItemStatIds.WeaponDamage, weaponAverage)
            + Weighted(ItemStatIds.SpellPower, item.SpellPowerFlat);
    }

    private static decimal Scale(decimal value, decimal multiplier) =>
        value == 0m
            ? 0m
            : decimal.Round(value * multiplier, 4, MidpointRounding.AwayFromZero);

    private static void ValidateLevel(int enhancementLevel)
    {
        if (enhancementLevel is < MinimumLevel or > MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(enhancementLevel));
    }
}
