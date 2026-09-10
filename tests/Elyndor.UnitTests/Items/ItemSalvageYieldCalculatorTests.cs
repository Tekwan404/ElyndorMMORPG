using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class ItemSalvageYieldCalculatorTests
{
    [Fact]
    public void CalculateUsesRarityLevelAndStarsForGeneratedEquipment()
    {
        ItemDefinition item = Equipment(ItemRarity.Epic, requiredLevel: 14);
        ItemSalvageProfileDefinition profile = Profile();

        ItemSalvageYield yield = ItemSalvageYieldCalculator.Calculate(
            item,
            itemLevel: 14,
            stars: 5,
            profile);

        Assert.Equal(9, yield.ReforgeStoneQuantity);
        Assert.Equal(12, yield.MaterialQuantity);
    }

    [Fact]
    public void CalculateUsesRequiredLevelAndNoStarBonusForLegacyEquipment()
    {
        ItemDefinition item = Equipment(ItemRarity.Common, requiredLevel: 4);
        ItemSalvageProfileDefinition profile = Profile();

        ItemSalvageYield yield = ItemSalvageYieldCalculator.Calculate(
            item,
            itemLevel: null,
            stars: null,
            profile);

        Assert.Equal(1, yield.ReforgeStoneQuantity);
        Assert.Equal(2, yield.MaterialQuantity);
    }

    [Fact]
    public void CalculateRejectsNonEquipment()
    {
        ItemDefinition material = new(
            "TEST_MATERIAL",
            "Test material",
            ItemType.Material,
            ItemRarity.Common,
            1,
            true,
            99,
            null,
            new PrimaryStats(0, 0, 0, 0),
            "Test material.");

        Assert.Throws<InvalidOperationException>(() =>
            ItemSalvageYieldCalculator.Calculate(material, 1, null, Profile()));
    }

    private static ItemDefinition Equipment(ItemRarity rarity, int requiredLevel) => new(
        "TEST_EQUIPMENT",
        "Test equipment",
        ItemType.Equipment,
        rarity,
        requiredLevel,
        false,
        1,
        EquipmentSlot.Chest,
        new PrimaryStats(0, 0, 0, 0),
        "Test equipment.",
        ArmorCategory: EquipmentCategoryIds.Heavy);

    private static ItemSalvageProfileDefinition Profile() => new(
        "SALVAGE_V1",
        "REFORGE_STONE",
        "FORGE_SCRAP",
        new Dictionary<string, int>
        {
            ["COMMON"] = 1,
            ["UNCOMMON"] = 2,
            ["RARE"] = 3,
            ["EPIC"] = 5,
            ["LEGENDARY"] = 8,
            ["UNIQUE"] = 10,
        },
        new Dictionary<string, int>
        {
            ["COMMON"] = 2,
            ["UNCOMMON"] = 3,
            ["RARE"] = 5,
            ["EPIC"] = 8,
            ["LEGENDARY"] = 12,
            ["UNIQUE"] = 16,
        },
        ItemLevelStep: 5,
        ReforgeStonesPerLevelStep: 1,
        MaterialsPerLevelStep: 2,
        StarsPerBonusStone: 2,
        HighValueConfirmationRarity: "RARE");
}
