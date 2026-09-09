using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

public static class ItemStatIds
{
    public const string Strength = "STRENGTH";
    public const string Agility = "AGILITY";
    public const string Intellect = "INTELLECT";
    public const string Stamina = "STAMINA";
    public const string MaxHp = "MAX_HP";
    public const string AttackPower = "ATTACK_POWER";
    public const string SpellPower = "SPELL_POWER";
    public const string CriticalChance = "CRITICAL_CHANCE";
    public const string CriticalDamage = "CRITICAL_DAMAGE";
    public const string Accuracy = "ACCURACY";
    public const string AttackSpeed = "ATTACK_SPEED";
    public const string Armor = "ARMOR";
    public const string MagicResistance = "MAGIC_RESISTANCE";
    public const string Dodge = "DODGE";
    public const string ArmorPenetration = "ARMOR_PENETRATION";
    public const string MagicPenetration = "MAGIC_PENETRATION";
    public const string MaxResource = "MAX_RESOURCE";
    public const string BlockChance = "BLOCK_CHANCE";
    public const string BlockValue = "BLOCK_VALUE";
    public const string WeaponDamage = "WEAPON_DAMAGE";

    public static IReadOnlySet<string> ApprovedV1 { get; } = new HashSet<string>(
        [
            Strength, Agility, Intellect, Stamina, MaxHp, AttackPower, SpellPower,
            CriticalChance, CriticalDamage, Accuracy, AttackSpeed, Armor,
            MagicResistance, Dodge, ArmorPenetration, MagicPenetration, MaxResource,
            BlockChance, BlockValue, WeaponDamage
        ],
        StringComparer.Ordinal);

    public static bool IsPercentage(string statId) =>
        statId is CriticalChance or CriticalDamage or Accuracy or AttackSpeed
            or Dodge or ArmorPenetration or MagicPenetration or BlockChance;
}

public sealed record ItemAffixPoolDefinition(
    string Id,
    IReadOnlyList<string> StatIds);

public sealed record ItemAffixCountProfileDefinition(
    string Id,
    int GuaranteedCount,
    int MinimumBonusCount,
    int MaximumBonusCount);

public sealed record ItemQualityProfileDefinition(
    string Id,
    decimal BiasExponent);

public sealed record ItemAffixNameDefinition(
    string Id,
    string StatId,
    string Kind,
    string Low,
    string Medium,
    string High);

public sealed record ItemizationDefinition(
    decimal TemplateBasePower,
    decimal LevelLinearCoefficient,
    decimal LevelQuadraticCoefficient,
    IReadOnlyDictionary<string, decimal> SlotMultipliers,
    IReadOnlyDictionary<string, decimal> RarityMultipliers,
    IReadOnlyDictionary<string, decimal> StatPowerWeights,
    IReadOnlyList<ItemAffixPoolDefinition> AffixPools,
    IReadOnlyList<ItemAffixCountProfileDefinition> AffixCountProfiles,
    IReadOnlyList<ItemQualityProfileDefinition> QualityProfiles,
    IReadOnlyList<ItemAffixNameDefinition> AffixNames,
    decimal IndividualQualityDeviationPercent = 7,
    decimal PerfectSnapThreshold = 0.9995m);

public sealed record GeneratedItemAffix(
    string SlotKey,
    string AffixDefinitionId,
    string StatId,
    decimal Value,
    decimal MinAtGeneration,
    decimal MaxAtGeneration,
    decimal StepAtGeneration,
    int AffixTier,
    bool IsGuaranteed,
    bool IsReforgeSlot,
    int GenerationOrdinal);

public sealed record GeneratedItemInstance(
    int ItemLevel,
    IReadOnlyList<GeneratedItemAffix> Affixes,
    decimal MinimumTemplateItemPower,
    decimal ActualItemPower,
    decimal MaxTemplateItemPower,
    decimal RollQuality,
    int Stars,
    bool IsPerfect,
    string? PerfectOrigin,
    string? GeneratedPrefixId,
    string? GeneratedSuffixId,
    string DisplayName,
    int GenerationVersion);

public static class ItemInstanceGenerator
{
    private const decimal MinimumAffixQuality = 0.40m;

    public static GeneratedItemInstance Generate(
        ItemDefinition template,
        ItemizationDefinition itemization,
        string sourceQualityProfileId,
        IGameRandom random,
        string perfectOrigin = "DROP")
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(itemization);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceQualityProfileId);
        ArgumentNullException.ThrowIfNull(random);

        if (template.Type != ItemType.Equipment || template.Slot is null)
            throw new InvalidOperationException("Only equipment templates can generate equipment instances.");

        int itemLevel = ResolveItemLevel(template, random);
        decimal maxTemplatePower = CalculateTemplateMaxPower(template, itemization, itemLevel);
        decimal structuralPower = CalculateStructuralPower(template, itemization);
        if (maxTemplatePower <= structuralPower)
            maxTemplatePower = structuralPower + 1;

        ItemAffixCountProfileDefinition countProfile = FindCountProfile(template, itemization);
        string[] guaranteed = (template.GuaranteedAffixStatIds ?? []).ToArray();
        if (guaranteed.Length != countProfile.GuaranteedCount)
        {
            throw new InvalidOperationException(
                $"Template '{template.Id}' guaranteed affix count does not match profile '{countProfile.Id}'.");
        }

        ItemAffixPoolDefinition pool = FindPool(template, itemization);
        int bonusCount = RollInclusive(
            countProfile.MinimumBonusCount,
            countProfile.MaximumBonusCount,
            random);
        int maxAffixCount = checked(countProfile.GuaranteedCount + countProfile.MaximumBonusCount);
        if (maxAffixCount <= 0)
            throw new InvalidOperationException($"Template '{template.Id}' has no legal affix capacity.");

        List<(string StatId, bool Guaranteed)> selected = guaranteed
            .Select(statId => (statId, true))
            .ToList();
        HashSet<string> selectedIds = guaranteed.ToHashSet(StringComparer.Ordinal);
        List<string> candidates = pool.StatIds
            .Where(statId => !selectedIds.Contains(statId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        for (var index = 0; index < bonusCount; index++)
        {
            if (candidates.Count == 0)
                throw new InvalidOperationException($"Template '{template.Id}' affix pool cannot satisfy unique-affix rules.");
            int choice = RollIndex(candidates.Count, random);
            string statId = candidates[choice];
            candidates.RemoveAt(choice);
            selectedIds.Add(statId);
            selected.Add((statId, false));
        }

        ItemQualityProfileDefinition qualityProfile = itemization.QualityProfiles
            .SingleOrDefault(profile => string.Equals(profile.Id, sourceQualityProfileId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Unknown item quality profile '{sourceQualityProfileId}'.");
        decimal seedQuality = RollQuality(random, qualityProfile.BiasExponent, itemization.PerfectSnapThreshold);
        decimal affixBudget = maxTemplatePower - structuralPower;
        decimal maxPowerPerAffix = affixBudget / maxAffixCount;

        List<GeneratedItemAffix> affixes = [];
        decimal actualPower = structuralPower;
        decimal minimumTemplatePower = structuralPower
            + (countProfile.GuaranteedCount * maxPowerPerAffix * MinimumAffixQuality);
        bool allAtMaximum = selected.Count == maxAffixCount;
        for (var index = 0; index < selected.Count; index++)
        {
            (string statId, bool isGuaranteed) = selected[index];
            if (!itemization.StatPowerWeights.TryGetValue(statId, out decimal weight) || weight <= 0)
                throw new InvalidOperationException($"Item stat '{statId}' has no positive power weight.");

            decimal step = StepFor(statId);
            decimal maximumValue = FloorToStep(maxPowerPerAffix / weight, step);
            if (maximumValue <= 0)
                maximumValue = step;
            decimal minimumValue = FloorToStep(maximumValue * MinimumAffixQuality, step);
            if (minimumValue <= 0)
                minimumValue = step;
            if (minimumValue > maximumValue)
                minimumValue = maximumValue;

            decimal deviationUnit = (random.NextUnit() * 2m) - 1m;
            decimal individualQuality = Clamp01(
                seedQuality + (deviationUnit * itemization.IndividualQualityDeviationPercent / 100m));
            if (individualQuality >= itemization.PerfectSnapThreshold)
                individualQuality = 1m;

            decimal value = RollValue(minimumValue, maximumValue, step, individualQuality);
            actualPower += value * weight;
            allAtMaximum &= value == maximumValue;
            affixes.Add(new GeneratedItemAffix(
                $"AFFIX_{index + 1}",
                statId,
                statId,
                value,
                minimumValue,
                maximumValue,
                step,
                AffixTier(itemLevel),
                isGuaranteed,
                false,
                index));
        }

        actualPower = decimal.Min(actualPower, maxTemplatePower);
        decimal denominator = maxTemplatePower - minimumTemplatePower;
        decimal realizedPotential = denominator <= 0
            ? 1m
            : Clamp01((actualPower - minimumTemplatePower) / denominator);
        decimal rollQuality = decimal.Round(realizedPotential * 100m, 2, MidpointRounding.AwayFromZero);
        int stars = StarsFor(realizedPotential);
        bool isPerfect = allAtMaximum
            && decimal.Abs(actualPower - maxTemplatePower) <= 0.01m;

        (string? prefixId, string? suffixId, string displayName) =
            ResolveGeneratedName(template, itemization, affixes);

        return new GeneratedItemInstance(
            itemLevel,
            affixes,
            decimal.Round(minimumTemplatePower, 2, MidpointRounding.AwayFromZero),
            decimal.Round(actualPower, 2, MidpointRounding.AwayFromZero),
            decimal.Round(maxTemplatePower, 2, MidpointRounding.AwayFromZero),
            rollQuality,
            stars,
            isPerfect,
            isPerfect ? perfectOrigin : null,
            prefixId,
            suffixId,
            displayName,
            template.GenerationVersion);
    }

    public static decimal CalculateTemplateMaxPower(
        ItemDefinition template,
        ItemizationDefinition itemization,
        int itemLevel)
    {
        string slot = SlotBudgetId(CanonicalSlot(template.Slot));
        string rarity = template.Rarity.ToString().ToUpperInvariant();
        if (!itemization.SlotMultipliers.TryGetValue(slot, out decimal slotMultiplier))
            throw new InvalidOperationException($"No itemization slot multiplier for '{slot}'.");
        if (!itemization.RarityMultipliers.TryGetValue(rarity, out decimal rarityMultiplier))
            throw new InvalidOperationException($"No itemization rarity multiplier for '{rarity}'.");

        decimal x = itemLevel - 1;
        decimal levelMultiplier = 1m
            + (itemization.LevelLinearCoefficient * x)
            + (itemization.LevelQuadraticCoefficient * x * x);
        return itemization.TemplateBasePower
            * levelMultiplier
            * slotMultiplier
            * rarityMultiplier
            * (1m + template.ExtraAffixBudgetCap);
    }

    public static ItemDefinition ApplyGeneratedAffixes(
        ItemDefinition template,
        IReadOnlyList<GeneratedItemAffix> affixes,
        string? displayName = null)
    {
        PrimaryStats stats = template.Stats;
        decimal maxHp = template.MaxHpFlat;
        decimal attackPower = template.AttackPowerFlat;
        decimal spellPower = template.SpellPowerFlat;
        decimal crit = template.CriticalChancePercent;
        decimal critDamage = template.CriticalDamagePercent;
        decimal accuracy = template.AccuracyPercent;
        decimal attackSpeed = template.AttackSpeedPercent;
        decimal armor = template.ArmorFlat;
        decimal magicResistance = template.MagicResistanceFlat;
        decimal dodge = template.DodgePercent;
        decimal armorPen = template.ArmorPenetrationPercent;
        decimal magicPen = template.MagicPenetrationPercent;
        decimal maxResource = template.MaxResourceFlat;
        decimal blockChance = template.BlockChancePercent;
        decimal blockMin = template.BlockValueMin;
        decimal blockMax = template.BlockValueMax;

        foreach (GeneratedItemAffix affix in affixes)
        {
            switch (affix.StatId)
            {
                case ItemStatIds.Strength: stats = stats with { Strength = stats.Strength + affix.Value }; break;
                case ItemStatIds.Agility: stats = stats with { Agility = stats.Agility + affix.Value }; break;
                case ItemStatIds.Intellect: stats = stats with { Intellect = stats.Intellect + affix.Value }; break;
                case ItemStatIds.Stamina: stats = stats with { Stamina = stats.Stamina + affix.Value }; break;
                case ItemStatIds.MaxHp: maxHp += affix.Value; break;
                case ItemStatIds.AttackPower: attackPower += affix.Value; break;
                case ItemStatIds.SpellPower: spellPower += affix.Value; break;
                case ItemStatIds.CriticalChance: crit += affix.Value; break;
                case ItemStatIds.CriticalDamage: critDamage += affix.Value; break;
                case ItemStatIds.Accuracy: accuracy += affix.Value; break;
                case ItemStatIds.AttackSpeed: attackSpeed += affix.Value; break;
                case ItemStatIds.Armor: armor += affix.Value; break;
                case ItemStatIds.MagicResistance: magicResistance += affix.Value; break;
                case ItemStatIds.Dodge: dodge += affix.Value; break;
                case ItemStatIds.ArmorPenetration: armorPen += affix.Value; break;
                case ItemStatIds.MagicPenetration: magicPen += affix.Value; break;
                case ItemStatIds.MaxResource: maxResource += affix.Value; break;
                case ItemStatIds.BlockChance: blockChance += affix.Value; break;
                case ItemStatIds.BlockValue:
                    blockMin += affix.Value;
                    blockMax += affix.Value;
                    break;
            }
        }

        return template with
        {
            Name = displayName ?? template.Name,
            Stats = stats,
            MaxHpFlat = maxHp,
            AttackPowerFlat = attackPower,
            SpellPowerFlat = spellPower,
            CriticalChancePercent = crit,
            CriticalDamagePercent = critDamage,
            AccuracyPercent = accuracy,
            AttackSpeedPercent = attackSpeed,
            ArmorFlat = armor,
            MagicResistanceFlat = magicResistance,
            DodgePercent = dodge,
            ArmorPenetrationPercent = armorPen,
            MagicPenetrationPercent = magicPen,
            MaxResourceFlat = maxResource,
            BlockChancePercent = blockChance,
            BlockValueMin = blockMin,
            BlockValueMax = blockMax
        };
    }

    private static int ResolveItemLevel(ItemDefinition template, IGameRandom random)
    {
        int minimum = template.ItemLevelMin ?? template.RequiredLevel;
        int maximum = template.ItemLevelMax ?? minimum;
        if (minimum < 1 || maximum < minimum)
            throw new InvalidOperationException($"Template '{template.Id}' item-level range is invalid.");
        return minimum == maximum
            ? minimum
            : minimum + RollIndex(maximum - minimum + 1, random);
    }

    private static ItemAffixCountProfileDefinition FindCountProfile(
        ItemDefinition template,
        ItemizationDefinition itemization) =>
        itemization.AffixCountProfiles.SingleOrDefault(profile =>
            string.Equals(profile.Id, template.AffixCountProfileId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"Template '{template.Id}' has no valid affix-count profile.");

    private static ItemAffixPoolDefinition FindPool(
        ItemDefinition template,
        ItemizationDefinition itemization) =>
        itemization.AffixPools.SingleOrDefault(pool =>
            string.Equals(pool.Id, template.RandomAffixPoolId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"Template '{template.Id}' has no valid random-affix pool.");

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

    private static decimal RollQuality(
        IGameRandom random,
        decimal biasExponent,
        decimal perfectSnapThreshold)
    {
        if (biasExponent <= 0)
            throw new InvalidOperationException("Quality bias exponent must be positive.");
        decimal unit = random.NextUnit();
        if (unit >= perfectSnapThreshold)
            return 1m;
        double shaped = Math.Pow((double)unit, (double)biasExponent);
        return Clamp01((decimal)shaped);
    }

    private static decimal RollValue(
        decimal minimum,
        decimal maximum,
        decimal step,
        decimal quality)
    {
        if (maximum <= minimum)
            return maximum;
        decimal raw = minimum + ((maximum - minimum) * Clamp01(quality));
        decimal steps = decimal.Floor((raw - minimum) / step);
        return decimal.Min(maximum, minimum + (steps * step));
    }

    private static (string? PrefixId, string? SuffixId, string DisplayName) ResolveGeneratedName(
        ItemDefinition template,
        ItemizationDefinition itemization,
        IReadOnlyList<GeneratedItemAffix> affixes)
    {
        if (template.SetId is not null || string.IsNullOrWhiteSpace(template.PrefixSuffixPolicyId))
            return (null, null, template.Name);

        GeneratedItemAffix? prefixAffix = affixes
            .Where(affix => affix.StatId is ItemStatIds.Strength or ItemStatIds.Agility
                or ItemStatIds.Intellect or ItemStatIds.Stamina)
            .OrderByDescending(affix => QualityOf(affix))
            .FirstOrDefault();
        GeneratedItemAffix? suffixAffix = affixes
            .Where(affix => affix.StatId is not ItemStatIds.Strength
                and not ItemStatIds.Agility and not ItemStatIds.Intellect and not ItemStatIds.Stamina)
            .OrderByDescending(affix => QualityOf(affix))
            .FirstOrDefault();

        (string? prefixId, string? prefix) = ResolveNamePart(itemization, prefixAffix, "PREFIX");
        (string? suffixId, string? suffix) = ResolveNamePart(itemization, suffixAffix, "SUFFIX");
        string name = string.Join(
            " ",
            new[] { prefix, template.Name, suffix }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return (prefixId, suffixId, name);
    }

    private static (string? Id, string? Text) ResolveNamePart(
        ItemizationDefinition itemization,
        GeneratedItemAffix? affix,
        string kind)
    {
        if (affix is null) return (null, null);
        ItemAffixNameDefinition? definition = itemization.AffixNames.FirstOrDefault(name =>
            string.Equals(name.StatId, affix.StatId, StringComparison.Ordinal)
            && string.Equals(name.Kind, kind, StringComparison.Ordinal));
        if (definition is null) return (null, null);
        decimal quality = QualityOf(affix);
        string tier = quality >= 0.80m ? "HIGH" : quality >= 0.55m ? "MEDIUM" : "LOW";
        string text = tier switch
        {
            "HIGH" => definition.High,
            "MEDIUM" => definition.Medium,
            _ => definition.Low
        };
        return ($"{definition.Id}_{tier}", text);
    }

    private static decimal QualityOf(GeneratedItemAffix affix) =>
        affix.MaxAtGeneration <= affix.MinAtGeneration
            ? 1m
            : Clamp01(
                (affix.Value - affix.MinAtGeneration)
                / (affix.MaxAtGeneration - affix.MinAtGeneration));

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

    private static int RollInclusive(int minimum, int maximum, IGameRandom random)
    {
        if (minimum < 0 || maximum < minimum)
            throw new InvalidOperationException("Affix-count profile is invalid.");
        if (minimum == maximum) return minimum;
        return minimum + RollIndex(maximum - minimum + 1, random);
    }

    private static int RollIndex(int count, IGameRandom random)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        int index = (int)decimal.Floor(random.NextUnit() * count);
        return Math.Min(index, count - 1);
    }

    private static decimal StepFor(string statId) =>
        statId switch
        {
            ItemStatIds.MaxHp => 5m,
            _ when ItemStatIds.IsPercentage(statId) => 0.1m,
            _ => 1m
        };

    private static int AffixTier(int itemLevel) =>
        itemLevel >= 17 ? 4 : itemLevel >= 13 ? 3 : itemLevel >= 9 ? 2 : 1;

    private static int StarsFor(decimal realizedPotential) =>
        realizedPotential >= 0.90m ? 5
        : realizedPotential >= 0.70m ? 4
        : realizedPotential >= 0.50m ? 3
        : realizedPotential >= 0.30m ? 2
        : 1;

    private static decimal FloorToStep(decimal value, decimal step) =>
        decimal.Floor(value / step) * step;

    private static decimal Clamp01(decimal value) =>
        decimal.Max(0m, decimal.Min(1m, value));
}
