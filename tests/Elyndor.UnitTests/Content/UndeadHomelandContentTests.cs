using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class UndeadHomelandContentTests
{
    [Fact]
    public async Task UndeadHomelandMirrorsEveryHumanLocationWithoutSharingLocationIdentity()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(ContentPath());
        var humanLocations = package.Locations.Where(location => location.MapId == "BORDERLANDS").ToArray();
        var undeadLocations = package.Locations.Where(location => location.MapId == "DEAD_KINGDOMS").ToArray();
        Assert.NotEmpty(undeadLocations);
        Assert.Equal(humanLocations.Length, undeadLocations.Length);
        foreach (var human in humanLocations)
        {
            var undead = Assert.Single(undeadLocations, location => location.Id == "UNDEAD_" + human.Id);
            Assert.NotEqual(human.DisplayName, undead.DisplayName);
            Assert.Equal("HUMAN", human.RequiredRaceId);
            Assert.Equal("UNDEAD", undead.RequiredRaceId);
            Assert.Equal(human.MinimumLevel, undead.MinimumLevel);
            Assert.Equal(human.MaximumLevel, undead.MaximumLevel);
            Assert.Equal(human.Encounters, undead.Encounters);
            Assert.DoesNotContain(undead.Transitions, id => humanLocations.Any(location => location.Id == id));
        }
        Assert.NotEqual(package.Locations.Single(location => location.Id == "STARTER_TOWN").ArtId,
            undeadLocations.Single(location => location.Id == "UNDEAD_STARTER_TOWN").ArtId);
        Assert.Contains(package.Merchants!, merchant => merchant.LocationId == "UNDEAD_STARTER_TOWN");
        Assert.Contains(package.Quests!, quest => quest.OfferLocationId == "UNDEAD_STARTER_TOWN");
        Assert.Contains("UNDEAD_OBSIDIAN_EDGE", package.Locations.Single(location => location.Id == "BLACK_CRAGS").Transitions);
    }

    [Fact]
    public async Task OuterReachesIsSharedButDeadReachesRequiresLevelSixty()
    {
        var package = await GameContentPackageLoader.LoadAsync(ContentPath());
        var shared = package.Locations.Where(location => location.RequiredRaceId is null).ToArray();
        Assert.Equal(6, shared.Length);
        Assert.All(shared, location => Assert.Equal("OUTER_REACHES", location.MapId));
        Assert.DoesNotContain(package.Locations, location => location.Id == "UNDEAD_BLACK_CRAGS");
        Assert.Equal(60, Assert.Single(shared, location => location.Id == "DEAD_REACHES").MinimumLevel);
        Assert.Contains("OBSIDIAN_EDGE", package.Locations.Single(location => location.Id == "BLACK_CRAGS").Transitions);
    }

    private static string ContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string path = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(path)) return path;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository content not found.");
    }
}
