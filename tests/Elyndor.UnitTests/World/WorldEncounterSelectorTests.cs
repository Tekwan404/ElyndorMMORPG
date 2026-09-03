using Elyndor.Core.World;

namespace Elyndor.UnitTests.World;

public sealed class WorldEncounterSelectorTests
{
    private static readonly LocationEncounterDefinition[] Encounters =
    [
        new("WOLF", 1),
        new("FOREST_BOAR", 2),
        new("GIANT_SPIDER", 1)
    ];

    [Theory]
    [InlineData(0.00, "WOLF")]
    [InlineData(0.24, "WOLF")]
    [InlineData(0.25, "FOREST_BOAR")]
    [InlineData(0.74, "FOREST_BOAR")]
    [InlineData(0.75, "GIANT_SPIDER")]
    [InlineData(0.99, "GIANT_SPIDER")]
    public void SelectUsesContentWeights(double roll, string expectedMonsterId)
    {
        LocationEncounterDefinition selected = WorldEncounterSelector.Select(
            Encounters,
            (decimal)roll);

        Assert.Equal(expectedMonsterId, selected.MonsterId);
    }

    [Fact]
    public void SelectRejectsEmptyOrInvalidWeights()
    {
        Assert.Throws<ArgumentException>(() =>
            WorldEncounterSelector.Select([], 0.5m));
        Assert.Throws<ArgumentException>(() =>
            WorldEncounterSelector.Select([new("WOLF", 0)], 0.5m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldEncounterSelector.Select(Encounters, 1m));
    }
}
