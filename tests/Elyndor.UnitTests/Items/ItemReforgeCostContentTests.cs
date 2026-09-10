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
