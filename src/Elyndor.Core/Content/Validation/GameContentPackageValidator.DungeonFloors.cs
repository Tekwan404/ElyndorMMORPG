using Elyndor.Core.Dungeons;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    private static void ValidateDungeonFloors(DungeonDefinition dungeon, string path, List<ContentValidationError> errors)
    {
        if (dungeon.Floors is null) return;
        IReadOnlyList<DungeonFloorDefinition> floors = dungeon.Floors;
        HashSet<string> ids = new(StringComparer.Ordinal);
        int next = 0;
        bool invalid = floors.Count == 0;
        for (int index = 0; index < floors.Count; index++)
        {
            DungeonFloorDefinition floor = floors[index];
            if (!ValidateIdentifier(floor.Id, "INVALID_DUNGEON_FLOOR", $"{path}.floors[{index}].id", errors)
                || !ids.Add(floor.Id) || string.IsNullOrWhiteSpace(floor.DisplayName)
                || floor.FirstEncounterIndex != next || floor.LastEncounterIndex < floor.FirstEncounterIndex
                || floor.LastEncounterIndex >= dungeon.Encounters.Count)
                invalid = true;
            next = floor.LastEncounterIndex + 1;
        }
        if (next != dungeon.Encounters.Count) invalid = true;
        if (invalid)
        {
            errors.Add(new("INVALID_DUNGEON_FLOORS", $"{path}.floors",
                "Floors must have unique ids and cover every encounter exactly once in authored order."));
            return;
        }
        for (int index = 0; index < floors.Count; index++)
        {
            HashSet<int> visited = [];
            int current = index;
            while (floors[current].RequiredEncounterId is { } required)
            {
                int requiredIndex = dungeon.Encounters.ToList().FindIndex(encounter => encounter.Id == required);
                if (!visited.Add(current) || requiredIndex < 0)
                {
                    errors.Add(new("INVALID_DUNGEON_FLOOR_UNLOCK", $"{path}.floors[{index}]",
                        "Floor unlock must reference an existing encounter without dependency cycles."));
                    break;
                }
                current = Enumerable.Range(0, floors.Count).Single(number => requiredIndex >= floors[number].FirstEncounterIndex
                    && requiredIndex <= floors[number].LastEncounterIndex);
            }
        }
        if (floors.Count > 0 && floors[0].RequiredEncounterId is not null)
            errors.Add(new("INVALID_DUNGEON_FLOOR_UNLOCK", $"{path}.floors[0]", "The entry floor must be open."));
    }
}
