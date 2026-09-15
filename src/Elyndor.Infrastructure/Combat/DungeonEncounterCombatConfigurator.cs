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
            case DungeonEncounterMechanicIds.VelariusMana:
                session.ConfigureVelariusEncounter(BuildVelariusProfile(encounter, contentSnapshot.Indexes));
                break;
            case DungeonEncounterMechanicIds.MorEtSouls:
                session.ConfigureMorEtEncounter(BuildMorEtProfile(encounter, contentSnapshot.Indexes));
                break;
            case DungeonEncounterMechanicIds.AzraelTriune:
                session.ConfigureAzraelEncounter(BuildAzraelProfile(encounter, contentSnapshot.Indexes));
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
        IReadOnlyList<DungeonEncounterAddDefinition> addDefinitions = RequireAdds(encounter);
        MirrorEncounterAddProfile[] adds = addDefinitions
            .Select(add =>
            {
                EncounterEnemyProfile resolved = ResolveEnemyProfile(add, indexes);
                return new MirrorEncounterAddProfile(
                    ResolveMirrorRole(add.Role),
                    resolved.Monster,
                    resolved.AiProfile);
            })
            .ToArray();
        return new MirrorCombatEncounterProfile(adds);
    }

    private static VelariusCombatEncounterProfile BuildVelariusProfile(
        DungeonEncounterDefinition encounter,
        GameContentIndexes indexes)
    {
        DungeonEncounterAddDefinition feeder = RequireAdds(encounter).Single(add =>
            string.Equals(add.Role, DungeonEncounterAddRoles.ManaFeeder, StringComparison.Ordinal));
        return new VelariusCombatEncounterProfile(ResolveEnemyProfile(feeder, indexes));
    }

    private static MorEtCombatEncounterProfile BuildMorEtProfile(
        DungeonEncounterDefinition encounter,
        GameContentIndexes indexes)
    {
        Dictionary<string, EncounterEnemyProfile> profiles = new(StringComparer.Ordinal);
        foreach (DungeonEncounterAddDefinition add in RequireAdds(encounter))
        {
            string classId = add.Role switch
            {
                DungeonEncounterAddRoles.SoulWarrior => "WARRIOR",
                DungeonEncounterAddRoles.SoulMage => "MAGE",
                DungeonEncounterAddRoles.SoulArcher => "ARCHER",
                DungeonEncounterAddRoles.SoulPaladin => "PALADIN",
                _ => throw new InvalidOperationException($"Unknown Mor-Et soul role '{add.Role}'.")
            };
            profiles.Add(classId, ResolveEnemyProfile(add, indexes));
        }
        return new MorEtCombatEncounterProfile(profiles);
    }

    private static AzraelCombatEncounterProfile BuildAzraelProfile(
        DungeonEncounterDefinition encounter,
        GameContentIndexes indexes)
    {
        Dictionary<AzraelCloneRole, EncounterEnemyProfile> profiles = [];
        foreach (DungeonEncounterAddDefinition add in RequireAdds(encounter))
        {
            AzraelCloneRole role = add.Role switch
            {
                DungeonEncounterAddRoles.Fire => AzraelCloneRole.Fire,
                DungeonEncounterAddRoles.Frost => AzraelCloneRole.Frost,
                DungeonEncounterAddRoles.Void => AzraelCloneRole.Void,
                _ => throw new InvalidOperationException($"Unknown Azrael clone role '{add.Role}'.")
            };
            profiles.Add(role, ResolveEnemyProfile(add, indexes));
        }
        return new AzraelCombatEncounterProfile(profiles);
    }

    private static IReadOnlyList<DungeonEncounterAddDefinition> RequireAdds(
        DungeonEncounterDefinition encounter) =>
        encounter.Adds
        ?? throw new InvalidOperationException(
            $"Encounter '{encounter.Id}' has no linked add definitions.");

    private static EncounterEnemyProfile ResolveEnemyProfile(
        DungeonEncounterAddDefinition add,
        GameContentIndexes indexes)
    {
        if (!indexes.MonstersById.TryGetValue(add.MonsterId, out MonsterDefinition? monster))
        {
            throw new InvalidOperationException(
                $"Encounter add monster '{add.MonsterId}' is missing from game content.");
        }

        if (!indexes.MonsterAiProfilesById.TryGetValue(
                monster.AiProfileId,
                out MonsterAiProfile? aiProfile))
        {
            throw new InvalidOperationException(
                $"Encounter add AI profile '{monster.AiProfileId}' is missing from game content.");
        }

        return new EncounterEnemyProfile(monster, aiProfile);
    }

    private static MirrorEncounterAddRole ResolveMirrorRole(string role) => role switch
    {
        DungeonEncounterAddRoles.Guardian => MirrorEncounterAddRole.Guardian,
        DungeonEncounterAddRoles.Priest => MirrorEncounterAddRole.Priest,
        DungeonEncounterAddRoles.Executioner => MirrorEncounterAddRole.Executioner,
        _ => throw new InvalidOperationException($"Unknown mirror encounter add role '{role}'.")
    };
}
