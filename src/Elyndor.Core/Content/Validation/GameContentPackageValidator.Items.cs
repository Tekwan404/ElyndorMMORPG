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
                || progression.BaseXpToNext <= 0
                || progression.GrowthFactor < 1)
            {
                errors.Add(new("INVALID_LEVEL_PROGRESSION", "levelProgression",
                    "Level progression contains values outside its valid range."));
            }

            IReadOnlyList<ItemDefinition> items = package.Items ?? [];
            HashSet<string> equipmentSetIds = (package.EquipmentSets ?? [])
                .Select(set => set.Id)
                .ToHashSet(StringComparer.Ordinal);
            Dictionary<string, ItemDefinition> itemsById = new(StringComparer.Ordinal);
            HashSet<string> itemClassIds = (package.ClassProfiles ?? [])
                .Select(profile => profile.Id)
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

                IReadOnlyList<string> allowedClassIds = item.AllowedClassIds ?? [];
                if (allowedClassIds.Count != allowedClassIds.Distinct(StringComparer.Ordinal).Count()
                    || allowedClassIds.Any(classId => !itemClassIds.Contains(classId)))
                {
                    errors.Add(new("INVALID_ITEM_CLASS_RESTRICTION", path,
                        $"Item '{item.Id}' contains an invalid or unknown class restriction."));
                }

                bool invalidEquipmentCategory = item.Type == ItemType.Equipment
                    ? !HasValidEquipmentCategoryShape(item)
                    : item.WeaponCategory is not null
                        || item.ArmorCategory is not null
                        || item.OffHandCategory is not null
                        || allowedClassIds.Count > 0;
                if (invalidEquipmentCategory)
                {
                    errors.Add(new("INVALID_ITEM_EQUIPMENT_CATEGORY", path,
                        $"Item '{item.Id}' contains an invalid equipment category shape."));
                }

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
                bool invalidTypeShape = item.Type switch
                {
                    ItemType.Material => !item.Stackable || item.MaxStack < 2 || item.Slot is not null
                        || HasEquipmentModifiers(item),
                    ItemType.Equipment => item.Stackable || item.MaxStack != 1 || item.Slot is null
                        || item.HealAmount != 0 || item.ConsumableCooldownSeconds != 0
                        || item.WeaponBaseAttackIntervalSeconds is <= 0,
                    ItemType.Consumable => !item.Stackable || item.MaxStack < 2 || item.Slot is not null
                        || HasEquipmentModifiers(item)
                        || item.HealAmount <= 0 || item.ConsumableCooldownSeconds <= 0,
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
                if (table.Version < 1 || table.Entries.Count == 0)
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
                        || !item.Stackable && entry.MaxQuantity != 1)
                    {
                        errors.Add(new("INVALID_LOOT_ENTRY", entryPath,
                            $"Loot entry for '{entry.ItemId}' is invalid."));
                    }
                }
            }

            for (var monsterIndex = 0; monsterIndex < (package.Monsters?.Count ?? 0); monsterIndex++)
            {
                MonsterDefinition monster = package.Monsters![monsterIndex];
                string path = $"monsters[{monsterIndex}]";
                if (monster.XpReward < 0)
                    errors.Add(new("INVALID_MONSTER_XP_REWARD", path,
                        $"Monster '{monster.Id}' has a negative XP reward."));
                if (!string.IsNullOrWhiteSpace(monster.LootTableId)
                    && !lootTableIds.Contains(monster.LootTableId))
                {
                    errors.Add(new("MISSING_MONSTER_LOOT_TABLE", path,
                        $"Monster '{monster.Id}' references missing loot table '{monster.LootTableId}'."));
                }
            }
        }

        private static bool HasValidEquipmentCategoryShape(ItemDefinition item) =>
            item.Slot switch
            {
                EquipmentSlot.Weapon or EquipmentSlot.MainHand =>
                    EquipmentCategoryIds.IsWeapon(item.WeaponCategory)
                    && item.ArmorCategory is null
                    && item.OffHandCategory is null,
                EquipmentSlot.OffHand =>
                    item.ArmorCategory is null
                    && (EquipmentCategoryIds.IsWeapon(item.WeaponCategory)
                        ^ EquipmentCategoryIds.IsOffHand(item.OffHandCategory)),
                EquipmentSlot.Head or EquipmentSlot.Chest or EquipmentSlot.Hands
                    or EquipmentSlot.Legs or EquipmentSlot.Boots or EquipmentSlot.Feet =>
                    EquipmentCategoryIds.IsArmor(item.ArmorCategory)
                    && item.WeaponCategory is null
                    && item.OffHandCategory is null,
                EquipmentSlot.Accessory or EquipmentSlot.Cloak or EquipmentSlot.Amulet
                    or EquipmentSlot.Ring1 or EquipmentSlot.Ring2 =>
                    item.WeaponCategory is null
                    && item.ArmorCategory is null
                    && item.OffHandCategory is null,
                _ => false
            };

        private static bool HasEquipmentModifiers(ItemDefinition item) =>
            item.Stats != new PrimaryStats(0, 0, 0, 0)
            || item.SetId is not null
            || item.WeaponBaseAttackIntervalSeconds is not null
            || item.AttackSpeedPercent != 0
            || item.DodgePercent != 0
            || item.AllowedClassIds is { Count: > 0 }
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

            EquipmentSlot? canonicalSlot = item.Slot switch
            {
                EquipmentSlot.Weapon => EquipmentSlot.MainHand,
                _ => item.Slot
            };
            return item.Type != ItemType.Equipment
                || canonicalSlot != EquipmentSlot.MainHand
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
