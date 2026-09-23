using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Economy;

public sealed class SpatialArtifactPremiumStoreContentTests
{
    private static readonly IReadOnlyDictionary<string, (int CapacityBonus, long CrystalPrice)> ExpectedOffers =
        new Dictionary<string, (int, long)>(StringComparer.Ordinal)
        {
            ["SPATIAL_CRACKED_RING"] = (5, 75),
            ["SPATIAL_MINOR_RING"] = (10, 125),
            ["SPATIAL_EXPANDED_RING"] = (15, 350),
            ["SPATIAL_SEAL"] = (20, 300),
            ["SPATIAL_BOTTOMLESS_RING"] = (25, 425),
            ["SPATIAL_POCKET_SHARD"] = (30, 600),
            ["SPATIAL_VOID_SEAL"] = (40, 900)
        };

    [Fact]
    public async Task SpatialArtifactsAreEnabledPremiumStoreOffers()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        Assert.NotNull(package.Items);
        Assert.NotNull(package.PremiumStoreOffers);

        foreach ((string itemId, (int capacityBonus, long crystalPrice)) in ExpectedOffers)
        {
            ItemDefinition item = Assert.Single(
                package.Items,
                candidate => string.Equals(candidate.Id, itemId, StringComparison.Ordinal));
            Assert.Equal(ItemType.SpatialArtifact, item.Type);
            Assert.True(item.PremiumEligible);
            Assert.Equal(capacityBonus, item.InventoryCapacityBonus);

            PremiumStoreOfferDefinition offer = Assert.Single(
                package.PremiumStoreOffers,
                candidate => string.Equals(candidate.Sku, itemId, StringComparison.Ordinal));
            Assert.Equal(itemId, offer.ItemDefinitionId);
            Assert.Equal(1, offer.Quantity);
            Assert.Equal(crystalPrice, offer.CrystalPrice);
            Assert.True(offer.Enabled);
            Assert.Equal(1, offer.PerAccountLimit);
        }
    }
}
