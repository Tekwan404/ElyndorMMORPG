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

        WorldMap worldMap = package.WorldMap;
        LocationDefinition[] ordinaryZones =
        [
            worldMap.GetRequired("WHISPERING_FOREST"),
            worldMap.GetRequired("DEEP_FOREST"),
            worldMap.GetRequired("BROODMOTHER_LAIR"),
            worldMap.GetRequired("BLIGHTED_GROVE")
        ];

        Assert.All(ordinaryZones, zone => Assert.Equal(1, zone.MinimumLevel));
        Assert.All(ordinaryZones, zone => Assert.Null(zone.RequiredContractId));

        Assert.Equal(9, worldMap.GetRequired("DEEP_FOREST").RecommendedLevel);
        Assert.Equal(14, worldMap.GetRequired("BROODMOTHER_LAIR").RecommendedLevel);
        Assert.Equal(17, worldMap.GetRequired("BLIGHTED_GROVE").RecommendedLevel);
    }
}
