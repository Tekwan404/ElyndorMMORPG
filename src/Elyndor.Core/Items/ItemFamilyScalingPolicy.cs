using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

/// <summary>
/// Scales the structural flat stats of an item-family template from its authored base item level
/// to a concrete generated item level. Percentage modifiers, attack speed and equip semantics do
/// not scale here; they remain authored properties of the family.
/// </summary>
public static class ItemFamilyScalingPolicy
{
    public static ItemDefinition Apply(
        ItemDefinition template,
        ItemizationDefinition itemization,
        int itemLevel)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(itemization);

        if (string.IsNullOrWhiteSpace(template.ItemFamilyId))
            return template;

        int baseLevel = template.ItemLevelMin ?? template.RequiredLevel;
        int maximum = template.ItemLevelMax ?? baseLevel;
        if (baseLevel < 1 || maximum < baseLevel || itemLevel < baseLevel || itemLevel > maximum)
        {
            throw new InvalidOperationException(
                $"Family item '{template.Id}' item level {itemLevel} is outside template range {baseLevel}-{maximum}.");
        }

        decimal baseMultiplier = LevelMultiplier(itemization, baseLevel);
        decimal targetMultiplier = LevelMultiplier(itemization, itemLevel);
        decimal scale = targetMultiplier / baseMultiplier;
        if (scale == 1m)
            return template;

        return template with
        {
            Stats = new PrimaryStats(
                Scale(template.Stats.Strength, scale),
                Scale(template.Stats.Agility, scale),
                Scale(template.Stats.Intellect, scale),
                Scale(template.Stats.Stamina, scale)),
            MaxHpFlat = Scale(template.MaxHpFlat, scale),
            AttackPowerFlat = Scale(template.AttackPowerFlat, scale),
            SpellPowerFlat = Scale(template.SpellPowerFlat, scale),
            ArmorFlat = Scale(template.ArmorFlat, scale),
            MagicResistanceFlat = Scale(template.MagicResistanceFlat, scale),
            MaxResourceFlat = Scale(template.MaxResourceFlat, scale),
            BlockValueMin = Scale(template.BlockValueMin, scale),
            BlockValueMax = Scale(template.BlockValueMax, scale),
            WeaponDamageMin = template.WeaponDamageMin.HasValue
                ? Scale(template.WeaponDamageMin.Value, scale)
                : null,
            WeaponDamageMax = template.WeaponDamageMax.HasValue
                ? Scale(template.WeaponDamageMax.Value, scale)
                : null
        };
    }

    public static decimal LevelMultiplier(ItemizationDefinition itemization, int itemLevel)
    {
        ArgumentNullException.ThrowIfNull(itemization);
        ArgumentOutOfRangeException.ThrowIfLessThan(itemLevel, 1);

        decimal x = itemLevel - 1;
        return 1m
            + (itemization.LevelLinearCoefficient * x)
            + (itemization.LevelQuadraticCoefficient * x * x);
    }

    private static decimal Scale(decimal value, decimal multiplier) =>
        value == 0m
            ? 0m
            : decimal.Round(value * multiplier, 4, MidpointRounding.AwayFromZero);
}
