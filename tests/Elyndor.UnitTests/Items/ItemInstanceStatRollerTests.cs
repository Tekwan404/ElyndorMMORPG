using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class ItemInstanceStatRollerTests
{
    [Fact]
    public void ResolveRollsConfiguredRangesAndKeepsStaticFallbacks()
    {
        ItemDefinition definition = new(
            "TEST_ROLLING_SWORD",
            "Rolling Sword",
            ItemType.Equipment,
            ItemRarity.Rare,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(1, 2, 7, 4),
            "Test rolling weapon.",
            WeaponCategory: EquipmentCategoryIds.OneHandSword,
            PrimaryStatRanges: new PrimaryStatRanges(
                Strength: new ItemStatRange(2, 4),
                Agility: new ItemStatRange(10, 12),
                Stamina: new ItemStatRange(4, 8, 2)));

        PrimaryStats rolled = ItemInstanceStatRoller.Resolve(
            definition,
            new SequenceGameRandom(0m, 0.999m, 0.5m));

        Assert.Equal(2, rolled.Strength);
        Assert.Equal(12, rolled.Agility);
        Assert.Equal(7, rolled.Intellect);
        Assert.Equal(6, rolled.Stamina);
    }

    [Fact]
    public void GeneratedWeaponDamageAffixChangesEffectiveWeaponDamageRange()
    {
        ItemDefinition definition = new(
            "TEST_PROCEDURAL_BOW",
            "Procedural Bow",
            ItemType.Equipment,
            ItemRarity.Rare,
            10,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(0, 0, 0, 0),
            "Procedural weapon.",
            WeaponCategory: EquipmentCategoryIds.Bow,
            WeaponDamageMin: 20,
            WeaponDamageMax: 30);

        ItemDefinition effective = ItemInstanceGenerator.ApplyGeneratedAffixes(
            definition,
            [
                new GeneratedItemAffix(
                    "BONUS_0",
                    "WEAPON_DAMAGE",
                    ItemStatIds.WeaponDamage,
                    5,
                    1,
                    10,
                    1,
                    1,
                    false,
                    false,
                    0)
            ]);

        Assert.Equal(25, effective.WeaponDamageMin);
        Assert.Equal(35, effective.WeaponDamageMax);
    }

    [Fact]
    public void ResolveKeepsLegacyStaticStatsWhenNoRangesExist()
    {
        ItemDefinition definition = new(
            "TEST_STATIC_STAFF",
            "Static Staff",
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(0, 0, 5, 2),
            "Legacy compatible static item.",
            WeaponCategory: EquipmentCategoryIds.Staff);

        PrimaryStats resolved = ItemInstanceStatRoller.Resolve(
            definition,
            new SequenceGameRandom());

        Assert.Equal(definition.Stats, resolved);
    }

    [Fact]
    public void GeneratedItemQualityIsDeterministicAndDoesNotChangeTemplateRarity()
    {
        ItemDefinition epicTemplate = ProceduralTemplate(ItemRarity.Epic);
        ItemizationDefinition itemization = TestItemization();

        GeneratedItemInstance first = ItemInstanceGenerator.Generate(
            epicTemplate,
            itemization,
            "TEST",
            new SequenceGameRandom(0.60m, 0.50m));
        GeneratedItemInstance second = ItemInstanceGenerator.Generate(
            epicTemplate,
            itemization,
            "TEST",
            new SequenceGameRandom(0.60m, 0.50m));
        GeneratedItemInstance rare = ItemInstanceGenerator.Generate(
            ProceduralTemplate(ItemRarity.Rare),
            itemization,
            "TEST",
            new SequenceGameRandom(0.60m, 0.50m));

        Assert.Equal(first.ActualItemPower, second.ActualItemPower);
        Assert.Equal(first.MaxTemplateItemPower, second.MaxTemplateItemPower);
        Assert.Equal(first.RollQuality, second.RollQuality);
        Assert.Equal(first.Stars, second.Stars);
        Assert.Equal(rare.Stars, first.Stars);
        Assert.NotEqual(rare.ActualItemPower, first.ActualItemPower);
        Assert.Equal(ItemRarity.Epic, epicTemplate.Rarity);
    }

    [Fact]
    public void GeneratedItemQualityMapsLowestAndPerfectRollsToOneAndFiveStars()
    {
        ItemDefinition template = ProceduralTemplate(ItemRarity.Rare);
        ItemizationDefinition itemization = TestItemization();

        GeneratedItemInstance lowest = ItemInstanceGenerator.Generate(
            template,
            itemization,
            "TEST",
            new SequenceGameRandom(0m, 0.50m));
        GeneratedItemInstance perfect = ItemInstanceGenerator.Generate(
            template,
            itemization,
            "TEST",
            new SequenceGameRandom(0.9999m, 0.50m));

        Assert.Equal(1, lowest.Stars);
        Assert.False(lowest.IsPerfect);
        Assert.Equal(5, perfect.Stars);
        Assert.True(perfect.IsPerfect);
        Assert.Equal("DROP", perfect.PerfectOrigin);
    }

    private static ItemDefinition ProceduralTemplate(ItemRarity rarity) => new(
        "TEST_PROCEDURAL_SWORD",
        "Test procedural sword",
        ItemType.Equipment,
        rarity,
        10,
        false,
        1,
        EquipmentSlot.MainHand,
        new PrimaryStats(0, 0, 0, 0),
        "Test procedural equipment.",
        GuaranteedAffixStatIds: [ItemStatIds.Strength],
        RandomAffixPoolId: "TEST_POOL",
        AffixCountProfileId: "TEST_COUNT");

    private static ItemizationDefinition TestItemization() => new(
        TemplateBasePower: 100,
        LevelLinearCoefficient: 0,
        LevelQuadraticCoefficient: 0,
        SlotMultipliers: new Dictionary<string, decimal> { ["MAIN_HAND"] = 1m },
        RarityMultipliers: new Dictionary<string, decimal>
        {
            ["COMMON"] = 1m,
            ["UNCOMMON"] = 1m,
            ["RARE"] = 1.55m,
            ["EPIC"] = 1.90m,
            ["LEGENDARY"] = 1m,
            ["UNIQUE"] = 1m,
        },
        StatPowerWeights: new Dictionary<string, decimal> { [ItemStatIds.Strength] = 1m },
        AffixPools: [new ItemAffixPoolDefinition("TEST_POOL", [ItemStatIds.Strength])],
        AffixCountProfiles: [new ItemAffixCountProfileDefinition("TEST_COUNT", 1, 0, 0)],
        QualityProfiles: [new ItemQualityProfileDefinition("TEST", 1m)],
        AffixNames: [],
        IndividualQualityDeviationPercent: 0);
}
