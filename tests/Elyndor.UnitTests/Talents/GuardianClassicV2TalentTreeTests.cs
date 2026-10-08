using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Talents;

public sealed class GuardianClassicV2TalentTreeTests
{
    private static readonly IReadOnlyDictionary<int, string[]> ExpectedTiers =
        new Dictionary<int, string[]>
        {
            [1] = ["G-1-1", "G-1-2", "G-1-3", "G-1-4", "G-1-5"],
            [2] = ["G-2-1", "G-2-2", "G-2-4", "G-2-5"],
            [3] = ["G-2-3", "G-3-1", "G-3-3", "G-3-5"],
            [4] = ["G-3-2", "G-3-4", "G-3-6", "G-4-1"],
            [5] = ["G-4-2", "G-4-3", "G-5-1", "G-6-1"],
            [6] = ["G-4-4", "G-4-5", "G-5-3"],
            [7] = ["G-5-2", "G-5-4", "G-6-3"],
            [8] = ["G-5-5", "G-6-2", "G-6-4"],
            [9] = ["G-6-5"],
        };

    [Fact]
    public async Task ComposedGuardianHasNineExecutableTiersWithAllStableTalentIds()
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
            .ToArray();

        Assert.Equal(31, guardian.NodeCount);
        Assert.Equal(31, nodes.Length);
        Assert.Equal(Enumerable.Range(1, 9), nodes.Select(node => node.Tier).Distinct().OrderBy(tier => tier));
        Assert.Equal(5, warrior.Version);

        foreach ((int tier, string[] expectedIds) in ExpectedTiers)
        {
            TalentDefinition[] tierNodes = nodes.Where(node => node.Tier == tier).ToArray();
            Assert.Equal(
                expectedIds.OrderBy(id => id, StringComparer.Ordinal),
                tierNodes.Select(node => node.Id).OrderBy(id => id, StringComparer.Ordinal));
            Assert.All(tierNodes, node =>
            {
                Assert.Equal((tier - 1) * 5, node.RequiredSpentPoints);
                Assert.Equal(5, node.Version);
            });
        }

        Dictionary<string, TalentDefinition> indexedNodes = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        foreach (TalentDefinition node in nodes)
        {
            foreach (TalentPrerequisite prerequisite in node.Prerequisites)
            {
                Assert.True(indexedNodes[prerequisite.TalentId].Tier <= node.Tier,
                    $"Prerequisite {prerequisite.TalentId} must not unlock later than {node.Id}.");
            }
        }

        TalentDefinition capstone = indexedNodes["G-6-5"];
        Assert.Equal(9, capstone.Tier);
        Assert.Equal(40, capstone.RequiredSpentPoints);
        Assert.Equal("G-6-1", Assert.Single(capstone.Prerequisites).TalentId);
        Assert.DoesNotContain(
            nodes.SelectMany(node => node.Modifiers ?? []),
            modifier => string.Equals(modifier.Key, "DODGE_PERCENT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GuardianCapstoneRequiresFortyBranchPointsAndAFreeLevelFortyTwoPoint()
    {
        var package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        TalentTreeDefinition tree = Assert.Single(package.TalentTrees!, tree => tree.Id == "WARRIOR_TREE");

        // Legal progression: 12 + 8 + 6 + 8 + 6 points, including Shield Slam.
        // Existing Guardian IDs and ranks are intentionally retained, not migrated/reset.
        Dictionary<string, int> ranks = new(StringComparer.Ordinal)
        {
            ["G-1-1"] = 1, ["G-1-2"] = 3, ["G-1-3"] = 3, ["G-1-4"] = 3, ["G-1-5"] = 2,
            ["G-2-1"] = 1, ["G-2-2"] = 1, ["G-2-4"] = 3, ["G-2-5"] = 3,
            ["G-2-3"] = 3, ["G-3-1"] = 1, ["G-3-3"] = 1, ["G-3-5"] = 1,
            ["G-3-2"] = 2, ["G-3-4"] = 2, ["G-3-6"] = 3, ["G-4-1"] = 1,
            ["G-4-2"] = 1, ["G-4-3"] = 3, ["G-5-1"] = 1, ["G-6-1"] = 1,
        };
        Assert.Equal(40, ranks.Values.Sum());
        Assert.Empty(TalentRules.ValidateBuild(tree, 42, ranks));

        Dictionary<string, int> early = ranks.Where(pair => pair.Key != "G-4-2")
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        TalentLearnResult tierLocked = TalentRules.TryLearn(tree, 60, early, "G-6-5");
        Assert.False(tierLocked.IsSuccess);
        Assert.Equal(TalentErrorCodes.TierLocked, tierLocked.ErrorCode);

        TalentLearnResult noEarnedPoint = TalentRules.TryLearn(tree, 41, ranks, "G-6-5");
        Assert.False(noEarnedPoint.IsSuccess);
        Assert.Equal(TalentErrorCodes.InsufficientPoints, noEarnedPoint.ErrorCode);

        TalentLearnResult learned = TalentRules.TryLearn(tree, 42, ranks, "G-6-5");
        Assert.True(learned.IsSuccess);
        Assert.Equal(1, learned.SelectedRanks["G-6-5"]);
        Assert.Equal(0, learned.AvailablePoints);
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
