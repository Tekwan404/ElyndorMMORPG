using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Items;

public sealed class ItemReforgeCostContentTests
{
    [Fact]
    public async Task ReforgeCostsUseReforgeStonesWithoutCatalysts()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        var costs = Assert.IsType<Elyndor.Core.Items.ItemReforgeCostProfileDefinition>(
            package.Itemization?.ReforgeCosts);

        Assert.Equal("REFORGE_STONE", costs.MaterialItemId);
        Assert.All(costs.MaterialQuantityByRarity.Values, quantity => Assert.True(quantity > 0));
        Assert.All(costs.CatalystQuantityByRarity.Values, quantity => Assert.Equal(0, quantity));
    }

    [Fact]
    public async Task MageWeaponAndFocusPoolsAllowCriticalDamage()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        var itemization = Assert.IsType<Elyndor.Core.Items.ItemizationDefinition>(package.Itemization);

        foreach (string poolId in new[] { "MAGE_WEAPON", "MAGE_FOCUS", "MAGE_OFFHAND" })
        {
            var pool = Assert.Single(itemization.AffixPools, candidate => candidate.Id == poolId);
            Assert.Contains(Elyndor.Core.Items.ItemStatIds.CriticalDamage, pool.StatIds);
        }
    }

    private static string RepositoryContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
