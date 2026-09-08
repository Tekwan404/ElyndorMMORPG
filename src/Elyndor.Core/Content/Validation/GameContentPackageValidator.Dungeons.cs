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
            }
        }
    }
}
