using Elyndor.Core.Content;
using Elyndor.Core.Quests;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Quests;

public sealed record QuestProgressUpdateResult(
    IReadOnlyList<string> ReadyQuestIds,
    IReadOnlyList<string> CompletedLegacyContractIds);

public static class QuestProgression
{
    public static async Task<QuestProgressUpdateResult> ApplyKillsAsync(
        GameDbContext dbContext,
        Guid characterId,
        Guid combatSessionId,
        IReadOnlyList<string> defeatedMonsterIds,
        GameContentPackage content,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (defeatedMonsterIds.Count == 0)
            return new([], []);

        CharacterQuestState[] states = await dbContext.CharacterQuestStates
            .Where(state => state.CharacterId == characterId
                && state.Status != QuestStateStatuses.Completed)
            .ToArrayAsync(cancellationToken);
        Dictionary<string, CharacterQuestState> byQuestId =
            states.ToDictionary(state => state.QuestId, StringComparer.Ordinal);

        CharacterContractAcceptance[] legacyAcceptances =
            await dbContext.CharacterContractAcceptances
                .Where(state => state.CharacterId == characterId)
                .ToArrayAsync(cancellationToken);
        HashSet<string> legacyCompleted = (await dbContext.CharacterContractCompletions
                .Where(state => state.CharacterId == characterId)
                .Select(state => state.ContractId)
                .ToArrayAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach (CharacterContractAcceptance acceptance in legacyAcceptances)
        {
            if (legacyCompleted.Contains(acceptance.ContractId)
                || byQuestId.ContainsKey(acceptance.ContractId))
            {
                continue;
            }

            CharacterQuestState migrated = new(
                characterId,
                acceptance.ContractId,
                acceptance.AcceptedAtUtc);
            dbContext.CharacterQuestStates.Add(migrated);
            byQuestId.Add(acceptance.ContractId, migrated);
        }

        Dictionary<string, int> defeatedCounts = defeatedMonsterIds
            .GroupBy(id => id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        List<string> ready = [];
        List<string> completedLegacyContracts = [];
        foreach (QuestDefinition quest in QuestCatalog.Resolve(content))
        {
            if (!byQuestId.TryGetValue(quest.Id, out CharacterQuestState? state))
                continue;

            Dictionary<string, int> progress =
                QuestProgressJson.Read(state.ProgressJson);
            bool changed = false;
            foreach (QuestObjectiveDefinition objective in quest.Objectives)
            {
                if (objective.Type != QuestObjectiveType.KillMonster
                    || !defeatedCounts.TryGetValue(
                        objective.TargetId,
                        out int defeatedCount))
                {
                    continue;
                }

                int current = progress.GetValueOrDefault(objective.Id);
                int updated = Math.Min(
                    objective.RequiredCount,
                    checked(current + defeatedCount));
                if (updated != current)
                {
                    progress[objective.Id] = updated;
                    changed = true;
                }
            }

            if (!changed)
                continue;

            bool killObjectivesReady = quest.Objectives
                .Where(objective => objective.Type == QuestObjectiveType.KillMonster)
                .All(objective =>
                    progress.GetValueOrDefault(objective.Id)
                    >= objective.RequiredCount);
            bool hasCollectObjectives = quest.Objectives.Any(
                objective => objective.Type == QuestObjectiveType.CollectItem);
            bool readyToClaim = killObjectivesReady && !hasCollectObjectives;
            state.UpdateProgress(
                QuestProgressJson.Write(progress),
                readyToClaim,
                now);
            if (readyToClaim)
                ready.Add(quest.Id);

            if (readyToClaim
                && QuestCatalog.IsLegacyContract(content, quest.Id)
                && !legacyCompleted.Contains(quest.Id))
            {
                QuestObjectiveDefinition target = quest.Objectives.Single(
                    objective => objective.Type == QuestObjectiveType.KillMonster);
                dbContext.CharacterContractCompletions.Add(
                    new CharacterContractCompletion(
                        characterId,
                        quest.Id,
                        target.TargetId,
                        combatSessionId,
                        now));
                legacyCompleted.Add(quest.Id);
                completedLegacyContracts.Add(quest.Id);
            }
        }

        return new(ready, completedLegacyContracts);
    }
}
