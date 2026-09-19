using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

/// <summary>
/// Keeps procedural equipment budgets large enough for every legal affix to have
/// a real roll range instead of being forced into a collapsed minimum step.
/// Budget repair is applied per item template so healthy items in the same slot
/// keep their configured power budget unchanged.
/// </summary>
public static class ItemizationBudgetPolicy
{
    private const int MinimumAffixRollSteps = 2;

    public static GameContentPackage NormalizePackage(GameContentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Itemization is null || package.Items is null || package.Items.Count == 0)
            return package;

        ItemDefinition[] normalizedItems = package.Items
            .Select(item => ProceduralItemPolicy.IsEnabled(item)
                ? NormalizeTemplate(item, package.Itemization)
                : item)
            .ToArray();

        return package with { Items = normalizedItems };
    }

    public static ItemDefinition NormalizeTemplate(
        ItemDefinition template,
        ItemizationDefinition itemization)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(itemization);
        if (!ProceduralItemPolicy.IsEnabled(template))
            return template;

        EquipmentSlot canonicalSlot = CanonicalSlot(template.Slot);
        string slotId = SlotBudgetId(canonicalSlot);
        if (!itemization.SlotMultipliers.TryGetValue(slotId, out decimal slotMultiplier)
            || slotMultiplier <= 0)
        {
            throw new InvalidOperationException($"No positive itemization slot multiplier for '{slotId}'.");
        }

        string rarity = template.Rarity.ToString().ToUpperInvariant();
        if (!itemization.RarityMultipliers.TryGetValue(rarity, out decimal rarityMultiplier)
            || rarityMultiplier <= 0)
        {
            throw new InvalidOperationException($"No positive itemization rarity multiplier for '{rarity}'.");
        }

        ItemAffixCountProfileDefinition countProfile = itemization.AffixCountProfiles
            .SingleOrDefault(profile => string.Equals(profile.Id, template.AffixCountProfileId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Template '{template.Id}' has no valid affix-count profile.");
        int maxAffixCount = checked(countProfile.GuaranteedCount + countProfile.MaximumBonusCount);
        if (maxAffixCount <= 0)
            throw new InvalidOperationException($"Template '{template.Id}' has no legal affix capacity.");

        ItemAffixPoolDefinition pool = itemization.AffixPools
            .SingleOrDefault(candidate => string.Equals(candidate.Id, template.RandomAffixPoolId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Template '{template.Id}' has no valid random-affix pool.");

        decimal minimumPowerPerAffix = (template.GuaranteedAffixStatIds ?? [])
            .Concat(pool.StatIds)
            .Distinct(StringComparer.Ordinal)
            .Select(statId => MinimumPowerEnvelope(statId, itemization))
            .DefaultIfEmpty(0m)
            .Max();
        if (minimumPowerPerAffix <= 0)
            throw new InvalidOperationException($"Template '{template.Id}' has no legal positive affix power envelope.");

        decimal structuralPower = CalculateStructuralPower(template, itemization);
        decimal minimumViableTemplatePower = structuralPower + (maxAffixCount * minimumPowerPerAffix);

        int minimumItemLevel = template.ItemLevelMin ?? template.RequiredLevel;
        int maximumItemLevel = template.ItemLevelMax ?? minimumItemLevel;
        if (minimumItemLevel < 1 || maximumItemLevel < minimumItemLevel)
            throw new InvalidOperationException($"Template '{template.Id}' item-level range is invalid.");

        decimal requiredExtraBudgetCap = template.ExtraAffixBudgetCap;
        for (int itemLevel = minimumItemLevel; itemLevel <= maximumItemLevel; itemLevel++)
        {
            decimal x = itemLevel - 1;
            decimal levelMultiplier = 1m
                + (itemization.LevelLinearCoefficient * x)
                + (itemization.LevelQuadraticCoefficient * x * x);
            decimal budgetWithoutExtraCap = itemization.TemplateBasePower
                * levelMultiplier
                * slotMultiplier
                * rarityMultiplier;
            if (budgetWithoutExtraCap <= 0)
                throw new InvalidOperationException($"Template '{template.Id}' has a non-positive item power budget.");

            decimal requiredAtLevel = (minimumViableTemplatePower / budgetWithoutExtraCap) - 1m;
            requiredExtraBudgetCap = decimal.Max(requiredExtraBudgetCap, requiredAtLevel);
        }

        return requiredExtraBudgetCap > template.ExtraAffixBudgetCap
            ? template with { ExtraAffixBudgetCap = requiredExtraBudgetCap }
            : template;
    }

    public static GeneratedItemInstance RecalculateStored(
        ItemDefinition template,
        ItemizationDefinition itemization,
        int itemLevel,
        IReadOnlyList<GeneratedItemAffix> affixes,
        string? perfectOrigin = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(itemization);
        ArgumentNullException.ThrowIfNull(affixes);

        ItemDefinition normalizedTemplate = NormalizeTemplate(template, itemization);
        GeneratedItemInstance recalculated = ItemInstanceGenerator.Recalculate(
            normalizedTemplate,
            itemization,
            itemLevel,
            affixes,
            perfectOrigin);

        // Historical under-budget instances can contain 1..1 affix envelopes.
        // Such a range contains no quality information, so it must never produce
        // a HIGH prefix/suffix or an "ideal" marker after the budget fix.
        if (affixes.Any(IsCollapsedAffix))
        {
            return recalculated with
            {
                GeneratedPrefixId = null,
                GeneratedSuffixId = null,
                DisplayName = template.Name,
                IsPerfect = false,
                PerfectOrigin = null
            };
        }

        return recalculated;
    }

    private static decimal MinimumPowerEnvelope(
        string statId,
        ItemizationDefinition itemization)
    {
        if (!itemization.StatPowerWeights.TryGetValue(statId, out decimal weight) || weight <= 0)
            throw new InvalidOperationException($"Item stat '{statId}' has no positive power weight.");

        return weight * StepFor(statId) * MinimumAffixRollSteps;
    }

    private static decimal CalculateStructuralPower(
        ItemDefinition template,
        ItemizationDefinition itemization)
    {
        decimal Sum(string statId, decimal value) =>
            value == 0 || !itemization.StatPowerWeights.TryGetValue(statId, out decimal weight)
                ? 0
                : decimal.Abs(value) * weight;

        decimal weaponAverage = template.WeaponDamageMin.HasValue && template.WeaponDamageMax.HasValue
            ? (template.WeaponDamageMin.Value + template.WeaponDamageMax.Value) / 2m
            : 0;
        decimal blockAverage = template.BlockValueMin > 0 && template.BlockValueMax > 0
            ? (template.BlockValueMin + template.BlockValueMax) / 2m
            : 0;

        return Sum(ItemStatIds.Strength, template.Stats.Strength)
            + Sum(ItemStatIds.Agility, template.Stats.Agility)
            + Sum(ItemStatIds.Intellect, template.Stats.Intellect)
            + Sum(ItemStatIds.Stamina, template.Stats.Stamina)
            + Sum(ItemStatIds.MaxHp, template.MaxHpFlat)
            + Sum(ItemStatIds.AttackPower, template.AttackPowerFlat)
            + Sum(ItemStatIds.SpellPower, template.SpellPowerFlat)
            + Sum(ItemStatIds.CriticalChance, template.CriticalChancePercent)
            + Sum(ItemStatIds.CriticalDamage, template.CriticalDamagePercent)
            + Sum(ItemStatIds.Accuracy, template.AccuracyPercent)
            + Sum(ItemStatIds.AttackSpeed, template.AttackSpeedPercent)
            + Sum(ItemStatIds.Armor, template.ArmorFlat)
            + Sum(ItemStatIds.MagicResistance, template.MagicResistanceFlat)
            + Sum(ItemStatIds.Dodge, template.DodgePercent)
            + Sum(ItemStatIds.ArmorPenetration, template.ArmorPenetrationPercent)
            + Sum(ItemStatIds.MagicPenetration, template.MagicPenetrationPercent)
            + Sum(ItemStatIds.MaxResource, template.MaxResourceFlat)
            + Sum(ItemStatIds.BlockChance, template.BlockChancePercent)
            + Sum(ItemStatIds.BlockValue, blockAverage)
            + Sum(ItemStatIds.WeaponDamage, weaponAverage);
    }

    private static bool IsCollapsedAffix(GeneratedItemAffix affix) =>
        affix.MaxAtGeneration <= affix.MinAtGeneration;

    private static decimal StepFor(string statId) =>
        statId switch
        {
            ItemStatIds.MaxHp => 5m,
            _ when ItemStatIds.IsPercentage(statId) => 0.1m,
            _ => 1m
        };

    private static string SlotBudgetId(EquipmentSlot slot) =>
        slot switch
        {
            EquipmentSlot.MainHand => "MAIN_HAND",
            EquipmentSlot.OffHand => "OFF_HAND",
            EquipmentSlot.Ring1 => "RING_1",
            EquipmentSlot.Ring2 => "RING_2",
            _ => slot.ToString().ToUpperInvariant()
        };

    private static EquipmentSlot CanonicalSlot(EquipmentSlot? slot) =>
        slot switch
        {
            EquipmentSlot.Weapon => EquipmentSlot.MainHand,
            EquipmentSlot.Boots => EquipmentSlot.Feet,
            EquipmentSlot.Accessory => EquipmentSlot.Amulet,
            null => throw new InvalidOperationException("Equipment slot is required."),
            _ => slot.Value
        };
}
