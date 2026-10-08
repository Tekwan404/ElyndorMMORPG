using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class ItemAffixEligibilityTests
{
    [Fact]
    public void ConcreteItemLevelSelectsSharedWeightBand()
    {
        var pool = new ItemAffixPoolDefinition("TEST", ["STRENGTH", "STAMINA"],
            LevelSelectionWeights: [new(12, 29, new Dictionary<string, decimal> { ["STRENGTH"] = 3 })]);
        Assert.Equal("STRENGTH", ItemAffixEligibilityPolicy.SelectWeighted(pool, pool.StatIds, new SequenceGameRandom(0.5m), 20));
        Assert.Equal("STAMINA", ItemAffixEligibilityPolicy.SelectWeighted(pool, pool.StatIds, new SequenceGameRandom(0.5m), 60));
    }
    [Fact]
    public void SelectionRespectsWeightsAndExclusiveGroups()
    {
        var pool = new ItemAffixPoolDefinition("GENERAL", ["STRENGTH", "INTELLECT", "STAMINA"],
            new Dictionary<string, decimal> { ["STRENGTH"] = 3, ["INTELLECT"] = 1, ["STAMINA"] = 1 },
            [["STRENGTH", "INTELLECT"]]);
        Assert.Equal("STRENGTH", ItemAffixEligibilityPolicy.SelectWeighted(pool, pool.StatIds, new SequenceGameRandom(0.5m)));
        Assert.False(ItemAffixEligibilityPolicy.IsCompatible(pool, "INTELLECT", ["STRENGTH"]));
        Assert.True(ItemAffixEligibilityPolicy.IsCompatible(pool, "STAMINA", ["STRENGTH"]));
    }

    [Fact]
    public void EligibilityRejectsWrongClassAndShieldStatsWithoutShield()
    {
        ItemDefinition mage = new("MAGE", "Mage", ItemType.Equipment, ItemRarity.Rare, 20, false, 1,
            EquipmentSlot.Head, new PrimaryStats(0, 0, 0, 0), "", GenerationVersion: 2, AllowedClassIds: ["MAGE"]);
        ItemAffixRuleDefinition strength = new("STRENGTH", ["WARRIOR", "PALADIN"]);
        ItemAffixRuleDefinition block = new("BLOCK_VALUE", AllowedSlots: [EquipmentSlot.OffHand], RequiresShield: true);
        Assert.False(ItemAffixEligibilityPolicy.IsAllowed(mage, strength));
        Assert.False(ItemAffixEligibilityPolicy.IsAllowed(mage with { Slot = EquipmentSlot.OffHand, OffHandCategory = "FOCUS" }, block));
        Assert.True(ItemAffixEligibilityPolicy.IsAllowed(mage with { Slot = EquipmentSlot.OffHand, OffHandCategory = "SHIELD" }, block));
    }

    [Fact]
    public void CapacityChecksEveryReachableSelectionRatherThanOnlyPoolSize()
    {
        var pool = new ItemAffixPoolDefinition("TRAP", ["STRENGTH", "INTELLECT", "STAMINA"],
            ExclusiveStatGroups: [["STRENGTH", "INTELLECT"]]);
        Assert.False(ItemAffixEligibilityPolicy.CanAlwaysFill(pool, pool.StatIds, [], 3));
        Assert.True(ItemAffixEligibilityPolicy.CanAlwaysFill(pool, pool.StatIds, [], 2));
    }

    [Fact]
    public void OverlappingGroupsRejectAChoiceThatBlocksBothRemainingStats()
    {
        var pool = new ItemAffixPoolDefinition("OVERLAPPING", ["STRENGTH", "INTELLECT", "STAMINA"],
            ExclusiveStatGroups: [["STRENGTH", "INTELLECT"], ["INTELLECT", "STAMINA"]]);
        Assert.False(ItemAffixEligibilityPolicy.CanAlwaysFill(pool, pool.StatIds, [], 2));
        Assert.True(ItemAffixEligibilityPolicy.CanAlwaysFill(pool, pool.StatIds, [], 1));
        Assert.True(ItemAffixEligibilityPolicy.CanAlwaysFill(pool, pool.StatIds, ["STRENGTH"], 1));
    }
}
