using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Talents;

public sealed class TalentRulesTests
{
    private static readonly TalentTreeDefinition Tree = new(
        "WARRIOR_TREE",
        "WARRIOR",
        59,
        1,
        [new TalentBranchDefinition("GUARDIAN", "Страж", "Защита", 2)],
        [
            new TalentDefinition("G-1-1", "GUARDIAN", 1, 0, "Стойкость", "Endurance", 2, [], ""),
            new TalentDefinition("G-2-1", "GUARDIAN", 2, 1, "Бастион", "Bastion", 1,
                [new TalentPrerequisite("G-1-1", 2)], "")
        ]);

    [Fact]
    public void LearnIncreasesRankWhenPointsTierAndPrerequisiteAreSatisfied()
    {
        IReadOnlyDictionary<string, int> selected = new Dictionary<string, int>
        {
            ["G-1-1"] = 2
        };

        TalentLearnResult result = TalentRules.TryLearn(Tree, 4, selected, "G-2-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.SelectedRanks["G-2-1"]);
        Assert.Equal(0, result.AvailablePoints);
    }

    [Fact]
    public void LearnRejectsMissingRequiredPrerequisiteRankWithoutMutation()
    {
        IReadOnlyDictionary<string, int> selected = new Dictionary<string, int>
        {
            ["G-1-1"] = 1
        };

        TalentLearnResult result = TalentRules.TryLearn(Tree, 4, selected, "G-2-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(TalentErrorCodes.PrerequisiteMissing, result.ErrorCode);
        Assert.DoesNotContain("G-2-1", result.SelectedRanks);
    }

    [Fact]
    public void LearnRejectsRankAboveMaximum()
    {
        IReadOnlyDictionary<string, int> selected = new Dictionary<string, int>
        {
            ["G-1-1"] = 2
        };

        TalentLearnResult result = TalentRules.TryLearn(Tree, 10, selected, "G-1-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(TalentErrorCodes.MaxRank, result.ErrorCode);
    }

    [Fact]
    public void ValidateBuildRejectsUnknownTalentAndOverspentRanks()
    {
        IReadOnlyDictionary<string, int> selected = new Dictionary<string, int>
        {
            ["UNKNOWN"] = 1,
            ["G-1-1"] = 3
        };

        IReadOnlyList<string> errors = TalentRules.ValidateBuild(Tree, 60, selected);

        Assert.Contains(TalentErrorCodes.UnknownTalent, errors);
        Assert.Contains(TalentErrorCodes.InvalidRank, errors);
    }

    [Fact]
    public void ResolvePrimaryStatPercentagesUsesTheSelectedRankValue()
    {
        TalentTreeDefinition tree = Tree with
        {
            Nodes =
            [
                Tree.Nodes[0] with
                {
                    Modifiers = [new TalentModifierDefinition(
                        TalentModifierType.StatModifier, "STAMINA_PERCENT", [2, 4])]
                }
            ]
        };

        TalentPrimaryStatPercentages result = TalentStatModifierResolver.ResolvePrimaryPercentages(
            tree, new Dictionary<string, int> { ["G-1-1"] = 2 });

        Assert.Equal(4, result.Stamina);
    }
}
