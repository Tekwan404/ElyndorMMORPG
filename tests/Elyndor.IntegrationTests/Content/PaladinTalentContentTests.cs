using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class PaladinTalentContentTests
{
    [Fact]
    public async Task ComposedPackageContainsPlayablePaladinTalentTree()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        var indexes = Elyndor.Core.Content.GameContentIndexes.For(package);

        TalentTreeDefinition tree = Assert.IsType<TalentTreeDefinition>(
            indexes.TalentTreesByClassId["PALADIN"]);

        Assert.Equal("PALADIN_TREE", tree.Id);
        Assert.Equal(59, tree.MaxSpendablePoints);
        Assert.Equal(96, tree.Nodes.Count);
        Assert.Equal(
            ["HOLY", "PROTECTION", "RETRIBUTION"],
            tree.Branches.Select(branch => branch.Id));
        Assert.All(
            tree.Nodes,
            node => Assert.True(
                TalentRuntimeAvailability.IsNodeFullySupported(node),
                $"Paladin talent '{node.Id}' is not runtime available."));
    }
}
