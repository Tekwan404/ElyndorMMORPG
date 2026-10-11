using Elyndor.Core.World;

namespace Elyndor.UnitTests.World;

public sealed class LocationEncounterAvailabilityTests
{
    private static readonly DateTimeOffset Epoch = DateTimeOffset.UnixEpoch;

    [Fact]
    public void OrdinaryEncounterIsAlwaysAvailable()
    {
        var encounter = new LocationEncounterDefinition("WOLF");
        Assert.True(LocationEncounterAvailability.IsAvailable(encounter, Epoch));
        Assert.Null(LocationEncounterAvailability.NextChange(encounter, Epoch));
    }

    [Theory]
    [InlineData(119, false, 120)]
    [InlineData(120, true, 180)]
    [InlineData(179, true, 180)]
    [InlineData(180, false, 720)]
    [InlineData(720, true, 780)]
    public void RareWindowUsesInclusiveStartAndExclusiveEnd(int seconds, bool available, int next)
    {
        var encounter = new LocationEncounterDefinition("ELITE",
            Availability: new EncounterAvailabilityDefinition(600, 60, 120));
        var now = Epoch.AddSeconds(seconds);
        Assert.Equal(available, LocationEncounterAvailability.IsAvailable(encounter, now));
        Assert.Equal(Epoch.AddSeconds(next), LocationEncounterAvailability.NextChange(encounter, now));
    }

    [Fact]
    public void AvailabilityIsStableAcrossRepeatedReadsAndBeforeEpoch()
    {
        var encounter = new LocationEncounterDefinition("ELITE",
            Availability: new EncounterAvailabilityDefinition(600, 60, 120));
        Assert.True(LocationEncounterAvailability.IsAvailable(encounter, Epoch.AddSeconds(-480)));
        Assert.Equal(Epoch.AddSeconds(-420),
            LocationEncounterAvailability.NextChange(encounter, Epoch.AddSeconds(-480)));
        Assert.Equal(LocationEncounterAvailability.NextChange(encounter, Epoch.AddSeconds(170)),
            LocationEncounterAvailability.NextChange(encounter, Epoch.AddSeconds(170)));
    }
}
