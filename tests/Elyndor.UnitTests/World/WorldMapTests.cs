using Elyndor.Core.World;

namespace Elyndor.UnitTests.World;

public sealed class WorldMapTests
{
    [Fact]
    public void CanTravelAcceptsOnlyConfiguredTransitions()
    {
        WorldMap worldMap = new(CreatePrototypeLocations());

        Assert.True(worldMap.CanTravel("STARTER_TOWN", "WHISPERING_FOREST"));
        Assert.True(worldMap.CanTravel("WHISPERING_FOREST", "STARTER_TOWN"));
        Assert.True(worldMap.CanTravel("WHISPERING_FOREST", "DEEP_FOREST"));
        Assert.True(worldMap.CanTravel("DEEP_FOREST", "WHISPERING_FOREST"));
        Assert.False(worldMap.CanTravel("STARTER_TOWN", "DEEP_FOREST"));
        Assert.False(worldMap.CanTravel("MISSING", "STARTER_TOWN"));
    }

    [Fact]
    public void GetRequiredReturnsTheConfiguredDefinition()
    {
        WorldMap worldMap = new(CreatePrototypeLocations());

        LocationDefinition location = worldMap.GetRequired("WHISPERING_FOREST");

        Assert.Equal("Whispering Forest", location.DisplayName);
        Assert.Equal("ADVENTURE", location.DangerLevel);
        Assert.Equal(1, location.RecommendedLevel);
    }

    [Fact]
    public void GetRequiredRejectsUnknownLocation()
    {
        WorldMap worldMap = new(CreatePrototypeLocations());

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => worldMap.GetRequired("MISSING"));

        Assert.Contains("MISSING", exception.Message, StringComparison.Ordinal);
    }

    private static LocationDefinition[] CreatePrototypeLocations() =>
    [
        new("STARTER_TOWN", "Starter Town", "SAFE", 1, ["WHISPERING_FOREST"]),
        new(
            "WHISPERING_FOREST",
            "Whispering Forest",
            "ADVENTURE",
            1,
            ["STARTER_TOWN", "DEEP_FOREST"]),
        new("DEEP_FOREST", "Deep Forest", "DANGEROUS", 3, ["WHISPERING_FOREST"])
    ];
}
