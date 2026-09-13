using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Talents;

public sealed class GuardianClassicV2TalentTreeTests
{
    [Fact]
    public async Task ComposedGuardianBranchIsExactlyTheApprovedThirtyOneNodeTree()
    {
        var package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        TalentTreeDefinition warrior = Assert.Single(
            package.TalentTrees!,
            tree => tree.Id == "WARRIOR_TREE");
        TalentBranchDefinition guardian = Assert.Single(
            warrior.Branches,
            branch => branch.Id == "GUARDIAN");
        TalentDefinition[] nodes = warrior.Nodes
            .Where(node => node.BranchId == "GUARDIAN")
            .OrderBy(node => node.Tier)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
        string[] expectedIds =
        [
            "G-1-1", "G-1-2", "G-1-3", "G-1-4", "G-1-5",
            "G-2-1", "G-2-2", "G-2-3", "G-2-4", "G-2-5",
            "G-3-1", "G-3-2", "G-3-3", "G-3-4", "G-3-5", "G-3-6",
            "G-4-1", "G-4-2", "G-4-3", "G-4-4", "G-4-5",
            "G-5-1", "G-5-2", "G-5-3", "G-5-4", "G-5-5",
            "G-6-1", "G-6-2", "G-6-3", "G-6-4", "G-6-5"
        ];

        Assert.Equal(31, guardian.NodeCount);
        Assert.Equal(31, nodes.Length);
        Assert.Equal(expectedIds, nodes.Select(node => node.Id));
        Assert.All(nodes, node => Assert.InRange(node.Tier, 1, 6));
        Assert.DoesNotContain(nodes, node => node.Id.StartsWith("G-7-", StringComparison.Ordinal));
        Assert.DoesNotContain(nodes, node => node.Id.StartsWith("G-8-", StringComparison.Ordinal));
        Assert.DoesNotContain(nodes, node => node.Id.StartsWith("G-9-", StringComparison.Ordinal));
        Assert.DoesNotContain(
            nodes.SelectMany(node => node.Modifiers ?? []),
            modifier => string.Equals(modifier.Key, "DODGE_PERCENT", StringComparison.Ordinal));
    }

    private static string RepositoryContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
