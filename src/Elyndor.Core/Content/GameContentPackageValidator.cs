using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static class GameContentPackageValidator
{
    private static readonly HashSet<string> AllowedDangerLevels =
        ["SAFE", "ADVENTURE", "DANGEROUS"];

    public static IReadOnlyList<ContentValidationError> Validate(GameContentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        List<ContentValidationError> errors = [];

        ValidateMetadata(package, errors);

        HashSet<ContentKey> definitions = [];

        for (var index = 0; index < package.Definitions.Count; index++)
        {
            GameContentDefinition definition = package.Definitions[index];
            string path = $"definitions[{index}]";

            bool typeIsValid = ValidateIdentifier(
                definition.Type,
                "INVALID_DEFINITION_TYPE",
                $"{path}.type",
                errors);
            bool idIsValid = ValidateIdentifier(
                definition.Id,
                "INVALID_DEFINITION_ID",
                $"{path}.id",
                errors);

            if (typeIsValid && idIsValid && !definitions.Add(new ContentKey(definition.Type, definition.Id)))
            {
                errors.Add(new ContentValidationError(
                    "DUPLICATE_DEFINITION_ID",
                    path,
                    $"Definition '{definition.Type}:{definition.Id}' is duplicated."));
            }
        }

        ValidateReferences(package, definitions, errors);
        ValidateLocations(package.Locations, errors);
        ValidateCharacterProfiles(package, definitions, errors);
        ValidateCombatDefinitions(package, errors);
        ValidateMonsterDefinitions(package, errors);
        ValidateTalentDefinitions(package.TalentTrees ?? [], package.Abilities ?? [], errors);
        ValidateProgressionItemsAndLoot(package, errors);

        return errors;
    }

    private static void ValidateProgressionItemsAndLoot(
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

    private static void ValidateTalentDefinitions(
        IReadOnlyList<TalentTreeDefinition> trees,
        IReadOnlyList<AbilityDefinition> abilities,
        List<ContentValidationError> errors)
    {
        HashSet<string> treeIds = [];
        for (var treeIndex = 0; treeIndex < trees.Count; treeIndex++)
        {
            TalentTreeDefinition tree = trees[treeIndex];
            string path = $"talentTrees[{treeIndex}]";
            if (!treeIds.Add(tree.Id))
            {
                errors.Add(new("DUPLICATE_TALENT_TREE_ID", path, $"Talent tree '{tree.Id}' is duplicated."));
            }

            if (string.IsNullOrWhiteSpace(tree.Id) || string.IsNullOrWhiteSpace(tree.ClassId)
                || tree.Version <= 0 || tree.MaxSpendablePoints <= 0)
            {
                errors.Add(new("INVALID_TALENT_TREE", path, $"Talent tree '{tree.Id}' is invalid."));
            }

            HashSet<string> branchIds = [];
            foreach (TalentBranchDefinition branch in tree.Branches)
            {
                if (!branchIds.Add(branch.Id) || branch.NodeCount <= 0)
                {
                    errors.Add(new("INVALID_TALENT_BRANCH", path, $"Talent branch '{branch.Id}' is invalid or duplicated."));
                }
            }

            Dictionary<string, TalentDefinition> nodes = [];
            HashSet<string> abilityIds = abilities.Select(ability => ability.Id).ToHashSet(StringComparer.Ordinal);
            foreach (TalentDefinition node in tree.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.Id) || !nodes.TryAdd(node.Id, node))
                {
                    errors.Add(new("DUPLICATE_TALENT_ID", path, $"Talent node '{node.Id}' is invalid or duplicated."));
                }

                if (!branchIds.Contains(node.BranchId) || node.Tier is < 1 or > 9
                    || node.RequiredSpentPoints < 0 || node.MaxRank <= 0 || node.Version <= 0
                    || node.RequiredLevel is < 1)
                {
                    errors.Add(new("INVALID_TALENT_DEFINITION", path, $"Talent node '{node.Id}' is invalid."));
                }

                if (node.Modifiers is null || node.Modifiers.Count == 0)
                {
                    errors.Add(new("MISSING_TALENT_MODIFIER", path,
                        $"Talent node '{node.Id}' must define a supported modifier or an explicit deferred hook."));
                }
                else if (node.Modifiers.Any(modifier => string.IsNullOrWhiteSpace(modifier.Key)
                    || !TalentModifierKeys.All.Contains(modifier.Key)
                    || modifier.Values.Count == 0 || modifier.Values.Any(value => value < 0)
                    || modifier.Values.Count != node.MaxRank
                    || modifier.InternalCooldownSeconds < 0
                    || modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
                        && (string.IsNullOrWhiteSpace(modifier.DeferredOwner)
                            || !TalentRuntimeOwners.All.Contains(modifier.DeferredOwner))))
                {
                    errors.Add(new("INVALID_TALENT_MODIFIER", path, $"Talent node '{node.Id}' contains an invalid modifier."));
                }

                if (node.Modifiers?.Any(modifier =>
                    modifier.Type == TalentModifierType.AbilityModifier
                    && modifier.Key == TalentModifierKeys.UnlockAbility
                    && modifier.RuntimeStatus == TalentModifierRuntimeStatus.Supported
                    && (string.IsNullOrWhiteSpace(modifier.TargetId)
                        || !abilityIds.Contains(modifier.TargetId))) == true)
                {
                    errors.Add(new("MISSING_TALENT_ABILITY_REFERENCE", path,
                        $"Talent node '{node.Id}' references an ability that does not exist."));
                }

                if (node.IconId is not null && !IsCanonicalIdentifier(node.IconId))
                {
                    errors.Add(new("INVALID_TALENT_ICON_ID", path,
                        $"Talent node '{node.Id}' has a non-canonical icon id."));
                }
            }

            foreach (TalentDefinition node in tree.Nodes)
            {
                foreach (TalentPrerequisite prerequisite in node.Prerequisites)
                {
                    if (!nodes.TryGetValue(prerequisite.TalentId, out TalentDefinition? required)
                        || prerequisite.RequiredRank <= 0
                        || prerequisite.RequiredRank > required.MaxRank)
                    {
                        errors.Add(new("INVALID_TALENT_PREREQUISITE", path,
                            $"Talent node '{node.Id}' has invalid prerequisite '{prerequisite.TalentId}'."));
                    }
                }
            }

            foreach (TalentBranchDefinition branch in tree.Branches)
            {
                if (tree.Nodes.Count(node => node.BranchId == branch.Id) != branch.NodeCount)
                {
                    errors.Add(new("TALENT_BRANCH_NODE_COUNT_MISMATCH", path,
                        $"Talent branch '{branch.Id}' node count does not match its content."));
                }
            }

            if (string.Equals(tree.Id, "WARRIOR_TREE", StringComparison.Ordinal)
                && (tree.Nodes.Count != 96 || tree.Branches.Count != 3))
            {
                errors.Add(new("INVALID_WARRIOR_TREE_SIZE", path,
                    "Warrior tree must contain exactly 96 nodes across 3 branches."));
            }

            if (HasTalentCycle(nodes))
            {
                errors.Add(new("CIRCULAR_TALENT_PREREQUISITE", path,
                    $"Talent tree '{tree.Id}' contains a prerequisite cycle."));
            }
        }
    }

    private static bool HasTalentCycle(IReadOnlyDictionary<string, TalentDefinition> nodes)
    {
        HashSet<string> visiting = [];
        HashSet<string> visited = [];

        bool Visit(string id)
        {
            if (visited.Contains(id)) return false;
            if (!visiting.Add(id)) return true;
            if (nodes.TryGetValue(id, out TalentDefinition? node))
            {
                foreach (TalentPrerequisite prerequisite in node.Prerequisites)
                {
                    if (nodes.ContainsKey(prerequisite.TalentId) && Visit(prerequisite.TalentId))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(id);
            visited.Add(id);
            return false;
        }

        return nodes.Keys.Any(Visit);
    }

    private static void ValidateCombatDefinitions(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        HashSet<string> effectIds = [];
        for (var index = 0; index < (package.Effects?.Count ?? 0); index++)
        {
            EffectDefinition effect = package.Effects![index];
            string path = $"effects[{index}]";
            bool idIsValid = ValidateIdentifier(
                effect.Id, "INVALID_EFFECT_ID", $"{path}.id", errors);
            if (idIsValid && !effectIds.Add(effect.Id))
            {
                errors.Add(new ContentValidationError(
                    "DUPLICATE_EFFECT_ID", path, $"Effect '{effect.Id}' is duplicated."));
            }

            bool periodic = effect.Kind is EffectKind.DamageOverTime or EffectKind.HealingOverTime;
            if (effect.Duration <= TimeSpan.Zero
                || effect.MaxStacks <= 0
                || effect.Magnitude < 0
                || effect.Version <= 0
                || periodic != effect.TickInterval.HasValue
                || effect.TickInterval <= TimeSpan.Zero)
            {
                errors.Add(new ContentValidationError(
                    "INVALID_EFFECT_DEFINITION", path,
                    $"Effect '{effect.Id}' contains values outside its valid range."));
            }
        }

        HashSet<string> abilityIds = [];
        for (var index = 0; index < (package.Abilities?.Count ?? 0); index++)
        {
            AbilityDefinition ability = package.Abilities![index];
            string path = $"abilities[{index}]";
            bool idIsValid = ValidateIdentifier(
                ability.Id, "INVALID_ABILITY_ID", $"{path}.id", errors);
            if (idIsValid && !abilityIds.Add(ability.Id))
            {
                errors.Add(new ContentValidationError(
                    "DUPLICATE_ABILITY_ID", path, $"Ability '{ability.Id}' is duplicated."));
            }

            if (ability.ResourceCost < 0
                || ability.Cooldown < TimeSpan.Zero
                || ability.CastTime < TimeSpan.Zero
                || ability.Type == AbilityType.Casted && ability.CastTime <= TimeSpan.Zero
                || ability.Type != AbilityType.Casted && ability.CastTime != TimeSpan.Zero
                || ability.UsesGlobalCooldown && ability.GlobalCooldownCategory == GlobalCooldownCategory.None
                || ability.Actions?.Any(action => action.Amount < 0
                    || action.AttackPowerCoefficient < 0
                    || action.ArmorPenetrationBonus < 0
                    || action.Type == AbilityActionType.ApplyEffect && action.Effect is null
                    || action.Type != AbilityActionType.ApplyEffect && action.Effect is not null
                    || action.Type == AbilityActionType.Taunt && action.Duration <= TimeSpan.Zero) == true
                || string.IsNullOrWhiteSpace(ability.School))
            {
                errors.Add(new ContentValidationError(
                    "INVALID_ABILITY_DEFINITION", path,
                    $"Ability '{ability.Id}' contains values outside its valid range."));
            }
        }
    }

    private static void ValidateMonsterDefinitions(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        HashSet<string> abilityIds = (package.Abilities ?? [])
            .Select(ability => ability.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> aiProfileIds = [];
        for (var index = 0; index < (package.MonsterAiProfiles?.Count ?? 0); index++)
        {
            MonsterAiProfile profile = package.MonsterAiProfiles![index];
            string path = $"monsterAiProfiles[{index}]";
            bool idIsValid = ValidateIdentifier(
                profile.Id, "INVALID_MONSTER_AI_PROFILE_ID", $"{path}.id", errors);
            if (idIsValid && !aiProfileIds.Add(profile.Id))
            {
                errors.Add(new("DUPLICATE_MONSTER_AI_PROFILE_ID", path,
                    $"Monster AI profile '{profile.Id}' is duplicated."));
            }

            if (profile.Version <= 0 || profile.PriorityAbilityIds.Any(id => !abilityIds.Contains(id)))
            {
                errors.Add(new("INVALID_MONSTER_AI_PROFILE", path,
                    $"Monster AI profile '{profile.Id}' is invalid."));
            }
        }

        HashSet<string> monsterIds = [];
        for (var index = 0; index < (package.Monsters?.Count ?? 0); index++)
        {
            MonsterDefinition monster = package.Monsters![index];
            string path = $"monsters[{index}]";
            bool idIsValid = ValidateIdentifier(
                monster.Id, "INVALID_MONSTER_ID", $"{path}.id", errors);
            if (idIsValid && !monsterIds.Add(monster.Id))
            {
                errors.Add(new("DUPLICATE_MONSTER_ID", path,
                    $"Monster '{monster.Id}' is duplicated."));
            }

            if (string.IsNullOrWhiteSpace(monster.Name)
                || monster.Level <= 0
                || monster.MaxHp <= 0
                || monster.AutoAttackInterval <= TimeSpan.Zero
                || monster.AutoAttackBaseDamage < 0
                || monster.AutoAttackAttackPowerCoefficient < 0
                || monster.Version <= 0)
            {
                errors.Add(new("INVALID_MONSTER_DEFINITION", path,
                    $"Monster '{monster.Id}' contains values outside its valid range."));
            }

            foreach (string abilityId in monster.AbilityIds)
            {
                if (!abilityIds.Contains(abilityId))
                {
                    errors.Add(new("MISSING_MONSTER_ABILITY", path,
                        $"Monster '{monster.Id}' references missing ability '{abilityId}'."));
                }
            }

            if (!aiProfileIds.Contains(monster.AiProfileId))
            {
                errors.Add(new("MISSING_MONSTER_AI_PROFILE", path,
                    $"Monster '{monster.Id}' references missing AI profile '{monster.AiProfileId}'."));
            }
        }
    }

    private static void ValidateCharacterProfiles(
        GameContentPackage package,
        IReadOnlySet<ContentKey> definitions,
        List<ContentValidationError> errors)
    {
        if (package.ClassProfiles is null
            || package.StatFormula is null
            || package.ResourceProfiles is null)
        {
            if (Version.TryParse(package.ContentVersion, out Version? version)
                && version >= new Version(0, 2, 0))
            {
                errors.Add(new ContentValidationError(
                    "MISSING_CHARACTER_PROFILES",
                    "classProfiles",
                    "Content version 0.2.0 and newer requires class, stat, and resource profiles."));
            }

            return;
        }

        HashSet<string> resourceIds = [];
        for (var index = 0; index < package.ResourceProfiles.Count; index++)
        {
            ResourceProfile profile = package.ResourceProfiles[index];
            string path = $"resourceProfiles[{index}]";
            if (!resourceIds.Add(profile.Id))
            {
                errors.Add(new ContentValidationError(
                    "DUPLICATE_RESOURCE_PROFILE",
                    path,
                    $"Resource profile '{profile.Id}' is duplicated."));
            }

            if (profile.MaxValue <= 0
                || profile.StartValue < 0
                || profile.StartValue > profile.MaxValue
                || profile.RespawnValue < 0
                || profile.RespawnValue > profile.MaxValue
                || profile.CombatRegenPerSecond < 0
                || profile.OutOfCombatRegenPerSecond < 0
                || profile.OutOfCombatDecayPerSecond < 0
                || profile.OutOfCombatDelaySeconds < 0)
            {
                errors.Add(new ContentValidationError(
                    "INVALID_RESOURCE_PROFILE",
                    path,
                    $"Resource profile '{profile.Id}' contains values outside its valid range."));
            }
        }

        HashSet<string> classIds = [];
        for (var index = 0; index < package.ClassProfiles.Count; index++)
        {
            ClassProfile profile = package.ClassProfiles[index];
            string path = $"classProfiles[{index}]";
            if (!classIds.Add(profile.Id))
            {
                errors.Add(new ContentValidationError(
                    "DUPLICATE_CLASS_PROFILE",
                    path,
                    $"Class profile '{profile.Id}' is duplicated."));
            }

            if (!definitions.Contains(new ContentKey("CLASS", profile.Id)))
            {
                errors.Add(new ContentValidationError(
                    "MISSING_CLASS_DEFINITION",
                    path,
                    $"Class definition '{profile.Id}' does not exist."));
            }

            if (!resourceIds.Contains(profile.ResourceProfileId))
            {
                errors.Add(new ContentValidationError(
                    "MISSING_RESOURCE_PROFILE",
                    path,
                    $"Resource profile '{profile.ResourceProfileId}' does not exist."));
            }

            if (profile.BaseStats.Strength < 0
                || profile.BaseStats.Agility < 0
                || profile.BaseStats.Intellect < 0
                || profile.BaseStats.Stamina < 0
                || profile.LevelGrowth.Strength < 0
                || profile.LevelGrowth.Agility < 0
                || profile.LevelGrowth.Intellect < 0
                || profile.LevelGrowth.Stamina < 0)
            {
                errors.Add(new ContentValidationError(
                    "INVALID_CLASS_STATS",
                    path,
                    $"Class profile '{profile.Id}' contains negative stats."));
            }

            bool invalidWeaponCategories = profile.AllowedWeaponCategories.Count == 0
                || profile.AllowedWeaponCategories.Distinct(StringComparer.Ordinal).Count()
                    != profile.AllowedWeaponCategories.Count
                || profile.AllowedWeaponCategories.Any(category =>
                    !EquipmentCategoryIds.IsWeapon(category));
            bool invalidArmorCategories = profile.AllowedArmorCategories.Count == 0
                || profile.AllowedArmorCategories.Distinct(StringComparer.Ordinal).Count()
                    != profile.AllowedArmorCategories.Count
                || profile.AllowedArmorCategories.Any(category =>
                    !EquipmentCategoryIds.IsArmor(category));
            if (invalidWeaponCategories || invalidArmorCategories)
            {
                errors.Add(new ContentValidationError(
                    "INVALID_CLASS_EQUIPMENT_CATEGORIES",
                    path,
                    $"Class profile '{profile.Id}' contains invalid equipment categories."));
            }

            if (profile.CombatAutoAttack is { } autoAttack
                && (autoAttack.Interval <= TimeSpan.Zero
                    || autoAttack.BaseDamage < 0
                    || autoAttack.AttackPowerCoefficient < 0
                    || autoAttack.ResourceOnHit < 0))
            {
                errors.Add(new ContentValidationError(
                    "INVALID_CLASS_AUTO_ATTACK",
                    path,
                    $"Class profile '{profile.Id}' contains an invalid combat auto attack."));
            }

            HashSet<string> ownedAbilityIds = [];
            foreach (string abilityId in profile.StartingAbilityIds ?? [])
            {
                if (!ownedAbilityIds.Add(abilityId)
                    || package.Abilities?.Any(ability => ability.Id == abilityId) != true)
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_CLASS_ABILITY", path,
                        $"Class profile '{profile.Id}' references invalid ability '{abilityId}'."));
                }
            }

            foreach (AbilityUnlockDefinition unlock in profile.AbilityUnlocks ?? [])
            {
                if (unlock.UnlockLevel < 2
                    || !ownedAbilityIds.Add(unlock.AbilityId)
                    || package.Abilities?.Any(ability => ability.Id == unlock.AbilityId) != true)
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_CLASS_ABILITY_UNLOCK", path,
                        $"Class profile '{profile.Id}' contains invalid unlock '{unlock.AbilityId}'."));
                }
            }
        }

        foreach (string requiredClassId in new[] { "WARRIOR", "ARCHER", "MAGE" })
        {
            if (!classIds.Contains(requiredClassId))
            {
                errors.Add(new ContentValidationError(
                    "MISSING_PROTOTYPE_CLASS_PROFILE",
                    "classProfiles",
                    $"Prototype class profile '{requiredClassId}' is required."));
            }
        }

        if (package.StatFormula.MaxHpBase <= 0
            || package.StatFormula.MaxHpPerStamina < 0
            || package.StatFormula.CriticalChanceBase is < 0 or > 100
            || package.StatFormula.CriticalDamageBase < 0
            || package.StatFormula.AccuracyBase < 0
            || package.StatFormula.AttackSpeedBase <= 0)
        {
            errors.Add(new ContentValidationError(
                "INVALID_STAT_FORMULA",
                "statFormula",
                "Stat formula contains values outside its valid range."));
        }
    }

    private static void ValidateLocations(
        IReadOnlyList<LocationDefinition> locations,
        List<ContentValidationError> errors)
    {
        HashSet<string> locationIds = [];

        for (var index = 0; index < locations.Count; index++)
        {
            LocationDefinition location = locations[index];
            string path = $"locations[{index}]";

            bool idIsValid = ValidateIdentifier(
                location.Id,
                "INVALID_LOCATION_ID",
                $"{path}.id",
                errors);

            if (idIsValid && !locationIds.Add(location.Id))
            {
                errors.Add(new ContentValidationError(
                    "DUPLICATE_LOCATION_ID",
                    $"{path}.id",
                    $"Location '{location.Id}' is duplicated."));
            }

            if (string.IsNullOrWhiteSpace(location.DisplayName))
            {
                errors.Add(new ContentValidationError(
                    "MISSING_LOCATION_DISPLAY_NAME",
                    $"{path}.displayName",
                    "Location display name is required."));
            }

            if (!AllowedDangerLevels.Contains(location.DangerLevel))
            {
                errors.Add(new ContentValidationError(
                    "INVALID_LOCATION_DANGER_LEVEL",
                    $"{path}.dangerLevel",
                    $"Location danger level '{location.DangerLevel}' is not supported."));
            }

            if (location.RecommendedLevel <= 0)
            {
                errors.Add(new ContentValidationError(
                    "INVALID_LOCATION_RECOMMENDED_LEVEL",
                    $"{path}.recommendedLevel",
                    "Location recommended level must be positive."));
            }
        }

        ValidateLocationTransitions(locations, locationIds, errors);
    }

    private static void ValidateLocationTransitions(
        IReadOnlyList<LocationDefinition> locations,
        HashSet<string> locationIds,
        List<ContentValidationError> errors)
    {
        for (var locationIndex = 0; locationIndex < locations.Count; locationIndex++)
        {
            LocationDefinition location = locations[locationIndex];
            HashSet<string> transitions = [];

            for (var transitionIndex = 0;
                 transitionIndex < location.Transitions.Count;
                 transitionIndex++)
            {
                string targetId = location.Transitions[transitionIndex];
                string path = $"locations[{locationIndex}].transitions[{transitionIndex}]";
                bool targetIsValid = ValidateIdentifier(
                    targetId,
                    "INVALID_LOCATION_TRANSITION_ID",
                    path,
                    errors);

                if (!targetIsValid)
                {
                    continue;
                }

                if (!transitions.Add(targetId))
                {
                    errors.Add(new ContentValidationError(
                        "DUPLICATE_LOCATION_TRANSITION",
                        path,
                        $"Transition to '{targetId}' is duplicated."));
                }

                if (string.Equals(location.Id, targetId, StringComparison.Ordinal))
                {
                    errors.Add(new ContentValidationError(
                        "SELF_LOCATION_TRANSITION",
                        path,
                        $"Location '{location.Id}' cannot transition to itself."));
                }
                else if (!locationIds.Contains(targetId))
                {
                    errors.Add(new ContentValidationError(
                        "MISSING_LOCATION_TRANSITION",
                        path,
                        $"Transition target '{targetId}' does not exist."));
                }
            }
        }
    }

    private static void ValidateMetadata(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(package.ContentVersion))
        {
            errors.Add(new ContentValidationError(
                "MISSING_CONTENT_VERSION",
                "contentVersion",
                "ContentVersion is required."));
        }

        if (string.IsNullOrWhiteSpace(package.BalanceVersion))
        {
            errors.Add(new ContentValidationError(
                "MISSING_BALANCE_VERSION",
                "balanceVersion",
                "BalanceVersion is required."));
        }

        if (package.PublishedAtUtc.Offset != TimeSpan.Zero)
        {
            errors.Add(new ContentValidationError(
                "PUBLISHED_AT_NOT_UTC",
                "publishedAtUtc",
                "PublishedAtUtc must use a zero UTC offset."));
        }
    }

    private static void ValidateReferences(
        GameContentPackage package,
        IReadOnlySet<ContentKey> definitions,
        List<ContentValidationError> errors)
    {
        for (var definitionIndex = 0; definitionIndex < package.Definitions.Count; definitionIndex++)
        {
            GameContentDefinition definition = package.Definitions[definitionIndex];

            for (var referenceIndex = 0; referenceIndex < definition.References.Count; referenceIndex++)
            {
                GameContentReference reference = definition.References[referenceIndex];
                string path = $"definitions[{definitionIndex}].references[{referenceIndex}]";

                bool typeIsValid = ValidateIdentifier(
                    reference.Type,
                    "INVALID_REFERENCE_TYPE",
                    $"{path}.type",
                    errors);
                bool idIsValid = ValidateIdentifier(
                    reference.Id,
                    "INVALID_REFERENCE_ID",
                    $"{path}.id",
                    errors);

                if (typeIsValid
                    && idIsValid
                    && !definitions.Contains(new ContentKey(reference.Type, reference.Id)))
                {
                    errors.Add(new ContentValidationError(
                        "MISSING_REFERENCE",
                        path,
                        $"Referenced definition '{reference.Type}:{reference.Id}' does not exist."));
                }
            }
        }
    }

    private static bool ValidateIdentifier(
        string value,
        string errorCode,
        string path,
        List<ContentValidationError> errors)
    {
        if (IsCanonicalIdentifier(value))
        {
            return true;
        }

        errors.Add(new ContentValidationError(
            errorCode,
            path,
            $"'{value}' must use uppercase ASCII letters, digits, and underscores, starting with a letter."));

        return false;
    }

    private static bool IsCanonicalIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || value[0] is < 'A' or > 'Z')
        {
            return false;
        }

        foreach (char character in value)
        {
            if (character is not (>= 'A' and <= 'Z')
                && character is not (>= '0' and <= '9')
                && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct ContentKey(string Type, string Id);
}
