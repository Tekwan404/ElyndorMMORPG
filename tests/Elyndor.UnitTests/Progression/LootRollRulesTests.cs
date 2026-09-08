using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Progression;

public sealed class LootRollRulesTests
{
    [Fact]
    public void NeedIsAllowedOnlyForUsableEquipment()
    {
        ItemDefinition sword = new(
            "EPIC_SWORD",
            "Epic Sword",
            ItemType.Equipment,
            ItemRarity.Epic,
            10,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(0, 0, 0, 0),
            "A sword",
            WeaponCategory: EquipmentCategoryIds.TwoHandSword);
        ClassProfile warrior = Profile("WARRIOR", [EquipmentCategoryIds.TwoHandSword]);
        ClassProfile mage = Profile("MAGE", [EquipmentCategoryIds.Staff]);

        Assert.True(LootRollRules.CanNeed(sword, "WARRIOR", 10, warrior, false));
        Assert.False(LootRollRules.CanNeed(sword, "MAGE", 10, mage, false));
        Assert.False(LootRollRules.CanNeed(sword, "WARRIOR", 9, warrior, false));
    }

    [Fact]
    public void NeedHasPriorityOverGreedAndPass()
    {
        Guid need = Guid.Parse("81000000-0000-0000-0000-000000000001");
        Guid greed = Guid.Parse("82000000-0000-0000-0000-000000000001");
        LootRollResolution resolution = LootRollRules.Resolve(
            new Dictionary<Guid, LootChoice>
            {
                [need] = LootChoice.Need,
                [greed] = LootChoice.Greed,
                [Guid.Parse("83000000-0000-0000-0000-000000000001")] = LootChoice.Pass
            },
            new SequenceGameRandom(0.1m, 0.99m));

        Assert.Equal(need, resolution.WinnerCharacterId);
        Assert.Equal(2, resolution.Entries.Count(entry => entry.Roll > 0));
    }

    [Fact]
    public void AllPassLeavesValuableItemUnassigned()
    {
        LootRollResolution resolution = LootRollRules.Resolve(
            new Dictionary<Guid, LootChoice>
            {
                [Guid.NewGuid()] = LootChoice.Pass,
                [Guid.NewGuid()] = LootChoice.Pass
            },
            new SequenceGameRandom());

        Assert.Null(resolution.WinnerCharacterId);
        Assert.All(resolution.Entries, entry => Assert.Equal(0, entry.Roll));
    }

    private static ClassProfile Profile(
        string id,
        IReadOnlyList<string> weapons) =>
        new(
            id,
            "STRENGTH",
            "RAGE",
            new PrimaryStats(1, 1, 1, 1),
            new PrimaryStats(1, 1, 1, 1),
            weapons,
            [],
            id);
}
