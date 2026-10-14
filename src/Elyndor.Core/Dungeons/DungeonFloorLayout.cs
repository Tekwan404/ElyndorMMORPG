namespace Elyndor.Core.Dungeons;

public sealed record DungeonFloorRange(int Number, int FirstEncounterIndex, int LastEncounterIndex,
    string? DisplayName = null, string? RequiredEncounterId = null);

public static class DungeonFloorLayout
{
    public static IReadOnlyList<DungeonFloorRange> Build(DungeonDefinition definition) =>
        definition.Floors is { Count: > 0 }
            ? definition.Floors.Select((floor, index) => new DungeonFloorRange(index + 1,
                floor.FirstEncounterIndex, floor.LastEncounterIndex, floor.DisplayName, floor.RequiredEncounterId)).ToArray()
            : Build(definition.Encounters).Select(floor => floor with
            {
                RequiredEncounterId = floor.Number == 1 ? null : definition.Encounters[floor.FirstEncounterIndex - 1].Id
            }).ToArray();

    public static bool IsUnlocked(DungeonDefinition definition, DungeonFloorRange floor,
        IReadOnlySet<int> completedEncounterIndexes) => floor.RequiredEncounterId is null
        || completedEncounterIndexes.Any(index => index >= 0 && index < definition.Encounters.Count
            && definition.Encounters[index].Id == floor.RequiredEncounterId);

    public static int? NextEncounterIndex(DungeonFloorRange floor, IReadOnlySet<int> completedEncounterIndexes)
    {
        for (int index = floor.FirstEncounterIndex; index <= floor.LastEncounterIndex; index++)
            if (!completedEncounterIndexes.Contains(index)) return index;
        return null;
    }

    public static IReadOnlyList<DungeonFloorRange> Build(IReadOnlyList<DungeonEncounterDefinition> encounters)
    {
        List<DungeonFloorRange> floors = [];
        int first = 0;
        for (int index = 0; index < encounters.Count; index++)
        {
            if (!encounters[index].IsBoss && index != encounters.Count - 1) continue;
            floors.Add(new DungeonFloorRange(floors.Count + 1, first, index));
            first = index + 1;
        }
        return floors;
    }
}
