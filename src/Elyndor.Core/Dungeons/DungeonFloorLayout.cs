namespace Elyndor.Core.Dungeons;

public sealed record DungeonFloorRange(int Number, int FirstEncounterIndex, int LastEncounterIndex);

public static class DungeonFloorLayout
{
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
