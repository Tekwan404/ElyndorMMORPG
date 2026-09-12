using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class OpenWorldAccessContentTests
{
    [Fact]
    public async Task OrdinaryWorldZonesUseRecommendedLevelWithoutLevelOrBossContractGate()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        LocationDefinition GetLocation(string id) =>
            package.Locations.Single(location => location.Id == id);

        LocationDefinition[] ordinaryZones =
        [
            GetLocation("WHISPERING_FOREST"),
            GetLocation("DEEP_FOREST"),
            GetLocation("BROODMOTHER_LAIR"),
            GetLocation("BLIGHTED_GROVE")
        ];

        Assert.All(ordinaryZones, zone => Assert.Equal(1, zone.MinimumLevel));
        Assert.All(ordinaryZones, zone => Assert.Null(zone.RequiredContractId));

        Assert.Equal(9, GetLocation("DEEP_FOREST").RecommendedLevel);
        Assert.Equal(14, GetLocation("BROODMOTHER_LAIR").RecommendedLevel);
        Assert.Equal(17, GetLocation("BLIGHTED_GROVE").RecommendedLevel);
    }
}
