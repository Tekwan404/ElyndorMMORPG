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
                        || allowedClassIds.Count > 0;
                if (invalidEquipmentCategory)
                {
                    errors.Add(new("INVALID_ITEM_EQUIPMENT_CATEGORY", path,
                        $"Item '{item.Id}' contains an invalid equipment category shape."));
                }

                bool negativeStats = item.Stats.Strength < 0
                    || item.Stats.Agility < 0
                    || item.Stats.Intellect < 0
                    || item.Stats.Stamina < 0;
                bool invalidTypeShape = item.Type switch
                {
                    ItemType.Material => !item.Stackable || item.MaxStack < 2 || item.Slot is not null
                        || item.Stats != new PrimaryStats(0, 0, 0, 0),
                    ItemType.Equipment => item.Stackable || item.MaxStack != 1 || item.Slot is null,
                    ItemType.Consumable => !item.Stackable || item.MaxStack < 2 || item.Slot is not null
                        || item.Stats != new PrimaryStats(0, 0, 0, 0)
                        || item.HealAmount <= 0 || item.ConsumableCooldownSeconds <= 0,
                    _ => true
                };
                if (string.IsNullOrWhiteSpace(item.Name)
                    || string.IsNullOrWhiteSpace(item.Description)
                    || item.RequiredLevel < 1
                    || item.Version < 1
                    || item.MaxStack < 1
                    || negativeStats
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
                EquipmentSlot.Weapon =>
                    EquipmentCategoryIds.IsWeapon(item.WeaponCategory)
                    && item.ArmorCategory is null,
                EquipmentSlot.Head or EquipmentSlot.Chest or EquipmentSlot.Legs or EquipmentSlot.Boots =>
                    EquipmentCategoryIds.IsArmor(item.ArmorCategory)
                    && item.WeaponCategory is null,
                EquipmentSlot.Accessory =>
                    item.WeaponCategory is null && item.ArmorCategory is null,
                _ => false
            };

}
