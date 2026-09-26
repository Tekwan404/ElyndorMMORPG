using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Economy;

public sealed class PremiumStoreConsumableContentTests
{
    [Fact]
    public async Task EnhancementOreIsAnEnabledPremiumStoreOffer()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition item = Assert.Single(
            package.Items!,
            candidate => candidate.Id == "ENHANCEMENT_ORE");
        PremiumStoreOfferDefinition offer = Assert.Single(
            package.PremiumStoreOffers!,
            candidate => candidate.Sku == "ENHANCEMENT_ORE_SMALL");

        Assert.Equal(ItemType.Material, item.Type);
        Assert.True(item.Stackable);
        Assert.True(item.PremiumEligible);
        Assert.Equal(item.Id, offer.ItemDefinitionId);
        Assert.Equal(20, offer.Quantity);
        Assert.Equal(30, offer.CrystalPrice);
        Assert.True(offer.Enabled);
        Assert.Equal(20, offer.PerAccountLimit);
    }
}
