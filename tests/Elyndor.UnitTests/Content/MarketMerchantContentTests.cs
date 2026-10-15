using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class MarketMerchantContentTests
{
    [Fact]
    public async Task MarketMerchantsHaveSeparateClassCatalogsAndKeepSuppliesAvailable()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "content", "package.json"))) root = root.Parent;
        Assert.NotNull(root);
        var package = await GameContentPackageLoader.LoadAsync(Path.Combine(root.FullName, "content", "package.json"));
        foreach (var (merchantId, classId) in new[] { ("BORIN_ARMORY", "WARRIOR"), ("LIORA_HUNTING", "ARCHER"), ("ELARA_ARCANA", "MAGE") })
        {
            var merchant = Assert.Single(package.Merchants!, merchant => merchant.Id == merchantId);
            Assert.NotEmpty(merchant.ItemIds);
            Assert.All(merchant.ItemIds, itemId => Assert.StartsWith(classId + "_", itemId, StringComparison.Ordinal));
            Assert.Equal("STARTER_TOWN", merchant.LocationId);
        }
        var supplies = Assert.Single(package.Merchants!, merchant => merchant.Id == "MARCUS_SUPPLIES");
        Assert.Contains("SMALL_HEALING_POTION", supplies.ItemIds);
        Assert.Contains("ENHANCEMENT_ORE", supplies.ItemIds);
        Assert.DoesNotContain(supplies.ItemIds, itemId => itemId.StartsWith("WARRIOR_", StringComparison.Ordinal)
            || itemId.StartsWith("ARCHER_", StringComparison.Ordinal) || itemId.StartsWith("MAGE_", StringComparison.Ordinal));
    }
}
