using Elyndor.Core.Dungeons;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    internal static void ValidateDungeons(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        IReadOnlyList<DungeonDefinition> dungeons = package.Dungeons ?? [];
        HashSet<string> dungeonIds = new(StringComparer.Ordinal);
        HashSet<string> locationIds = package.Locations
            .Select(location => location.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> monsterIds = (package.Monsters ?? [])
            .Select(monster => monster.Id)
            .ToHashSet(StringComparer.Ordinal);

        for (var index = 0; index < dungeons.Count; index++)
        {
            DungeonDefinition dungeon = dungeons[index];
            string path = $"dungeons[{index}]";
            bool validId = ValidateIdentifier(dungeon.Id, "INVALID_DUNGEON_ID", $"{path}.id", errors);
            if (validId && !dungeonIds.Add(dungeon.Id))
            {
                errors.Add(new ContentValidationError(
                    "DUPLICATE_DUNGEON_ID",
                    $"{path}.id",
                    $"Dungeon '{dungeon.Id}' is duplicated."));
            }

            if (!locationIds.Contains(dungeon.EntryLocationId)
                || dungeon.MinimumLevel <= 0
                || dungeon.MaximumLevel < dungeon.MinimumLevel
                || dungeon.MinimumPartySize <= 0
                || dungeon.MaximumPartySize < dungeon.MinimumPartySize
                || dungeon.MaximumPartySize > 5
                || dungeon.Encounters.Count == 0)
            {
                errors.Add(new ContentValidationError(
                    "INVALID_DUNGEON_DEFINITION",
                    path,
                    $"Dungeon '{dungeon.Id}' has invalid entry, level, party, or encounter settings."));
            }

            HashSet<string> encounterIds = new(StringComparer.Ordinal);
            for (var encounterIndex = 0; encounterIndex < dungeon.Encounters.Count; encounterIndex++)
            {
                DungeonEncounterDefinition encounter = dungeon.Encounters[encounterIndex];
                string encounterPath = $"{path}.encounters[{encounterIndex}]";
                if (!ValidateIdentifier(
                        encounter.Id,
                        "INVALID_DUNGEON_ENCOUNTER_ID",
                        $"{encounterPath}.id",
                        errors)
                    || !encounterIds.Add(encounter.Id)
                    || !monsterIds.Contains(encounter.MonsterId)
                    || string.IsNullOrWhiteSpace(encounter.CheckpointId))
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_DUNGEON_ENCOUNTER",
                        encounterPath,
                        $"Dungeon '{dungeon.Id}' contains an invalid encounter reference."));
                }

                ValidateEncounterMechanic(
                    dungeon,
                    encounter,
                    encounterPath,
                    monsterIds,
                    errors);
            }
        }
    }

    private static void ValidateEncounterMechanic(
        DungeonDefinition dungeon,
        DungeonEncounterDefinition encounter,
        string encounterPath,
        HashSet<string> monsterIds,
        List<ContentValidationError> errors)
    {
        IReadOnlyList<DungeonEncounterAddDefinition> adds = encounter.Adds ?? [];
        if (string.IsNullOrWhiteSpace(encounter.MechanicId))
        {
            if (adds.Count > 0)
            {
                errors.Add(new ContentValidationError(
                    "INVALID_DUNGEON_ENCOUNTER_MECHANIC",
                    $"{encounterPath}.adds",
                    $"Dungeon '{dungeon.Id}' encounter '{encounter.Id}' defines adds without a mechanic id."));
            }
            return;
        }

        string[]? requiredRoles = encounter.MechanicId switch
        {
            DungeonEncounterMechanicIds.MirrorBarrier =>
            [
                DungeonEncounterAddRoles.Guardian,
                DungeonEncounterAddRoles.Priest,
                DungeonEncounterAddRoles.Executioner
            ],
            DungeonEncounterMechanicIds.VelariusMana =>
            [
                DungeonEncounterAddRoles.ManaFeeder
            ],
            DungeonEncounterMechanicIds.MorEtSouls =>
            [
                DungeonEncounterAddRoles.SoulWarrior,
                DungeonEncounterAddRoles.SoulMage,
                DungeonEncounterAddRoles.SoulArcher,
                DungeonEncounterAddRoles.SoulPaladin
            ],
            DungeonEncounterMechanicIds.AzraelTriune =>
            [
                DungeonEncounterAddRoles.Fire,
                DungeonEncounterAddRoles.Frost,
                DungeonEncounterAddRoles.Void
            ],
            _ => null
        };

        if (requiredRoles is null)
        {
            errors.Add(new ContentValidationError(
                "INVALID_DUNGEON_ENCOUNTER_MECHANIC",
                $"{encounterPath}.mechanicId",
                $"Dungeon '{dungeon.Id}' encounter '{encounter.Id}' references unknown mechanic '{encounter.MechanicId}'."));
            return;
        }

        HashSet<string> seenRoles = new(StringComparer.Ordinal);
        bool invalidAdd = adds.Count != requiredRoles.Length;
        foreach (DungeonEncounterAddDefinition add in adds)
        {
            if (!requiredRoles.Contains(add.Role, StringComparer.Ordinal)
                || !seenRoles.Add(add.Role)
                || !monsterIds.Contains(add.MonsterId))
            {
                invalidAdd = true;
            }
        }
        if (requiredRoles.Any(role => !seenRoles.Contains(role)))
            invalidAdd = true;

        if (invalidAdd)
        {
            errors.Add(new ContentValidationError(
                "INVALID_DUNGEON_ENCOUNTER_ADD",
                $"{encounterPath}.adds",
                $"Encounter '{encounter.Id}' requires exactly one valid add profile for each role: {string.Join(", ", requiredRoles)}."));
        }
    }
}
