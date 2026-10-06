using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class ItemFamilyFoundationTests
{
    [Fact]
    public void CanonicalSlotPolicyKeepsTwelveSlotsAndMapsOnlyTransitionalAliases()
    {
        Assert.True(EquipmentSlotPolicy.IsCanonical(EquipmentSlot.MainHand));
        Assert.True(EquipmentSlotPolicy.IsCanonical(EquipmentSlot.Ring2));
        Assert.False(EquipmentSlotPolicy.IsCanonical(EquipmentSlot.Weapon));
        Assert.False(EquipmentSlotPolicy.IsCanonical(EquipmentSlot.Waist));
        Assert.False(EquipmentSlotPolicy.IsCanonical(EquipmentSlot.Wrist));

        Assert.Equal(EquipmentSlot.MainHand, EquipmentSlotPolicy.Canonicalize(EquipmentSlot.Weapon));
        Assert.Equal(EquipmentSlot.Feet, EquipmentSlotPolicy.Canonicalize(EquipmentSlot.Boots));
        Assert.Equal(EquipmentSlot.Amulet, EquipmentSlotPolicy.Canonicalize(EquipmentSlot.Accessory));
    }

    [Fact]
    public void FamilyRequiredLevelComesFromInstanceItemLevelAndCapsAtCharacterCap()
    {
        ItemDefinition family = FamilySword();
        ItemDefinition legacy = family with { ItemFamilyId = null, RequiredLevel = 7 };

        Assert.Equal(42, ItemRequiredLevelPolicy.Resolve(family, 42, 60));
        Assert.Equal(60, ItemRequiredLevelPolicy.Resolve(family, 72, 60));
        Assert.Equal(7, ItemRequiredLevelPolicy.Resolve(legacy, 42, 60));
    }

    [Fact]
    public void LootRollCarriesSourceItemLevelRange()
    {
        LootTableDefinition table = new(
            "FAMILY_DROP",
            [new LootTableEntry("TEST_FAMILY_SWORD", 1m, 1, 1, 44, 48)]);

        LootRoll roll = Assert.Single(LootRoller.Roll(
            table,
            new SequenceGameRandom(0m)));

        Assert.Equal("TEST_FAMILY_SWORD", roll.ItemId);
        Assert.Equal(44, roll.ItemLevelMin);
        Assert.Equal(48, roll.ItemLevelMax);
    }

    [Fact]
    public void SourceOverridePinsGeneratedFamilyItemLevel()
    {
        ItemDefinition family = FamilySword();
        GeneratedItemInstance generated = ItemInstanceGenerator.Generate(
            family,
            TestItemization(),
            "TEST",
            new SeededGameRandom(42),
            overrides: new ItemGenerationOverrides(48, 48));

        Assert.Equal(48, generated.ItemLevel);
    }

    [Fact]
    public void SourceOverrideCannotEscapeTemplateItemLevelRange()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            ItemInstanceGenerator.Generate(
                FamilySword(),
                TestItemization(),
                "TEST",
                new SeededGameRandom(42),
                overrides: new ItemGenerationOverrides(61, 61)));

        Assert.Contains("outside template range", error.Message);
    }

    private static ItemDefinition FamilySword() => new(
        "TEST_FAMILY_SWORD",
        "Family sword",
        ItemType.Equipment,
        ItemRarity.Rare,
        1,
        false,
        1,
        EquipmentSlot.MainHand,
        new PrimaryStats(0, 0, 0, 0),
        "Family item foundation test.",
        WeaponCategory: EquipmentCategoryIds.OneHandSword,
        ItemLevelMin: 1,
        ItemLevelMax: 60,
        GuaranteedAffixStatIds: [ItemStatIds.Strength],
        RandomAffixPoolId: "TEST_POOL",
        AffixCountProfileId: "TEST_COUNT",
        GenerationMode: ItemGenerationMode.Rolled,
        ItemFamilyId: "TEST_FAMILY_SWORD");

    private static ItemizationDefinition TestItemization() => new(
        TemplateBasePower: 110m,
        LevelLinearCoefficient: 0.055m,
        LevelQuadraticCoefficient: 0.0035m,
        SlotMultipliers: new Dictionary<string, decimal>
        {
            ["MAIN_HAND"] = 1.15m
        },
        RarityMultipliers: new Dictionary<string, decimal>
        {
            ["RARE"] = 1.55m
        },
        StatPowerWeights: new Dictionary<string, decimal>
        {
            [ItemStatIds.Strength] = 8m
        },
        AffixPools:
        [
            new ItemAffixPoolDefinition("TEST_POOL", [ItemStatIds.Strength])
        ],
        AffixCountProfiles:
        [
            new ItemAffixCountProfileDefinition("TEST_COUNT", 1, 0, 0)
        ],
        QualityProfiles:
        [
            new ItemQualityProfileDefinition("TEST", 1m)
        ],
        AffixNames: [],
        IndividualQualityDeviationPercent: 0m);
}
