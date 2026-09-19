using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

/// <summary>
/// Builds a per-template runtime itemization envelope large enough for every legal
/// affix to have a real roll range. Published slot/rarity multipliers and rarity
/// extra-budget caps remain unchanged.
/// </summary>
public static class ItemizationBudgetPolicy
{
    private const decimal MinimumAffixQuality = 0.40m;
    private const int MinimumAffixRollSteps = 2;

    public static ItemizationDefinition NormalizeForTemplate(
        ItemDefinition template,
        ItemizationDefinition itemization)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(itemization);
        if (!ProceduralItemPolicy.IsEnabled(template))
            return itemization;

        EquipmentSlot canonicalSlot = CanonicalSlot(template.Slot);
        string slotId = SlotBudgetId(canonicalSlot);
        if (!itemization.SlotMultipliers.TryGetValue(slotId, out decimal configuredSlotMultiplier)
            || configuredSlotMultiplier <= 0)
        {
            throw new InvalidOperationException($"No positive itemization slot multiplier for '{slotId}'.");
        }

        string rarity = template.Rarity.ToString().ToUpperInvariant();
        if (!itemization.RarityMultipliers.TryGetValue(rarity, out decimal rarityMultiplier)
            || rarityMultiplier <= 0)
        {
            throw new InvalidOperationException($"No positive itemization rarity multiplier for '{rarity}'.");
        }

        ItemAffixCountProfileDefinition countProfile = FindCountProfile(template, itemization);
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

        decimal requiredSlotMultiplier = configuredSlotMultiplier;
        for (int itemLevel = minimumItemLevel; itemLevel <= maximumItemLevel; itemLevel++)
        {
            decimal x = itemLevel - 1;
            decimal levelMultiplier = 1m
                + (itemization.LevelLinearCoefficient * x)
                + (itemization.LevelQuadraticCoefficient * x * x);
            decimal budgetWithoutSlot = itemization.TemplateBasePower
                * levelMultiplier
                * rarityMultiplier
                * (1m + template.ExtraAffixBudgetCap);
            if (budgetWithoutSlot <= 0)
                throw new InvalidOperationException($"Template '{template.Id}' has a non-positive item power budget.");

            requiredSlotMultiplier = decimal.Max(
                requiredSlotMultiplier,
                minimumViableTemplatePower / budgetWithoutSlot);
        }

        if (requiredSlotMultiplier <= configuredSlotMultiplier)
            return itemization;

        Dictionary<string, decimal> slotMultipliers = itemization.SlotMultipliers
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        slotMultipliers[slotId] = requiredSlotMultiplier;
        return itemization with { SlotMultipliers = slotMultipliers };
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

        bool hadCollapsedAffix = affixes.Any(IsCollapsedAffix);
        ItemizationDefinition effectiveItemization = NormalizeForTemplate(template, itemization);
        GeneratedItemAffix[] effectiveAffixes = ReferenceEquals(effectiveItemization, itemization)
            ? affixes.ToArray()
            : NormalizeStoredAffixEnvelopes(
                template,
                effectiveItemization,
                itemLevel,
                affixes);
        GeneratedItemInstance recalculated = ItemInstanceGenerator.Recalculate(
            template,
            effectiveItemization,
            itemLevel,
            effectiveAffixes,
            perfectOrigin);

        // Historical under-budget instances can contain 1..1 affix envelopes.
        // The actual stat values are preserved, but the old envelope contains no
        // trustworthy naming/perfect-quality information.
        if (hadCollapsedAffix)
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

    private static GeneratedItemAffix[] NormalizeStoredAffixEnvelopes(
        ItemDefinition template,
        ItemizationDefinition itemization,
        int itemLevel,
        IReadOnlyList<GeneratedItemAffix> affixes)
    {
        decimal maxTemplatePower = ItemInstanceGenerator.CalculateTemplateMaxPower(
            template,
            itemization,
            itemLevel);
        decimal structuralPower = CalculateStructuralPower(template, itemization);
        ItemAffixCountProfileDefinition countProfile = FindCountProfile(template, itemization);
        int maxAffixCount = checked(countProfile.GuaranteedCount + countProfile.MaximumBonusCount);
        decimal maxPowerPerAffix = (maxTemplatePower - structuralPower) / maxAffixCount;

        return affixes.Select(affix =>
        {
            if (!itemization.StatPowerWeights.TryGetValue(affix.StatId, out decimal weight) || weight <= 0)
                throw new InvalidOperationException($"Item stat '{affix.StatId}' has no positive power weight.");

            decimal step = StepFor(affix.StatId);
            decimal maximumValue = FloorToStep(maxPowerPerAffix / weight, step);
            if (maximumValue <= 0)
                maximumValue = step;
            decimal minimumValue = FloorToStep(maximumValue * MinimumAffixQuality, step);
            if (minimumValue <= 0)
                minimumValue = step;
            if (minimumValue > maximumValue)
                minimumValue = maximumValue;

            return affix with
            {
                MinAtGeneration = minimumValue,
                MaxAtGeneration = maximumValue,
                StepAtGeneration = step
            };
        }).ToArray();
    }

    private static ItemAffixCountProfileDefinition FindCountProfile(
        ItemDefinition template,
        ItemizationDefinition itemization) =>
        itemization.AffixCountProfiles.SingleOrDefault(profile =>
            string.Equals(profile.Id, template.AffixCountProfileId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"Template '{template.Id}' has no valid affix-count profile.");

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

    private static decimal FloorToStep(decimal value, decimal step) =>
        decimal.Floor(value / step) * step;

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
