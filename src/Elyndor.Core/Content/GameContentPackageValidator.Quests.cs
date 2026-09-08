using Elyndor.Core.Items;
using Elyndor.Core.Quests;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    internal static void ValidateQuests(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        IReadOnlyList<QuestDefinition> quests = package.Quests ?? [];
        HashSet<string> questIds = quests
            .Select(quest => quest.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> prerequisiteIds = questIds
            .Concat((package.WorldContracts ?? []).Select(contract => contract.Id))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> locationIds = package.Locations
            .Select(location => location.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> monsterIds = (package.Monsters ?? [])
            .Select(monster => monster.Id)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, ItemDefinition> items = (package.Items ?? [])
            .ToDictionary(item => item.Id, StringComparer.Ordinal);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var questIndex = 0; questIndex < quests.Count; questIndex++)
        {
            QuestDefinition quest = quests[questIndex];
            string path = $"quests[{questIndex}]";

            bool idValid = ValidateIdentifier(
                quest.Id,
                "INVALID_QUEST_ID",
                $"{path}.id",
                errors);
            if (idValid && !seen.Add(quest.Id))
            {
                errors.Add(new(
                    "DUPLICATE_QUEST_ID",
                    $"{path}.id",
                    $"Quest '{quest.Id}' is duplicated."));
            }

            if (string.IsNullOrWhiteSpace(quest.DisplayName)
                || string.IsNullOrWhiteSpace(quest.Description)
                || quest.RequiredLevel <= 0
                || !locationIds.Contains(quest.OfferLocationId)
                || quest.RewardXp < 0
                || quest.RewardGold < 0
                || quest.UnlockLocationId is { Length: > 0 } unlock
                    && !locationIds.Contains(unlock))
            {
                errors.Add(new(
                    "INVALID_QUEST",
                    path,
                    $"Quest '{quest.Id}' contains invalid presentation, level, location, or rewards."));
            }

            if (quest.Type == QuestType.Contract
                && (string.IsNullOrWhiteSpace(quest.IssuerName)
                    || string.IsNullOrWhiteSpace(quest.RegionName)
                    || string.IsNullOrWhiteSpace(quest.ContractNumber)
                    || string.IsNullOrWhiteSpace(quest.ThreatLevel)))
            {
                errors.Add(new(
                    "INVALID_CONTRACT_PRESENTATION",
                    path,
                    $"Contract '{quest.Id}' requires issuer, region, contract number, and threat level."));
            }

            if (quest.Type == QuestType.Side
                && string.IsNullOrWhiteSpace(quest.IssuerName))
            {
                errors.Add(new(
                    "INVALID_ERRAND_PRESENTATION",
                    path,
                    $"Errand '{quest.Id}' requires an issuer."));
            }

            if (quest.Objectives.Count == 0)
            {
                errors.Add(new(
                    "QUEST_WITHOUT_OBJECTIVES",
                    $"{path}.objectives",
                    $"Quest '{quest.Id}' must have at least one objective."));
            }

            HashSet<string> objectiveIds = new(StringComparer.Ordinal);
            for (var objectiveIndex = 0;
                 objectiveIndex < quest.Objectives.Count;
                 objectiveIndex++)
            {
                QuestObjectiveDefinition objective =
                    quest.Objectives[objectiveIndex];
                string objectivePath =
                    $"{path}.objectives[{objectiveIndex}]";

                if (!ValidateIdentifier(
                        objective.Id,
                        "INVALID_QUEST_OBJECTIVE_ID",
                        $"{objectivePath}.id",
                        errors)
                    || !objectiveIds.Add(objective.Id))
                {
                    errors.Add(new(
                        "DUPLICATE_QUEST_OBJECTIVE_ID",
                        $"{objectivePath}.id",
                        $"Quest objective '{objective.Id}' is invalid or duplicated."));
                }

                if (objective.RequiredCount <= 0)
                {
                    errors.Add(new(
                        "INVALID_QUEST_OBJECTIVE_COUNT",
                        $"{objectivePath}.requiredCount",
                        "Quest objective count must be positive."));
                }

                switch (objective.Type)
                {
                    case QuestObjectiveType.KillMonster:
                        if (!monsterIds.Contains(objective.TargetId)
                            || objective.ConsumeOnClaim)
                        {
                            errors.Add(new(
                                "INVALID_QUEST_KILL_OBJECTIVE",
                                objectivePath,
                                $"Kill objective '{objective.Id}' references an invalid monster or consumption rule."));
                        }
                        break;

                    case QuestObjectiveType.CollectItem:
                        if (!items.TryGetValue(
                                objective.TargetId,
                                out ItemDefinition? item)
                            || !item.Stackable
                            || item.Type == ItemType.Equipment)
                        {
                            errors.Add(new(
                                "INVALID_QUEST_COLLECT_OBJECTIVE",
                                objectivePath,
                                $"Collect objective '{objective.Id}' must reference a stackable non-equipment item."));
                        }
                        break;
                }
            }

            foreach (string prerequisite in quest.PrerequisiteQuestIds ?? [])
            {
                if (!prerequisiteIds.Contains(prerequisite)
                    || string.Equals(prerequisite, quest.Id, StringComparison.Ordinal))
                {
                    errors.Add(new(
                        "INVALID_QUEST_PREREQUISITE",
                        $"{path}.prerequisiteQuestIds",
                        $"Quest '{quest.Id}' references invalid prerequisite '{prerequisite}'."));
                }
            }

            foreach (QuestItemRewardDefinition reward in quest.RewardItems ?? [])
            {
                if (reward.Quantity <= 0 || !items.ContainsKey(reward.ItemId))
                {
                    errors.Add(new(
                        "INVALID_QUEST_ITEM_REWARD",
                        $"{path}.rewardItems",
                        $"Quest '{quest.Id}' has an invalid item reward."));
                }
            }
        }
    }
}
