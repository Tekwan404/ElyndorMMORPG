using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    internal static void ValidateItemization(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        ItemizationDefinition? itemization = package.Itemization;
        if (itemization is null)
        {
            if ((package.Items ?? []).Any(UsesProceduralItemization))
            {
                errors.Add(new(
                    "MISSING_ITEMIZATION_PROFILE",
                    "itemization",
                    "Procedural item templates require an itemization profile."));
            }
            return;
        }

        if (itemization.TemplateBasePower <= 0
            || itemization.LevelLinearCoefficient < 0
            || itemization.LevelQuadraticCoefficient < 0
            || itemization.IndividualQualityDeviationPercent is < 0 or > 100
            || itemization.PerfectSnapThreshold is <= 0 or > 1)
        {
            errors.Add(new(
                "INVALID_ITEMIZATION_PROFILE",
                "itemization",
                "Itemization power and quality configuration is invalid."));
        }

        string[] requiredSlots =
        [
            "MAIN_HAND", "OFF_HAND", "HEAD", "SHOULDERS", "CHEST", "HANDS",
            "LEGS", "FEET", "CLOAK", "AMULET", "RING_1", "RING_2"
        ];
        if (requiredSlots.Any(slot =>
                !itemization.SlotMultipliers.TryGetValue(slot, out decimal multiplier)
                || multiplier <= 0))
        {
            errors.Add(new(
                "INVALID_ITEMIZATION_SLOT_MULTIPLIERS",
                "itemization.slotMultipliers",
                "Every canonical V1 equipment slot requires a positive budget multiplier."));
        }

        string[] requiredRarities = ["COMMON", "UNCOMMON", "RARE", "EPIC", "LEGENDARY"];
        if (requiredRarities.Any(rarity =>
                !itemization.RarityMultipliers.TryGetValue(rarity, out decimal multiplier)
                || multiplier <= 0))
        {
            errors.Add(new(
                "INVALID_ITEMIZATION_RARITY_MULTIPLIERS",
                "itemization.rarityMultipliers",
                "Every normal V1 rarity requires a positive budget multiplier."));
        }

        if (itemization.StatPowerWeights.Any(pair =>
                !ItemStatIds.ApprovedV1.Contains(pair.Key) || pair.Value <= 0))
        {
            errors.Add(new(
                "INVALID_ITEMIZATION_STAT_WEIGHT",
                "itemization.statPowerWeights",
                "Itemization uses an unknown V1 stat or a non-positive power weight."));
        }

        Dictionary<string, ItemAffixPoolDefinition> pools = new(StringComparer.Ordinal);
        for (var index = 0; index < itemization.AffixPools.Count; index++)
        {
            ItemAffixPoolDefinition pool = itemization.AffixPools[index];
            string path = $"itemization.affixPools[{index}]";
            if (!IsCanonicalIdentifier(pool.Id)
                || !pools.TryAdd(pool.Id, pool)
                || pool.StatIds.Count == 0
                || pool.StatIds.Count != pool.StatIds.Distinct(StringComparer.Ordinal).Count()
                || pool.StatIds.Any(statId =>
                    !ItemStatIds.ApprovedV1.Contains(statId)
                    || !itemization.StatPowerWeights.ContainsKey(statId)))
            {
                errors.Add(new(
                    "INVALID_ITEM_AFFIX_POOL",
                    path,
                    $"Affix pool '{pool.Id}' is invalid or contains unsupported/duplicate stats."));
            }
        }

        Dictionary<string, ItemAffixCountProfileDefinition> countProfiles =
            new(StringComparer.Ordinal);
        for (var index = 0; index < itemization.AffixCountProfiles.Count; index++)
        {
            ItemAffixCountProfileDefinition profile = itemization.AffixCountProfiles[index];
            string path = $"itemization.affixCountProfiles[{index}]";
            if (!IsCanonicalIdentifier(profile.Id)
                || !countProfiles.TryAdd(profile.Id, profile)
                || profile.GuaranteedCount < 0
                || profile.MinimumBonusCount < 0
                || profile.MaximumBonusCount < profile.MinimumBonusCount
                || profile.GuaranteedCount + profile.MaximumBonusCount <= 0)
            {
                errors.Add(new(
                    "INVALID_ITEM_AFFIX_COUNT_PROFILE",
                    path,
                    $"Affix-count profile '{profile.Id}' is invalid."));
            }
        }

        HashSet<string> qualityIds = new(StringComparer.Ordinal);
        for (var index = 0; index < itemization.QualityProfiles.Count; index++)
        {
            ItemQualityProfileDefinition profile = itemization.QualityProfiles[index];
            if (!IsCanonicalIdentifier(profile.Id)
                || !qualityIds.Add(profile.Id)
                || profile.BiasExponent <= 0)
            {
                errors.Add(new(
                    "INVALID_ITEM_QUALITY_PROFILE",
                    $"itemization.qualityProfiles[{index}]",
                    $"Quality profile '{profile.Id}' is invalid."));
            }
        }

        if (itemization.ReforgeCosts is { } reforge)
        {
            string[] rarityIds = ["COMMON", "UNCOMMON", "RARE", "EPIC", "LEGENDARY", "UNIQUE"];
            ItemDefinition? material = (package.Items ?? []).FirstOrDefault(item =>
                string.Equals(item.Id, reforge.MaterialItemId, StringComparison.Ordinal));
            ItemDefinition? catalyst = (package.Items ?? []).FirstOrDefault(item =>
                string.Equals(item.Id, reforge.CatalystItemId, StringComparison.Ordinal));
            bool invalidCosts =
                !IsCanonicalIdentifier(reforge.Id)
                || material is null
                || material.Type != ItemType.Material
                || catalyst is null
                || catalyst.Type != ItemType.Material
                || reforge.ReforgeCountMultipliers.Count == 0
                || reforge.ReforgeCountMultipliers.Any(multiplier => multiplier <= 0)
                || reforge.OverflowGrowthMultiplier < 1
                || !qualityIds.Contains("REFORGE")
                || reforge.CatalystMinimumRarity is not ("EPIC" or "LEGENDARY" or "UNIQUE")
                || rarityIds.Any(rarity =>
                    !reforge.BaseGoldByRarity.TryGetValue(rarity, out int gold)
                    || gold < 0
                    || !reforge.MaterialQuantityByRarity.TryGetValue(rarity, out int materialQuantity)
                    || materialQuantity < 0
                    || !reforge.CatalystQuantityByRarity.TryGetValue(rarity, out int catalystQuantity)
                    || catalystQuantity < 0);

            if (invalidCosts)
            {
                errors.Add(new(
                    "INVALID_ITEM_REFORGE_COST_PROFILE",
                    "itemization.reforgeCosts",
                    "Reforge costs require valid material/catalyst items, positive growth and complete rarity tables."));
            }
        }

        if (itemization.Salvage is { } salvage)
        {
            string[] rarityIds = ["COMMON", "UNCOMMON", "RARE", "EPIC", "LEGENDARY", "UNIQUE"];
            ItemDefinition? stone = (package.Items ?? []).FirstOrDefault(item =>
                string.Equals(item.Id, salvage.ReforgeStoneItemId, StringComparison.Ordinal));
            ItemDefinition? material = (package.Items ?? []).FirstOrDefault(item =>
                string.Equals(item.Id, salvage.MaterialItemId, StringComparison.Ordinal));
            bool invalidSalvage = !IsCanonicalIdentifier(salvage.Id)
                || stone?.Type != ItemType.Material
                || material?.Type != ItemType.Material
                || salvage.ItemLevelStep <= 0
                || salvage.ReforgeStonesPerLevelStep < 0
                || salvage.MaterialsPerLevelStep < 0
                || salvage.StarsPerBonusStone <= 0
                || salvage.HighValueConfirmationRarity is not ("RARE" or "EPIC" or "LEGENDARY" or "UNIQUE")
                || rarityIds.Any(rarity =>
                    !salvage.ReforgeStoneQuantityByRarity.TryGetValue(rarity, out int stones)
                    || stones <= 0
                    || !salvage.MaterialQuantityByRarity.TryGetValue(rarity, out int materials)
                    || materials < 0);
            if (invalidSalvage)
            {
                errors.Add(new(
                    "INVALID_ITEM_SALVAGE_PROFILE",
                    "itemization.salvage",
                    "Salvage requires material rewards, valid progression values, and complete rarity tables."));
            }
        }

        HashSet<string> nameIds = new(StringComparer.Ordinal);
        for (var index = 0; index < itemization.AffixNames.Count; index++)
        {
            ItemAffixNameDefinition name = itemization.AffixNames[index];
            if (!IsCanonicalIdentifier(name.Id)
                || !nameIds.Add(name.Id)
                || !ItemStatIds.ApprovedV1.Contains(name.StatId)
                || name.Kind is not ("PREFIX" or "SUFFIX")
                || string.IsNullOrWhiteSpace(name.Low)
                || string.IsNullOrWhiteSpace(name.Medium)
                || string.IsNullOrWhiteSpace(name.High))
            {
                errors.Add(new(
                    "INVALID_ITEM_AFFIX_NAME",
                    $"itemization.affixNames[{index}]",
                    $"Affix naming definition '{name.Id}' is invalid."));
            }
        }

        IReadOnlyList<ItemDefinition> items = package.Items ?? [];
        for (var index = 0; index < items.Count; index++)
        {
            ItemDefinition item = items[index];
            if (!UsesProceduralItemization(item))
                continue;

            string path = $"items[{index}]";
            if (item.Type != ItemType.Equipment
                || item.Slot is null
                || string.IsNullOrWhiteSpace(item.RandomAffixPoolId)
                || string.IsNullOrWhiteSpace(item.AffixCountProfileId)
                || !pools.TryGetValue(item.RandomAffixPoolId, out ItemAffixPoolDefinition? pool)
                || !countProfiles.TryGetValue(item.AffixCountProfileId, out ItemAffixCountProfileDefinition? countProfile)
                || item.GenerationVersion < 1
                || item.ExtraAffixBudgetCap is < 0 or > 0.08m)
            {
                errors.Add(new(
                    "INVALID_PROCEDURAL_ITEM_TEMPLATE",
                    path,
                    $"Item '{item.Id}' has an incomplete or invalid procedural generation policy."));
                continue;
            }

            int minimumLevel = item.ItemLevelMin ?? item.RequiredLevel;
            int maximumLevel = item.ItemLevelMax ?? minimumLevel;
            string[] guaranteed = (item.GuaranteedAffixStatIds ?? []).ToArray();
            bool forbiddenSetStat = item.SetId is not null
                && item.AllowedClassIds is { Count: 1 }
                && guaranteed.Concat(pool.StatIds).Any(statId =>
                    IsForbiddenForClass(item.AllowedClassIds[0], statId));

            if (minimumLevel < 1
                || maximumLevel < minimumLevel
                || guaranteed.Length != countProfile.GuaranteedCount
                || guaranteed.Length != guaranteed.Distinct(StringComparer.Ordinal).Count()
                || guaranteed.Any(statId =>
                    !ItemStatIds.ApprovedV1.Contains(statId)
                    || !itemization.StatPowerWeights.ContainsKey(statId))
                || pool.StatIds.Concat(guaranteed).Distinct(StringComparer.Ordinal).Count()
                    < countProfile.GuaranteedCount + countProfile.MaximumBonusCount
                || forbiddenSetStat)
            {
                errors.Add(new(
                    "INVALID_PROCEDURAL_ITEM_AFFIX_POLICY",
                    path,
                    $"Item '{item.Id}' has impossible, duplicate, unsupported, or class-incompatible affix rules."));
            }

            decimal allowedExtraCap = item.Rarity switch
            {
                ItemRarity.Rare => 0.04m,
                ItemRarity.Epic => 0.06m,
                ItemRarity.Legendary or ItemRarity.Unique => 0.08m,
                _ => 0m
            };
            if (item.ExtraAffixBudgetCap > allowedExtraCap)
            {
                errors.Add(new(
                    "ITEM_EXTRA_AFFIX_BUDGET_EXCEEDED",
                    $"{path}.extraAffixBudgetCap",
                    $"Item '{item.Id}' exceeds the V1 optional-affix budget cap for its rarity."));
            }
        }
    }

    private static bool UsesProceduralItemization(ItemDefinition item) =>
        item.ItemLevelMin.HasValue
        || item.ItemLevelMax.HasValue
        || item.GuaranteedAffixStatIds is { Count: > 0 }
        || item.RandomAffixPoolId is not null
        || item.AffixCountProfileId is not null
        || item.ExtraAffixBudgetCap != 0
        || item.PrefixSuffixPolicyId is not null
        || item.UniqueEquippedGroup is not null
        || item.TradePolicyId is not null
        || item.GenerationVersion != 1;

    private static bool IsForbiddenForClass(string classId, string statId) =>
        classId switch
        {
            "WARRIOR" => statId is ItemStatIds.Intellect or ItemStatIds.SpellPower
                or ItemStatIds.MagicPenetration,
            "MAGE" => statId is ItemStatIds.Strength or ItemStatIds.Agility
                or ItemStatIds.AttackPower or ItemStatIds.ArmorPenetration,
            "ARCHER" => statId is ItemStatIds.Strength or ItemStatIds.Intellect
                or ItemStatIds.SpellPower or ItemStatIds.MagicPenetration,
            _ => false
        };
}
