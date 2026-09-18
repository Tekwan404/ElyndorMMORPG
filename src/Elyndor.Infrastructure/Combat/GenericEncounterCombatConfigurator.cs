using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;

namespace Elyndor.Infrastructure.Combat;

public static class GenericEncounterCombatConfigurator
{
    public static void Configure(
        CombatSession session,
        string monsterId,
        GameContentSnapshot contentSnapshot)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(monsterId);
        ArgumentNullException.ThrowIfNull(contentSnapshot);

        GameContentIndexes indexes = contentSnapshot.Indexes;
        if (!indexes.EncountersByMonsterId.TryGetValue(monsterId, out var encounter))
            return;

        string[] summonMonsterIds = encounter.Phases
            .SelectMany(phase => phase.Actions)
            .Where(action => action.Summon is not null)
            .Select(action => action.Summon!.MonsterId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, EncounterEnemyProfile> summonProfiles = new(StringComparer.Ordinal);
        foreach (string summonMonsterId in summonMonsterIds)
        {
            if (!indexes.MonstersById.TryGetValue(
                    summonMonsterId,
                    out MonsterDefinition? summonedMonster))
            {
                throw new InvalidOperationException(
                    $"Generic encounter '{encounter.Id}' references missing summon monster '{summonMonsterId}'.");
            }
            if (!indexes.MonsterAiProfilesById.TryGetValue(
                    summonedMonster.AiProfileId,
                    out MonsterAiProfile? summonedAi))
            {
                throw new InvalidOperationException(
                    $"Generic encounter summon '{summonMonsterId}' references missing AI '{summonedMonster.AiProfileId}'.");
            }

            summonProfiles.Add(
                summonMonsterId,
                new EncounterEnemyProfile(summonedMonster, summonedAi));
        }

        session.ConfigureGenericEncounter(
            encounter,
            summonProfiles,
            indexes.EffectsById);
    }
}
