using System.Text.Json;
using Elyndor.Contracts.Items;

namespace Elyndor.UnitTests.Items;

public sealed class InventoryItemResponseBlockTests
{
    [Fact]
    public void ShieldBlockProfileRoundTripsWithReadableTooltip()
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

        Assert.Contains("Шанс блока: 4%", response.Description);
        Assert.Contains("Сила блока: 28–42", response.Description);

        string json = JsonSerializer.Serialize(response);
        InventoryItemResponse restored = JsonSerializer.Deserialize<InventoryItemResponse>(json)!;

        Assert.Equal(response.Description, restored.Description);
        Assert.Equal(4m, restored.BlockChancePercent);
        Assert.Equal(28m, restored.BlockValueMin);
        Assert.Equal(42m, restored.BlockValueMax);
    }

    [Fact]
    public void NonShieldKeepsZeroBlockProfileAndOriginalDescription()
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

        Assert.Equal("Обычный меч.", response.Description);
        Assert.Equal(0m, response.BlockChancePercent);
        Assert.Equal(0m, response.BlockValueMin);
        Assert.Equal(0m, response.BlockValueMax);
    }
}
