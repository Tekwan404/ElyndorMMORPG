using Elyndor.Core.Dungeons;

namespace Elyndor.UnitTests.Dungeons;

public sealed class DungeonFloorLayoutTests
{
    [Fact]
    public void ExplicitFloorsDoNotDependOnBossPositions()
    {
        DungeonDefinition definition = Definition();
        var floors = DungeonFloorLayout.Build(definition);
        Assert.Equal(2, floors.Count);
        Assert.Equal((0, 0), (floors[0].FirstEncounterIndex, floors[0].LastEncounterIndex));
        Assert.Equal((1, 2), (floors[1].FirstEncounterIndex, floors[1].LastEncounterIndex));
    }

    [Fact]
    public void FloorCanBeOpenOrRequireOneSpecificEncounter()
    {
        DungeonDefinition definition = Definition();
        var floors = DungeonFloorLayout.Build(definition);
        Assert.True(DungeonFloorLayout.IsUnlocked(definition, floors[0], new HashSet<int>()));
        Assert.False(DungeonFloorLayout.IsUnlocked(definition, floors[1], new HashSet<int>()));
        Assert.True(DungeonFloorLayout.IsUnlocked(definition, floors[1], new HashSet<int> { 0 }));
        definition = definition with { Floors = [definition.Floors![0], definition.Floors[1] with { RequiredEncounterId = null }] };
        Assert.True(DungeonFloorLayout.IsUnlocked(definition, DungeonFloorLayout.Build(definition)[1], new HashSet<int>()));
    }

    [Fact]
    public void EachFloorKeepsItsOwnNextUnfinishedEncounter()
    {
        var floors = DungeonFloorLayout.Build(Definition());
        Assert.Equal(1, DungeonFloorLayout.NextEncounterIndex(floors[1], new HashSet<int> { 0 }));
        Assert.Equal(2, DungeonFloorLayout.NextEncounterIndex(floors[1], new HashSet<int> { 1 }));
        Assert.Null(DungeonFloorLayout.NextEncounterIndex(floors[1], new HashSet<int> { 1, 2 }));
        Assert.Equal(0, DungeonFloorLayout.NextEncounterIndex(floors[0], new HashSet<int> { 1, 2 }));
    }

    private static DungeonDefinition Definition() => new("TEST_DUNGEON", "Dungeon", "Test", 1, 10,
        "ENTRY", [new("MOB", "M1", "C1"), new("BOSS1", "M2", "C2", true), new("BOSS2", "M3", "C3", true)],
        Floors: [new("ENTRANCE", "Entrance", 0, 0), new("HALL", "Hall", 1, 2, "MOB")]);

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
