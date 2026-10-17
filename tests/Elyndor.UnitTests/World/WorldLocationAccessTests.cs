using Elyndor.Core.World;

namespace Elyndor.UnitTests.World;

public sealed class WorldLocationAccessTests
{
    [Theory]
    [InlineData("HUMAN", "HUMAN", true)]
    [InlineData("UNDEAD", "UNDEAD", true)]
    [InlineData("HUMAN", "UNDEAD", false)]
    [InlineData("UNDEAD", "HUMAN", false)]
    [InlineData(null, "UNDEAD", true)]
    [InlineData(null, "HUMAN", true)]
    public void EntryDependsOnAuthoredRaceRestriction(string? requiredRace, string race, bool allowed)
    {
        var location = new LocationDefinition("TEST", "Test", "SAFE", 1, [], RequiredRaceId: requiredRace);
        Assert.Equal(allowed, WorldLocationAccess.CanEnter(location, race));
    }

    [Theory]
    [InlineData("HUMAN", "STARTER_TOWN")]
    [InlineData("UNDEAD", "UNDEAD_STARTER_TOWN")]
    public void HomeAndWorkshopUseTheSameFactionCity(string race, string expected)
    {
        Assert.Equal(expected, WorldLocationAccess.HomeTown(race));
        Assert.Equal(expected, WorldLocationAccess.RequiredLocation("STARTER_TOWN", race));
        Assert.True(WorldLocationAccess.IsTown(expected));
        Assert.Equal("BLACK_CRAGS", WorldLocationAccess.RequiredLocation("BLACK_CRAGS", race));
    }
}
