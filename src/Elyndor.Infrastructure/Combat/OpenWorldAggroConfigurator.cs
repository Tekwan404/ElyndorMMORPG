using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Core.World;

namespace Elyndor.Infrastructure.Combat;

/// <summary>
/// One possible nearby aggro per ordinary field combat. Never used for raids,
/// authored boss encounters, dungeons, training or world boss sessions.
/// Uses the pinned content snapshot and existing combat single-writer scheduler.
/// </summary>
public static class OpenWorldAggroConfigurator
{
    private static readonly TimeSpan AggroDelay = TimeSpan.FromSeconds(6);
    private const int MaximumLevelDifference = 2;

    public static string? SelectNearbyMonster(
        LocationDefinition location,
        MonsterDefinition primary,
        IReadOnlyDictionary<string, MonsterDefinition> monsters,
        decimal aggroRoll,
        decimal monsterRoll)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(monsters);
        if (aggroRoll is < 0 or >= 1 || monsterRoll is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(aggroRoll), "Rolls must be in [0, 1).");

        if (string.Equals(location.DangerLevel, "SAFE", StringComparison.Ordinal)
            || primary.Rank == MonsterRank.Boss
            || location.Encounters is not { Count: > 1 }
            || !location.Encounters.Any(encounter =>
                string.Equals(encounter.MonsterId, primary.Id, StringComparison.Ordinal)))
            return null;

        decimal chance = primary.Rank == MonsterRank.Elite ? 0.12m : 0.22m;
        if (aggroRoll >= chance) return null;

        LocationEncounterDefinition[] candidates = location.Encounters
            .Where(encounter => !string.Equals(encounter.MonsterId, primary.Id, StringComparison.Ordinal))
            .Where(encounter => monsters.TryGetValue(encounter.MonsterId, out MonsterDefinition? monster)
                && monster.Rank == MonsterRank.Normal
                && Math.Abs(monster.Level - primary.Level) <= MaximumLevelDifference)
            .ToArray();

        if (candidates.Length == 0) return null;
        return WorldEncounterSelector.Select(candidates, monsterRoll).MonsterId;
    }

    public static void Configure(
        CombatSession session,
        MonsterDefinition primary,
        LocationDefinition location,
        GameContentSnapshot snapshot,
        decimal aggroRoll,
        decimal monsterRoll)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(snapshot);
        GameContentIndexes indexes = snapshot.Indexes;
        string? monsterId = SelectNearbyMonster(
            location, primary, indexes.MonstersById, aggroRoll, monsterRoll);
        if (monsterId is null) return;

        MonsterDefinition monster = indexes.MonstersById[monsterId];
        if (!indexes.MonsterAiProfilesById.TryGetValue(monster.AiProfileId, out MonsterAiProfile? ai))
            return;

        // Keep the primary monster alive to trigger one timed add; no periodic
        // respawns or extra loot/xp/gold are granted for an ambient intruder.
        EncounterDefinition encounter = new(
            $"OPEN_WORLD_AGGRO_{primary.Id}",
            primary.Id,
            [
                new EncounterPhaseDefinition(
                    "NEARBY_ENEMY_JOINS",
                    new EncounterTriggerDefinition(
                        EncounterTriggerType.ElapsedTime,
                        Elapsed: AggroDelay),
                    [
                        new EncounterActionDefinition(
                            EncounterActionType.Summon,
                            Summon: new SummonDefinition(
                                monsterId,
                                Count: 1,
                                MaxActive: 1,
                                DespawnOnBossDeath: false,
                                NoReward: true,
                                IsAmbientAggro: true))
                    ])
            ]);

        session.ConfigureGenericEncounter(
            encounter,
            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal)
            {
                [monsterId] = new(monster, ai)
            },
            indexes.EffectsById);
    }
}
