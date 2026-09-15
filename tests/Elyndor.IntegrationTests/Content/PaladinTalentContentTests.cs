using Elyndor.Core.Characters;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class PaladinTalentContentTests
{
    [Fact]
    public async Task PaladinStartsWithItsBaselineHealingAndAuraAbilities()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        var paladin = Assert.Single(package.ClassProfiles!, profile => profile.Id == "PALADIN");

        Assert.Equal(
            ["DEVOTION_AURA", "FLASH_OF_LIGHT", "HOLY_LIGHT", "JUDGEMENT", "LAY_ON_HANDS", "SEAL_OF_RIGHTEOUSNESS"],
            CharacterKnownAbilityResolver.Resolve(paladin, level: 1));
    }

    [Fact]
    public async Task PaladinTalentPlayerFacingTextIsRussian()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        var tree = Assert.Single(package.TalentTrees!, tree => tree.ClassId == "PALADIN");

        Assert.All(tree.Branches, branch => Assert.DoesNotMatch("[A-Za-z]{2,}", branch.Fantasy));
        Assert.All(tree.Nodes, node =>
        {
            Assert.DoesNotMatch("[A-Za-z]{2,}", node.Name);
            Assert.DoesNotMatch("[A-Za-z]{2,}", node.Description);
        });
    }

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
