using Elyndor.Core.WorldBosses;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    internal static void ValidateWorldBossDefinitions(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        HashSet<string> ids = [];
        IReadOnlyList<WorldBossDefinition> definitions = package.WorldBosses ?? [];
        for (int index = 0; index < definitions.Count; index++)
        {
            WorldBossDefinition definition = definitions[index];
            string path = $"worldBosses[{index}]";
            bool idValid = ValidateIdentifier(
                definition.Id,
                "INVALID_WORLD_BOSS_ID",
                $"{path}.id",
                errors);
            if (idValid && !ids.Add(definition.Id))
            {
                errors.Add(new(
                    "DUPLICATE_WORLD_BOSS_ID",
                    path,
                    $"World boss '{definition.Id}' is duplicated."));
            }

            if (string.IsNullOrWhiteSpace(definition.Name)
                || definition.Level <= 0
                || definition.BaseMaxHealth <= 0
                || definition.DurationSeconds <= 0
                || string.IsNullOrWhiteSpace(definition.EncounterProfileId)
                || string.IsNullOrWhiteSpace(definition.LootTableId)
                || string.IsNullOrWhiteSpace(definition.RewardProfileId)
                || string.IsNullOrWhiteSpace(definition.TokenCurrencyId)
                || definition.Phases.Count == 0)
            {
                errors.Add(new(
                    "INVALID_WORLD_BOSS_DEFINITION",
                    path,
                    $"World boss '{definition.Id}' contains invalid required values."));
                continue;
            }

            var encounter = package.Encounters?.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Id,
                    definition.EncounterProfileId,
                    StringComparison.Ordinal));
            if (encounter is null)
            {
                errors.Add(new(
                    "WORLD_BOSS_ENCOUNTER_NOT_FOUND",
                    $"{path}.encounterProfileId",
                    $"World boss '{definition.Id}' references missing encounter '{definition.EncounterProfileId}'."));
            }
            else
            {
                var monster = package.Monsters?.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, encounter.MonsterId, StringComparison.Ordinal));
                if (monster is null)
                {
                    errors.Add(new(
                        "WORLD_BOSS_MONSTER_NOT_FOUND",
                        $"{path}.encounterProfileId",
                        $"World boss encounter '{encounter.Id}' references missing monster '{encounter.MonsterId}'."));
                }
                else
                {
                    if (monster.Level != definition.Level
                        || monster.MaxHp != definition.BaseMaxHealth)
                    {
                        errors.Add(new(
                            "WORLD_BOSS_COMBAT_PROFILE_MISMATCH",
                            path,
                            $"World boss '{definition.Id}' combat profile must match level and global max health."));
                    }

                    if (monster.XpReward != 0
                        || monster.GoldRewardMin != 0
                        || monster.GoldRewardMax != 0
                        || !string.IsNullOrWhiteSpace(monster.LootTableId))
                    {
                        errors.Add(new(
                            "WORLD_BOSS_STANDARD_REWARD_LEAK",
                            path,
                            $"World boss '{definition.Id}' must not use ordinary PvE rewards."));
                    }
                }
            }

            WorldBossRewardProfileDefinition? rewardProfile =
                package.WorldBossRewardProfiles?.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, definition.RewardProfileId, StringComparison.Ordinal));
            if (rewardProfile is null)
            {
                errors.Add(new(
                    "WORLD_BOSS_REWARD_PROFILE_NOT_FOUND",
                    $"{path}.rewardProfileId",
                    $"World boss '{definition.Id}' references missing reward profile '{definition.RewardProfileId}'."));
            }
            else
            {
                bool invalidRewardProfile =
                    rewardProfile.MinimumContribution <= 0
                    || rewardProfile.BossExperience < 0
                    || rewardProfile.BossGold < 0
                    || rewardProfile.ChestGoldMin < 0
                    || rewardProfile.ChestGoldMax < rewardProfile.ChestGoldMin
                    || rewardProfile.Tiers.Count == 0
                    || rewardProfile.Tiers.Any(tier =>
                        tier.MinimumContribution < rewardProfile.MinimumContribution)
                    || rewardProfile.Tiers
                        .Select(tier => tier.Tier)
                        .Distinct()
                        .Count() != rewardProfile.Tiers.Count;
                if (invalidRewardProfile)
                {
                    errors.Add(new(
                        "INVALID_WORLD_BOSS_REWARD_PROFILE",
                        $"{path}.rewardProfileId",
                        $"World boss reward profile '{rewardProfile.Id}' contains invalid values."));
                }
            }

            var chestTable = package.LootTables?.FirstOrDefault(table =>
                string.Equals(table.Id, definition.LootTableId, StringComparison.Ordinal));
            if (chestTable is null)
            {
                errors.Add(new(
                    "WORLD_BOSS_LOOT_TABLE_NOT_FOUND",
                    $"{path}.lootTableId",
                    $"World boss '{definition.Id}' references missing chest loot table '{definition.LootTableId}'."));
            }
            else
            {
                bool invalidChest =
                    chestTable.Entries.Count != 0
                    || chestTable.SelectionGroups is not { Count: 1 }
                    || chestTable.SelectionGroups[0].Rolls != 1
                    || chestTable.SelectionGroups[0].Entries.Count == 0;
                if (invalidChest)
                {
                    errors.Add(new(
                        "INVALID_WORLD_BOSS_CHEST_TABLE",
                        $"{path}.lootTableId",
                        $"World boss chest '{chestTable.Id}' must contain exactly one personal selection roll."));
                }
            }

            decimal previousThreshold = decimal.MaxValue;
            for (int phaseIndex = 0; phaseIndex < definition.Phases.Count; phaseIndex++)
            {
                WorldBossPhaseDefinition phase = definition.Phases[phaseIndex];
                string phasePath = $"{path}.phases[{phaseIndex}]";
                bool invalid = phase.Phase != phaseIndex + 1
                    || string.IsNullOrWhiteSpace(phase.Name)
                    || phase.StartsAtHealthPercent <= 0
                    || phase.StartsAtHealthPercent > 100
                    || phase.StartsAtHealthPercent >= previousThreshold
                    || phaseIndex == 0 && phase.StartsAtHealthPercent != 100;
                if (invalid)
                {
                    errors.Add(new(
                        "INVALID_WORLD_BOSS_PHASE",
                        phasePath,
                        $"World boss '{definition.Id}' has an invalid phase sequence."));
                }

                previousThreshold = phase.StartsAtHealthPercent;
            }
        }
    }
}
