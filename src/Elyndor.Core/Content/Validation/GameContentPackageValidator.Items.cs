using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
        internal static void ValidateProgressionItemsAndLoot(
            GameContentPackage package,
            List<ContentValidationError> errors)
        {
            bool hasPhaseFiveContent = package.LevelProgression is not null
                || package.Items is not null
                || package.LootTables is not null;
            if (!hasPhaseFiveContent) return;

            if (package.LevelProgression is not { } progression)
            {
                errors.Add(new("MISSING_LEVEL_PROGRESSION", "levelProgression",
                    "Phase 5 content requires a level progression definition."));
            }
            else if (!IsCanonicalIdentifier(progression.Id)
                || progression.MaxLevel < 2
                || progression.XpAnchors.Count < 2
                || progression.XpAnchors.Any(anchor =>
                    anchor.Level < 1
                    || anchor.Level >= progression.MaxLevel
                    || anchor.XpToNext <= 0))
            {
                errors.Add(new("INVALID_LEVEL_PROGRESSION", "levelProgression",
                    "Level progression contains values outside its valid range."));
            }

            if (package.InventoryProfile is { DefaultCapacity: <= 0 })
            {
                errors.Add(new(
                    "INVALID_INVENTORY_PROFILE",
                    "inventoryProfile.defaultCapacity",
                    "Inventory capacity must be positive."));
            }

            IReadOnlyList<ItemDefinition> items = package.Items ?? [];
            HashSet<string> classProfileIds = (package.ClassProfiles ?? [])
                .Select(profile => profile.Id)
                .ToHashSet(StringComparer.Ordinal);
            IReadOnlyList<EquipmentSetDefinition> equipmentSets = package.EquipmentSets ?? [];
            HashSet<string> equipmentSetIds = equipmentSets
                .Select(set => set.Id)
                .ToHashSet(StringComparer.Ordinal);
            for (var setIndex = 0; setIndex < equipmentSets.Count; setIndex++)
            {
                EquipmentSetDefinition set = equipmentSets[setIndex];
                string setPath = $"equipmentSets[{setIndex}]";
                if (!ValidateIdentifier(
                        set.Id,
                        "INVALID_EQUIPMENT_SET_ID",
                        $"{setPath}.id",
                        errors))
                {
                    continue;
                }

                if (set.AllowedClassIds is not null)
                {
                    bool invalidSetClassRestriction =
                        set.AllowedClassIds.Count == 0
                        || set.AllowedClassIds.Distinct(StringComparer.Ordinal).Count()
                            != set.AllowedClassIds.Count
                        || set.AllowedClassIds.Any(classId =>
                            !IsCanonicalIdentifier(classId)
                            || !classProfileIds.Contains(classId));
                    if (invalidSetClassRestriction)
                    {
                        errors.Add(new(
                            "INVALID_EQUIPMENT_SET_CLASS_RESTRICTION",
                            $"{setPath}.allowedClassIds",
                            $"Equipment set '{set.Id}' has an invalid class restriction."));
                    }
                }
            }

            Dictionary<string, ItemDefinition> itemsById = new(StringComparer.Ordinal);
            HashSet<string> itemFamilyIds = new(StringComparer.Ordinal);
            HashSet<string> resourceProfileIds = (package.ResourceProfiles ?? [])
                .Select(profile => profile.Id)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> effectIds = (package.Effects ?? [])
                .Select(effect => effect.Id)
                .ToHashSet(StringComparer.Ordinal);
            for (var index = 0; index < items.Count; index++)
            {
                ItemDefinition item = items[index];
                string path = $"items[{index}]";
                if (!ValidateIdentifier(item.Id, "INVALID_ITEM_ID", $"{path}.id", errors))
                    continue;
                if (!itemsById.TryAdd(item.Id, item))
                {
                    errors.Add(new("DUPLICATE_ITEM_ID", path, $"Item '{item.Id}' is duplicated."));
                }

                if (!string.IsNullOrWhiteSpace(item.ItemFamilyId))
                {
                    bool invalidFamily =
                        !IsCanonicalIdentifier(item.ItemFamilyId)
                        || item.Type != ItemType.Equipment
                        || item.Slot is null
                        || !EquipmentSlotPolicy.IsCanonical(item.Slot.Value)
                        || item.GenerationMode != ItemGenerationMode.Rolled
                        || !item.ItemLevelMin.HasValue
                        || !item.ItemLevelMax.HasValue;
                    if (invalidFamily)
                    {
                        errors.Add(new(
                            "INVALID_ITEM_FAMILY",
                            $"{path}.itemFamilyId",
                            $"Item '{item.Id}' has an invalid item-family configuration."));
                    }
                    else if (!itemFamilyIds.Add(item.ItemFamilyId))
                    {
                        errors.Add(new(
                            "DUPLICATE_ITEM_FAMILY_ID",
                            $"{path}.itemFamilyId",
                            $"Item family '{item.ItemFamilyId}' is duplicated."));
                    }
                }

                if (item.HonorPrice < 0
                    || item.HonorPrice > 0 && item.Type != ItemType.Equipment)
                {
                    errors.Add(new(
                        "INVALID_ITEM_HONOR_PRICE",
                        $"{path}.honorPrice",
                        $"Item '{item.Id}' has an invalid Honor price."));
                }

                if (item.AllowedClassIds is not null)
                {
                    bool invalidClassRestriction =
                        item.Type != ItemType.Equipment
                        || item.AllowedClassIds.Count == 0
                        || item.AllowedClassIds.Distinct(StringComparer.Ordinal).Count()
                            != item.AllowedClassIds.Count
                        || item.AllowedClassIds.Any(classId =>
                            !IsCanonicalIdentifier(classId)
                            || !classProfileIds.Contains(classId));
                    if (invalidClassRestriction)
                    {
                        errors.Add(new(
                            "INVALID_ITEM_CLASS_RESTRICTION",
                            $"{path}.allowedClassIds",
                            $"Item '{item.Id}' has an invalid class restriction."));
                    }
                }

                bool invalidEquipmentCategory = item.Type == ItemType.Equipment
                    ? !HasValidEquipmentCategoryShape(item)
                    : item.WeaponCategory is not null
                        || item.ArmorCategory is not null
                        || item.OffHandCategory is not null;
                if (invalidEquipmentCategory)
                {
                    errors.Add(new("INVALID_ITEM_EQUIPMENT_CATEGORY", path,
                        $"Item '{item.Id}' contains an invalid equipment category shape."));
                }

                ValidateItemGenerationShape(item, path, errors);

                if (!string.IsNullOrWhiteSpace(item.SetId)
                    && !equipmentSetIds.Contains(item.SetId))
                {
                    errors.Add(new("MISSING_ITEM_SET_REFERENCE", path,
                        $"Item '{item.Id}' references missing equipment set '{item.SetId}'."));
                }

                bool negativeStats = item.Stats.Strength < 0
                    || item.Stats.Agility < 0
                    || item.Stats.Intellect < 0
                    || item.Stats.Stamina < 0;
                bool invalidPrimaryStatRanges = HasInvalidPrimaryStatRanges(item.PrimaryStatRanges);
                bool invalidWeaponDamageRange = HasInvalidWeaponDamageRange(item);
                bool invalidBlockProfile = HasInvalidBlockProfile(item);
                bool hasConsumableData =
                    item.ConsumableCooldownSeconds != 0
                    || item.ConsumableActions is { Count: > 0 }
                    || !string.IsNullOrWhiteSpace(item.ConsumableCooldownCategoryId);
                bool hasLootContainerData =
                    !string.IsNullOrWhiteSpace(item.LootContainerTableId)
                    || item.LootContainerGoldMin != 0
                    || item.LootContainerGoldMax != 0;
                bool invalidConsumableShape =
                    item.Type == ItemType.Consumable
                    && HasInvalidConsumableShape(
                        item,
                        resourceProfileIds,
                        effectIds);
                bool invalidTypeShape = item.Type switch
                {
                    ItemType.Material => !item.Stackable || item.MaxStack < 2 || item.Slot is not null
                        || HasEquipmentModifiers(item)
                        || hasConsumableData
                        || hasLootContainerData,
                    ItemType.Equipment => item.Stackable || item.MaxStack != 1 || item.Slot is null
                        || hasConsumableData
                        || hasLootContainerData
                        || item.WeaponBaseAttackIntervalSeconds is <= 0,
                    ItemType.Consumable => !item.Stackable || item.MaxStack < 2 || item.Slot is not null
                        || HasEquipmentModifiers(item)
                        || hasLootContainerData
                        || invalidConsumableShape,
                    ItemType.LootContainer => !item.Stackable || item.MaxStack < 2 || item.Slot is not null
                        || HasEquipmentModifiers(item)
                        || hasConsumableData
                        || string.IsNullOrWhiteSpace(item.LootContainerTableId)
                        || item.LootContainerGoldMin < 0
                        || item.LootContainerGoldMax < item.LootContainerGoldMin,
                    _ => true
                };
                if (string.IsNullOrWhiteSpace(item.Name)
                    || string.IsNullOrWhiteSpace(item.Description)
                    || item.RequiredLevel < 1
                    || item.Version < 1
                    || item.MaxStack < 1
                    || negativeStats
                    || invalidPrimaryStatRanges
                    || invalidWeaponDamageRange
                    || invalidBlockProfile
                    || invalidTypeShape)
                {
                    errors.Add(new("INVALID_ITEM_DEFINITION", path,
                        $"Item '{item.Id}' contains values outside its valid range."));
                }
            }

            IReadOnlyList<LootTableDefinition> lootTables = package.LootTables ?? [];
            HashSet<string> lootTableIds = new(StringComparer.Ordinal);
            for (var tableIndex = 0; tableIndex < lootTables.Count; tableIndex++)
            {
                LootTableDefinition table = lootTables[tableIndex];
                string path = $"lootTables[{tableIndex}]";
                if (!ValidateIdentifier(table.Id, "INVALID_LOOT_TABLE_ID", $"{path}.id", errors))
                    continue;
                if (!lootTableIds.Add(table.Id))
                    errors.Add(new("DUPLICATE_LOOT_TABLE_ID", path, $"Loot table '{table.Id}' is duplicated."));
                IReadOnlyList<LootSelectionGroup> selectionGroups = table.SelectionGroups ?? [];
                if (table.Version < 1 || table.Entries.Count == 0 && selectionGroups.Count == 0)
                    errors.Add(new("INVALID_LOOT_TABLE", path, $"Loot table '{table.Id}' is invalid."));

                HashSet<string> entryItemIds = new(StringComparer.Ordinal);
                for (var entryIndex = 0; entryIndex < table.Entries.Count; entryIndex++)
                {
                    LootTableEntry entry = table.Entries[entryIndex];
                    string entryPath = $"{path}.entries[{entryIndex}]";
                    if (!itemsById.TryGetValue(entry.ItemId, out ItemDefinition? item))
                    {
                        errors.Add(new("MISSING_LOOT_ITEM_REFERENCE", entryPath,
                            $"Loot entry references missing item '{entry.ItemId}'."));
                        continue;
                    }

                    if (!entryItemIds.Add(entry.ItemId)
                        || entry.DropChance is <= 0 or > 1
                        || entry.MinQuantity < 1
                        || entry.MaxQuantity < entry.MinQuantity
                        || !item.Stackable && entry.MaxQuantity != 1
                        || HasInvalidLootItemLevelOverride(
                            item,
                            entry.ItemLevelMin,
                            entry.ItemLevelMax))
                    {
                        errors.Add(new("INVALID_LOOT_ENTRY", entryPath,
                            $"Loot entry for '{entry.ItemId}' is invalid."));
                    }
                }

                HashSet<string> groupIds = new(StringComparer.Ordinal);
                for (var groupIndex = 0; groupIndex < selectionGroups.Count; groupIndex++)
                {
                    LootSelectionGroup group = selectionGroups[groupIndex];
                    string groupPath = $"{path}.selectionGroups[{groupIndex}]";
                    bool isWeighted = string.Equals(group.SelectionMode, "WeightedExclusive", StringComparison.Ordinal);
                    bool isEqual = string.Equals(group.SelectionMode, "EqualWeight", StringComparison.Ordinal);
                    if (!ValidateIdentifier(group.Id, "INVALID_LOOT_SELECTION_GROUP_ID", $"{groupPath}.id", errors)
                        || !groupIds.Add(group.Id)
                        || group.Rolls < 1
                        || group.Entries.Count == 0
                        || !isWeighted && !isEqual)
                    {
                        errors.Add(new("INVALID_LOOT_SELECTION_GROUP", groupPath,
                            $"Loot selection group '{group.Id}' is invalid."));
                    }

                    HashSet<string> groupItemIds = new(StringComparer.Ordinal);
                    for (var selectionIndex = 0; selectionIndex < group.Entries.Count; selectionIndex++)
                    {
                        LootSelectionEntry entry = group.Entries[selectionIndex];
                        string entryPath = $"{groupPath}.entries[{selectionIndex}]";
                        if (!itemsById.TryGetValue(entry.ItemId, out ItemDefinition? item))
                        {
                            errors.Add(new("MISSING_LOOT_ITEM_REFERENCE", entryPath,
                                $"Loot selection references missing item '{entry.ItemId}'."));
                            continue;
                        }

                        if (!groupItemIds.Add(entry.ItemId)
                            || entry.Weight <= 0
                            || entry.MinQuantity < 1
                            || entry.MaxQuantity < entry.MinQuantity
                            || !item.Stackable && entry.MaxQuantity != 1
                            || HasInvalidLootItemLevelOverride(
                                item,
                                entry.ItemLevelMin,
                                entry.ItemLevelMax))
                        {
                            errors.Add(new("INVALID_LOOT_SELECTION_ENTRY", entryPath,
                                $"Loot selection entry for '{entry.ItemId}' is invalid."));
                        }
                    }
                }
            }

            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                ItemDefinition item = items[itemIndex];
                if (item.Type != ItemType.LootContainer)
                    continue;

                if (string.IsNullOrWhiteSpace(item.LootContainerTableId)
                    || !lootTableIds.Contains(item.LootContainerTableId))
                {
                    errors.Add(new(
                        "MISSING_LOOT_CONTAINER_TABLE_REFERENCE",
                        $"items[{itemIndex}].lootContainerTableId",
                        $"Loot container '{item.Id}' references missing loot table '{item.LootContainerTableId}'."));
                }
            }

            for (var monsterIndex = 0; monsterIndex < (package.Monsters?.Count ?? 0); monsterIndex++)
            {
                MonsterDefinition monster = package.Monsters![monsterIndex];
                string path = $"monsters[{monsterIndex}]";
                if (!string.IsNullOrWhiteSpace(monster.LootTableId)
                    && !lootTableIds.Contains(monster.LootTableId))
                {
                    errors.Add(new("MISSING_MONSTER_LOOT_TABLE", path,
                        $"Monster '{monster.Id}' references missing loot table '{monster.LootTableId}'."));
                }
            }
        }

        private static bool HasInvalidLootItemLevelOverride(
            ItemDefinition item,
            int? itemLevelMin,
            int? itemLevelMax)
        {
            if (!itemLevelMin.HasValue && !itemLevelMax.HasValue)
                return false;
            if (item.Type != ItemType.Equipment
                || string.IsNullOrWhiteSpace(item.ItemFamilyId)
                || item.GenerationMode != ItemGenerationMode.Rolled)
            {
                return true;
            }

            int templateMinimum = item.ItemLevelMin ?? item.RequiredLevel;
            int templateMaximum = item.ItemLevelMax ?? templateMinimum;
            int minimum = itemLevelMin ?? itemLevelMax ?? templateMinimum;
            int maximum = itemLevelMax ?? itemLevelMin ?? templateMaximum;
            return minimum < templateMinimum
                || maximum > templateMaximum
                || maximum < minimum;
        }

        private static bool HasInvalidConsumableShape(
            ItemDefinition item,
            HashSet<string> resourceProfileIds,
            HashSet<string> effectIds)
        {
            if (item.ConsumableCooldownSeconds <= 0
                || string.IsNullOrWhiteSpace(item.ConsumableCooldownCategoryId)
                || !IsCanonicalIdentifier(item.ConsumableCooldownCategoryId)
                || item.ConsumableActions is not { Count: > 0 })
            {
                return true;
            }

            foreach (ConsumableActionDefinition action in item.ConsumableActions)
            {
                bool hasResource = !string.IsNullOrWhiteSpace(action.ResourceType);
                bool hasEffect = !string.IsNullOrWhiteSpace(action.EffectId);
                bool hasDispel = !string.IsNullOrWhiteSpace(action.DispelCategory);
                switch (action.Type)
                {
                    case ConsumableActionType.RestoreHp:
                        if (action.Amount <= 0 || hasResource || hasEffect || hasDispel)
                            return true;
                        break;
                    case ConsumableActionType.RestoreResource:
                        if (action.Amount <= 0
                            || !hasResource
                            || !resourceProfileIds.Contains(action.ResourceType!)
                            || hasEffect
                            || hasDispel)
                        {
                            return true;
                        }
                        break;
                    case ConsumableActionType.ApplyEffect:
                        if (action.Amount != 0
                            || hasResource
                            || !hasEffect
                            || !effectIds.Contains(action.EffectId!)
                            || hasDispel)
                        {
                            return true;
                        }
                        break;
                    case ConsumableActionType.RemoveEffect:
                        if (action.Amount != 0
                            || hasResource
                            || hasEffect == hasDispel
                            || hasEffect && !effectIds.Contains(action.EffectId!))
                        {
                            return true;
                        }
                        break;
                    default:
                        return true;
                }
            }

            return false;
        }

        private static bool HasValidEquipmentCategoryShape(ItemDefinition item) =>
            item.Slot switch
            {
                EquipmentSlot.MainHand =>
                    EquipmentCategoryIds.IsWeapon(item.WeaponCategory)
                    && item.ArmorCategory is null
                    && item.OffHandCategory is null,
                EquipmentSlot.OffHand =>
                    item.ArmorCategory is null
                    && (EquipmentCategoryIds.IsOneHandedWeapon(item.WeaponCategory)
                        ^ EquipmentCategoryIds.IsOffHand(item.OffHandCategory)),
                EquipmentSlot.Head or EquipmentSlot.Shoulders or EquipmentSlot.Chest or EquipmentSlot.Hands
                    or EquipmentSlot.Legs or EquipmentSlot.Feet =>
                    EquipmentCategoryIds.IsArmor(item.ArmorCategory)
                    && item.WeaponCategory is null
                    && item.OffHandCategory is null,
                EquipmentSlot.Cloak or EquipmentSlot.Amulet
                    or EquipmentSlot.Ring1 or EquipmentSlot.Ring2 =>
                    item.WeaponCategory is null
                    && item.ArmorCategory is null
                    && item.OffHandCategory is null,
                _ => false
            };

        private static void ValidateItemGenerationShape(
            ItemDefinition item,
            string path,
            List<ContentValidationError> errors)
        {
            bool hasLegacyRanges = ItemGenerationSemantics.HasLegacyRangeConfiguration(item);
            bool hasV2Configuration = ItemGenerationSemantics.HasV2Configuration(item);

            if (item.GenerationMode == ItemGenerationMode.Fixed)
            {
                if (hasLegacyRanges || hasV2Configuration)
                {
                    errors.Add(new(
                        "ITEM_FIXED_GENERATION_CONFLICT",
                        $"{path}.generationMode",
                        $"Fixed item '{item.Id}' cannot declare rolled-stat generation configuration."));
                }

                return;
            }

            if (item.Type != ItemType.Equipment)
            {
                errors.Add(new(
                    "ITEM_GENERATION_MODE_INVALID_TYPE",
                    $"{path}.generationMode",
                    $"Only equipment can use rolled item generation."));
                return;
            }

            if (hasLegacyRanges && hasV2Configuration)
            {
                errors.Add(new(
                    "ITEM_GENERATION_POLICY_AMBIGUOUS",
                    $"{path}.generationMode",
                    $"Rolled item '{item.Id}' cannot mix legacy stat ranges with V2 affix generation."));
                return;
            }

            if (!hasLegacyRanges && !ItemGenerationSemantics.HasCompleteV2Configuration(item))
            {
                errors.Add(new(
                    "ITEM_ROLLED_GENERATION_REQUIRED",
                    $"{path}.generationMode",
                    $"Rolled item '{item.Id}' requires a complete generation configuration."));
            }
        }

        private static bool HasEquipmentModifiers(ItemDefinition item) =>
            item.Stats != new PrimaryStats(0, 0, 0, 0)
            || item.SetId is not null
            || item.WeaponBaseAttackIntervalSeconds is not null
            || item.AttackSpeedPercent != 0
            || item.DodgePercent != 0
            || item.MaxHpFlat != 0
            || item.AttackPowerFlat != 0
            || item.SpellPowerFlat != 0
            || item.CriticalChancePercent != 0
            || item.CriticalDamagePercent != 0
            || item.AccuracyPercent != 0
            || item.ArmorFlat != 0
            || item.MagicResistanceFlat != 0
            || item.ArmorPenetrationPercent != 0
            || item.MagicPenetrationPercent != 0
            || item.MaxResourceFlat != 0
            || item.BlockChancePercent != 0
            || item.BlockValueMin != 0
            || item.BlockValueMax != 0
            || item.PrimaryStatRanges is not null
            || item.WeaponDamageMin is not null
            || item.WeaponDamageMax is not null;

        private static bool HasInvalidWeaponDamageRange(ItemDefinition item)
        {
            bool hasMinimum = item.WeaponDamageMin.HasValue;
            bool hasMaximum = item.WeaponDamageMax.HasValue;
            if (!hasMinimum && !hasMaximum) return false;
            if (!hasMinimum || !hasMaximum) return true;

            return item.Type != ItemType.Equipment
                || item.Slot != EquipmentSlot.MainHand
                || !EquipmentCategoryIds.IsWeapon(item.WeaponCategory)
                || item.WeaponDamageMin < 0
                || item.WeaponDamageMax < item.WeaponDamageMin;
        }

        private static bool HasInvalidBlockProfile(ItemDefinition item)
        {
            bool isShield = item.Type == ItemType.Equipment
                && item.Slot == EquipmentSlot.OffHand
                && string.Equals(
                    item.OffHandCategory,
                    EquipmentCategoryIds.Shield,
                    StringComparison.Ordinal);
            bool hasBlockData = item.BlockChancePercent != 0
                || item.BlockValueMin != 0
                || item.BlockValueMax != 0;

            if (!isShield)
                return hasBlockData;

            return item.BlockChancePercent is <= 0 or > 100
                || item.BlockValueMin < 0
                || item.BlockValueMax <= 0
                || item.BlockValueMax < item.BlockValueMin;
        }

        private static bool HasInvalidPrimaryStatRanges(PrimaryStatRanges? ranges)
        {
            if (ranges is null) return false;
            ItemStatRange?[] values =
            [
                ranges.Strength,
                ranges.Agility,
                ranges.Intellect,
                ranges.Stamina
            ];
            return values
                .Where(range => range is not null)
                .Any(range => range!.Min < 0 || range.Max < range.Min || range.Step <= 0);
        }

}
