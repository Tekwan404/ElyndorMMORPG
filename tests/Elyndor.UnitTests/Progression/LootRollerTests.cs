using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Progression;

public sealed class LootRollerTests
{
    [Fact]
    public void WeightedExclusiveGroupProducesExactlyOneItem()
    {
        LootTableDefinition table = new(
            "BOSS",
            [],
            SelectionGroups:
            [
                new LootSelectionGroup(
                    "SET",
                    1,
                    "WeightedExclusive",
                    [
                        new LootSelectionEntry("COMMON_SET", 1m),
                        new LootSelectionEntry("RARE_SET", 9m)
                    ])
            ]);

        IReadOnlyList<LootRoll> result = LootRoller.Roll(table, new SequenceGameRandom(0.5m));

        Assert.Equal([new LootRoll("RARE_SET", 1)], result);
    }

    [Fact]
    public void EqualWeightGroupIgnoresSourceWeightsAndUsesOneRollPerConfiguredRoll()
    {
        LootTableDefinition table = new(
            "BOSS",
            [],
            SelectionGroups:
            [
                new LootSelectionGroup(
                    "SET",
                    2,
                    "EqualWeight",
                    [
                        new LootSelectionEntry("FIRST", 5m),
                        new LootSelectionEntry("SECOND", 1m)
                    ])
            ]);

        IReadOnlyList<LootRoll> result = LootRoller.Roll(table, new SequenceGameRandom(0.1m, 0.9m));

        Assert.Equal([new LootRoll("FIRST", 1), new LootRoll("SECOND", 1)], result);
    }

    [Fact]
    public void IndependentDropsRemainIndependentAlongsideExclusiveGroups()
    {
        LootTableDefinition table = new(
            "BOSS",
            [new LootTableEntry("MATERIAL", 1m, 2, 2)],
            SelectionGroups:
            [
                new LootSelectionGroup("SET", 1, "EqualWeight", [new LootSelectionEntry("SET_ITEM", 1m)])
            ]);

        IReadOnlyList<LootRoll> result = LootRoller.Roll(table, new SequenceGameRandom(0m, 0.99m));

        Assert.Equal([new LootRoll("MATERIAL", 2), new LootRoll("SET_ITEM", 1)], result);
    }
}
