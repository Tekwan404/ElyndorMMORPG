using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Monsters;

namespace Elyndor.Infrastructure.Combat;

internal static class DungeonEncounterCombatConfigurator
{
    public static void Configure(
        CombatSession session,
        string monsterId,
        GameContentSnapshot contentSnapshot)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(monsterId);
        ArgumentNullException.ThrowIfNull(contentSnapshot);

        DungeonEncounterDefinition[] configuredEncounters =
            (contentSnapshot.Package.Dungeons ?? [])
                .SelectMany(dungeon => dungeon.Encounters)
                .Where(encounter =>
                    string.Equals(encounter.MonsterId, monsterId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(encounter.MechanicId))
                .ToArray();

        if (configuredEncounters.Length == 0)
            return;
        if (configuredEncounters.Length != 1)
        {
            throw new InvalidOperationException(
                $"Monster '{monsterId}' is bound to more than one configured dungeon mechanic encounter.");
        }

        DungeonEncounterDefinition encounter = configuredEncounters[0];
        switch (encounter.MechanicId)
        {
            case DungeonEncounterMechanicIds.MirrorBarrier:
                session.ConfigureMirrorEncounter(BuildMirrorProfile(encounter, contentSnapshot.Indexes));
                break;
            default:
                throw new InvalidOperationException(
                    $"Dungeon encounter mechanic '{encounter.MechanicId}' is not supported by combat runtime.");
        }
    }

    private static MirrorCombatEncounterProfile BuildMirrorProfile(
        DungeonEncounterDefinition encounter,
        GameContentIndexes indexes)
    {
        IReadOnlyList<DungeonEncounterAddDefinition> addDefinitions = encounter.Adds
            ?? throw new InvalidOperationException(
                $"Mirror encounter '{encounter.Id}' has no linked add definitions.");

        MirrorEncounterAddProfile[] adds = addDefinitions
            .Select(add => ResolveMirrorAdd(add, indexes))
            .ToArray();
        return new MirrorCombatEncounterProfile(adds);
    }

    private static MirrorEncounterAddProfile ResolveMirrorAdd(
        DungeonEncounterAddDefinition add,
        GameContentIndexes indexes)
    {
        if (!indexes.MonstersById.TryGetValue(add.MonsterId, out MonsterDefinition? monster))
        {
            throw new InvalidOperationException(
                $"Mirror add monster '{add.MonsterId}' is missing from game content.");
        }

        if (!indexes.MonsterAiProfilesById.TryGetValue(
                monster.AiProfileId,
                out MonsterAiProfile? aiProfile))
        {
            throw new InvalidOperationException(
                $"Mirror add AI profile '{monster.AiProfileId}' is missing from game content.");
        }

        return new MirrorEncounterAddProfile(
            ResolveMirrorRole(add.Role),
            monster,
            aiProfile);
    }

    private static MirrorEncounterAddRole ResolveMirrorRole(string role) => role switch
    {
        DungeonEncounterAddRoles.Guardian => MirrorEncounterAddRole.Guardian,
        DungeonEncounterAddRoles.Priest => MirrorEncounterAddRole.Priest,
        DungeonEncounterAddRoles.Executioner => MirrorEncounterAddRole.Executioner,
        _ => throw new InvalidOperationException($"Unknown mirror encounter add role '{role}'.")
    };
}
