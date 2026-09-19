using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Items;

namespace Elyndor.UnitTests.Items;

public sealed class EquippedSetPieceCounterTests
{
    private const string GuardianSetId = "SET_ANCIENT_MINE_WARRIOR_GUARDIAN";
    private const string BerserkerSetId = "SET_ANCIENT_MINE_WARRIOR_BERSERKER";

    [Fact]
    public void CountsOnePieceForEachEquippedSetItem()
    {
        InventoryItemSnapshot chest = Item(EquipmentSlot.Chest, GuardianSetId);
        InventoryItemSnapshot legs = Item(EquipmentSlot.Legs, GuardianSetId);
        InventoryItemSnapshot boots = Item(EquipmentSlot.Feet, BerserkerSetId);

        IReadOnlyDictionary<string, int> counts = EquippedSetPieceCounter.Count(
            new InventorySnapshot(
                [chest, legs, boots],
                new Dictionary<EquipmentSlot, InventoryItemSnapshot>
                {
                    [EquipmentSlot.Chest] = chest,
                    [EquipmentSlot.Legs] = legs,
                    [EquipmentSlot.Feet] = boots
                }));

        Assert.Equal(2, counts[GuardianSetId]);
        Assert.Equal(1, counts[BerserkerSetId]);
    }

    [Fact]
    public void CountsATwoHandedItemOccupyingTwoSlotsOnlyOnce()
    {
        InventoryItemSnapshot twoHander = Item(EquipmentSlot.MainHand, GuardianSetId);

        IReadOnlyDictionary<string, int> counts = EquippedSetPieceCounter.Count(
            new InventorySnapshot(
                [twoHander],
                new Dictionary<EquipmentSlot, InventoryItemSnapshot>
                {
                    [EquipmentSlot.MainHand] = twoHander,
                    [EquipmentSlot.OffHand] = twoHander
                }));

        Assert.Equal(1, counts[GuardianSetId]);
    }

    [Fact]
    public void IgnoresCarriedItemsAndItemsWithoutASet()
    {
        InventoryItemSnapshot equipped = Item(EquipmentSlot.Chest, setId: null);
        InventoryItemSnapshot carried = Item(EquipmentSlot.Chest, GuardianSetId, equipped: false);

        IReadOnlyDictionary<string, int> counts = EquippedSetPieceCounter.Count(
            new InventorySnapshot(
                [equipped, carried],
                new Dictionary<EquipmentSlot, InventoryItemSnapshot>
                {
                    [EquipmentSlot.Chest] = equipped
                }));

        Assert.Empty(counts);
    }

    private static InventoryItemSnapshot Item(
        EquipmentSlot slot,
        string? setId,
        bool equipped = true) => new(
        Guid.NewGuid(),
        new ItemDefinition(
            $"ITEM_{slot}_{setId ?? "NONE"}",
            "Test item",
            ItemType.Equipment,
            ItemRarity.Rare,
            1,
            false,
            1,
            slot,
            new PrimaryStats(0, 0, 0, 0),
            "Test item",
            SetId: setId),
        1,
        DateTimeOffset.UnixEpoch,
        equipped ? slot : null,
        false);
}
