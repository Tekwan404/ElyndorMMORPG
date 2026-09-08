using Elyndor.Core.Content;
using Elyndor.Core.World;

namespace Elyndor.Core.Quests;

public static class QuestCatalog
{
    public static IReadOnlyList<QuestDefinition> Resolve(GameContentPackage package)
    {
        Dictionary<string, QuestDefinition> quests = (package.Quests ?? [])
            .ToDictionary(quest => quest.Id, StringComparer.Ordinal);

        foreach (WorldContractDefinition contract in package.WorldContracts ?? [])
        {
            if (quests.ContainsKey(contract.Id))
                continue;

            quests.Add(
                contract.Id,
                new QuestDefinition(
                    contract.Id,
                    contract.DisplayName,
                    contract.Description,
                    QuestType.Contract,
                    contract.RequiredLevel,
                    contract.OfferLocationId ?? contract.UnlockLocationId,
                    [
                        new QuestObjectiveDefinition(
                            "KILL_TARGET",
                            QuestObjectiveType.KillMonster,
                            contract.TargetMonsterId,
                            1)
                    ],
                    contract.RewardXp,
                    contract.RewardGold,
                    UnlockLocationId: contract.UnlockLocationId));
        }

        return quests.Values
            .OrderBy(quest => quest.RequiredLevel)
            .ThenBy(quest => quest.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static QuestDefinition? Find(
        GameContentPackage package,
        string questId) =>
        Resolve(package).FirstOrDefault(quest =>
            string.Equals(quest.Id, questId, StringComparison.Ordinal));

    public static bool IsLegacyContract(
        GameContentPackage package,
        string questId) =>
        (package.WorldContracts ?? []).Any(contract =>
            string.Equals(contract.Id, questId, StringComparison.Ordinal));
}
