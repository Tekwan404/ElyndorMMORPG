using Elyndor.Core.Content;
using Elyndor.Core.World;

namespace Elyndor.Core.Quests;

public static class QuestCatalog
{
    // Mirrored regions share persistent quest identity, so moving an existing undead
    // character preserves progress and cannot grant a second copy of story rewards.
    public static GameContentPackage ForRace(GameContentPackage package, string raceId)
    {
        var locations = package.Locations.ToDictionary(location => location.Id, StringComparer.Ordinal);
        bool Available(string locationId) => !locations.TryGetValue(locationId, out var location)
            || WorldLocationAccess.CanEnter(location, raceId);
        static string ProgressId(string id) => id.StartsWith("UNDEAD_", StringComparison.Ordinal) ? id[7..] : id;
        return package with
        {
            Quests = package.Quests?.Where(quest => Available(quest.OfferLocationId))
                .Select(quest => quest with
                {
                    Id = ProgressId(quest.Id),
                    PrerequisiteQuestIds = quest.PrerequisiteQuestIds?.Select(ProgressId).ToArray()
                }).ToArray(),
            WorldContracts = package.WorldContracts?.Where(contract => Available(contract.OfferLocationId ?? contract.UnlockLocationId))
                .Select(contract => contract with { Id = ProgressId(contract.Id) }).ToArray()
        };
    }

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
