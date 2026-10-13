using Elyndor.Core.Dungeons;

namespace Elyndor.UnitTests.Dungeons;

public sealed class DungeonFloorLayoutTests
{
    [Fact]
    public void BossesEndFloorsAndAllEncounterIndexesRemainInOrder()
    {
        DungeonEncounterDefinition[] encounters = [new("a", "m1", "c1"), new("b", "m2", "c2", true), new("c", "m3", "c3", true), new("d", "m4", "c4")];
        var floors = DungeonFloorLayout.Build(encounters);
        Assert.Equal(3, floors.Count);
        Assert.Equal((0, 1), (floors[0].FirstEncounterIndex, floors[0].LastEncounterIndex));
        Assert.Equal((2, 2), (floors[1].FirstEncounterIndex, floors[1].LastEncounterIndex));
        Assert.Equal((3, 3), (floors[2].FirstEncounterIndex, floors[2].LastEncounterIndex));
        Assert.Equal(Enumerable.Range(0, 4), floors.SelectMany(floor => Enumerable.Range(floor.FirstEncounterIndex, floor.LastEncounterIndex - floor.FirstEncounterIndex + 1)));
    }
}
