using Elyndor.Core.World;

namespace Elyndor.UnitTests.World;

public sealed class WorldMapTests
{
    [Fact]
    public void CanTravelAcceptsAnyKnownDestinationForTesting()
    {
        WorldMap worldMap = new(CreatePrototypeLocations());

        Assert.True(worldMap.CanTravel("STARTER_TOWN", "WHISPERING_FOREST"));
        Assert.True(worldMap.CanTravel("STARTER_TOWN", "DEEP_FOREST"));
        Assert.True(worldMap.CanTravel("DEEP_FOREST", "STARTER_TOWN"));
        Assert.False(worldMap.CanTravel("STARTER_TOWN", "STARTER_TOWN"));
        Assert.False(worldMap.CanTravel("MISSING", "STARTER_TOWN"));
        Assert.False(worldMap.CanTravel("STARTER_TOWN", "MISSING"));
    }

    [Fact]
    public void ConstructorMakesKnownLocationsInstantAndDirectForTesting()
    {
        WorldMap worldMap = new(CreatePrototypeLocations());

        LocationDefinition starterTown = worldMap.GetRequired("STARTER_TOWN");
        LocationDefinition deepForest = worldMap.GetRequired("DEEP_FOREST");

        Assert.Equal(0m, starterTown.TravelDurationSeconds);
        Assert.Equal(0m, deepForest.TravelDurationSeconds);
        Assert.Contains("DEEP_FOREST", starterTown.Transitions);
        Assert.Contains("STARTER_TOWN", deepForest.Transitions);
        Assert.DoesNotContain("STARTER_TOWN", starterTown.Transitions);
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
        new(
            "STARTER_TOWN",
            "Starter Town",
            "SAFE",
            1,
            ["WHISPERING_FOREST"],
            TravelDurationSeconds: 12m),
        new(
            "WHISPERING_FOREST",
            "Whispering Forest",
            "ADVENTURE",
            1,
            ["STARTER_TOWN", "DEEP_FOREST"],
            TravelDurationSeconds: 18m),
        new(
            "DEEP_FOREST",
            "Deep Forest",
            "DANGEROUS",
            3,
            ["WHISPERING_FOREST"],
            TravelDurationSeconds: 24m)
    ];
}
