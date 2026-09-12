using Elyndor.Contracts.Items;

namespace Elyndor.UnitTests.Items;

public sealed class InventoryItemResponseBlockTests
{
    [Fact]
    public void ShieldDescriptionExplainsChanceAndAbsorbedDamageRange()
    {
        InventoryItemResponse response = new(
            Guid.CreateVersion7(),
            "DUNGEON_MINES_WARRIOR_LEGENDARY_SHIELD",
            "Оплот Хранителя Глубин",
            "Equipment",
            "Legendary",
            16,
            1,
            "OffHand",
            null,
            new ItemStatsResponse(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128, 0, 0, 0, 0, 0, 0),
            "Массивный щит Хранителя Глубин.",
            null,
            null,
            null,
            ["WARRIOR"],
            null,
            0,
            0,
            [],
            null,
            0,
            0,
            0,
            false,
            BlockChancePercent: 4m,
            BlockValueMin: 28m,
            BlockValueMax: 42m);

        Assert.Contains("Блок щитом: шанс 4%", response.ClientDescription);
        Assert.Contains("28–42 входящего урона", response.ClientDescription);
    }

    [Fact]
    public void NonShieldDescriptionIsNotModified()
    {
        InventoryItemResponse response = new(
            Guid.CreateVersion7(),
            "SWORD",
            "Меч",
            "Equipment",
            "Common",
            1,
            1,
            "MainHand",
            null,
            new ItemStatsResponse(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            "Обычный меч.",
            null,
            "ONE_HAND_SWORD",
            null,
            ["WARRIOR"],
            2m,
            0,
            0,
            [],
            null,
            0,
            0,
            0,
            false);

        Assert.Equal("Обычный меч.", response.ClientDescription);
    }
}
