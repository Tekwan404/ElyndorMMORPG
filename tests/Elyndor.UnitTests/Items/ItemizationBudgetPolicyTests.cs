using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class ItemizationBudgetPolicyTests
{
    [Fact]
    public void SnapshotNormalizesOnlyUnderfundedTemplateBudget()
    {
        ItemDefinition shield = UnderfundedShield();
        ItemDefinition healthySword = HealthySword();
        ItemizationDefinition itemization = TestItemization();
        GameContentPackage package = Package([shield, healthySword], itemization);

        GameContentSnapshot snapshot = GameContentSnapshot.Create(package);

        ItemDefinition normalizedShield = snapshot.Indexes.ItemsById[shield.Id];
        ItemDefinition normalizedSword = snapshot.Indexes.ItemsById[healthySword.Id];

        Assert.True(normalizedShield.ExtraAffixBudgetCap > shield.ExtraAffixBudgetCap);
        Assert.Equal(healthySword.ExtraAffixBudgetCap, normalizedSword.ExtraAffixBudgetCap);
        Assert.Equal(itemization.SlotMultipliers, snapshot.Package.Itemization!.SlotMultipliers);
        Assert.Equal(587.50m, decimal.Round(
            ItemInstanceGenerator.CalculateTemplateMaxPower(
                normalizedShield,
                snapshot.Package.Itemization,
                16),
            2,
            MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void NormalizedShieldGenerationHasRealAffixRangesAndCannotOverflowPower()
    {
        ItemDefinition shield = UnderfundedShield();
        GameContentSnapshot snapshot = GameContentSnapshot.Create(Package([shield], TestItemization()));
        ItemDefinition normalizedShield = snapshot.Indexes.ItemsById[shield.Id];

        GeneratedItemInstance generated = ItemInstanceGenerator.Generate(
            normalizedShield,
            snapshot.Package.Itemization!,
            "TEST",
            new SequenceGameRandom(
                0m, 0m, // choose both bonus affixes
                0m,     // lowest shared quality
                0.5m, 0.5m, 0.5m, 0.5m));

        Assert.Equal(4, generated.Affixes.Count);
        Assert.All(generated.Affixes, affix =>
            Assert.True(
                affix.MaxAtGeneration > affix.MinAtGeneration,
                $"{affix.StatId} collapsed to {affix.MinAtGeneration}..{affix.MaxAtGeneration}"));
        Assert.True(generated.ActualItemPower < generated.MaxTemplateItemPower);
        Assert.True(generated.RollQuality < 100m);
        Assert.True(generated.Stars < 5);
        Assert.False(generated.IsPerfect);
    }

    [Fact]
    public void HistoricalCollapsedAffixesLoseFalsePerfectQualityAndHighName()
    {
        ItemDefinition shield = UnderfundedShield();
        ItemizationDefinition itemization = TestItemization();
        GeneratedItemAffix[] historicalAffixes =
        [
            LegacyAffix("AFFIX_1", ItemStatIds.Strength, true, 0),
            LegacyAffix("AFFIX_2", ItemStatIds.Stamina, true, 1),
            LegacyAffix("AFFIX_3", ItemStatIds.MagicResistance, false, 2),
            LegacyAffix("AFFIX_4", ItemStatIds.BlockValue, false, 3)
        ];

        GeneratedItemInstance recalculated = ItemizationBudgetPolicy.RecalculateStored(
            shield,
            itemization,
            16,
            historicalAffixes,
            perfectOrigin: "DROP");

        Assert.Equal(587.50m, recalculated.MaxTemplateItemPower);
        Assert.Equal(514.20m, recalculated.ActualItemPower);
        Assert.True(recalculated.RollQuality < 10m);
        Assert.Equal(1, recalculated.Stars);
        Assert.False(recalculated.IsPerfect);
        Assert.Null(recalculated.PerfectOrigin);
        Assert.Null(recalculated.GeneratedPrefixId);
        Assert.Null(recalculated.GeneratedSuffixId);
        Assert.Equal(shield.Name, recalculated.DisplayName);
    }

    private static GameContentPackage Package(
        IReadOnlyList<ItemDefinition> items,
        ItemizationDefinition itemization) =>
        new(
            "test",
            "test",
            DateTimeOffset.UnixEpoch,
            [],
            [],
            Items: items,
            Itemization: itemization);

    private static ItemDefinition UnderfundedShield() => new(
        "TEST_UNDERFUNDED_SHIELD",
        "Оплот Хранителя Глубин",
        ItemType.Equipment,
        ItemRarity.Legendary,
        16,
        false,
        1,
        EquipmentSlot.OffHand,
        new PrimaryStats(0, 0, 0, 0),
        "Regression shield.",
        ArmorFlat: 230,
        MagicResistanceFlat: 0,
        OffHandCategory: EquipmentCategoryIds.Shield,
        BlockChancePercent: 9,
        BlockValueMin: 45,
        BlockValueMax: 65,
        ItemLevelMin: 16,
        ItemLevelMax: 16,
        GuaranteedAffixStatIds: [ItemStatIds.Strength, ItemStatIds.Stamina],
        RandomAffixPoolId: "WARRIOR_SHIELD",
        AffixCountProfileId: "LEVEL_13_16_TEST",
        ExtraAffixBudgetCap: 0.08m,
        PrefixSuffixPolicyId: "WORLD_V1");

    private static ItemDefinition HealthySword() => new(
        "TEST_HEALTHY_SWORD",
        "Healthy sword",
        ItemType.Equipment,
        ItemRarity.Rare,
        10,
        false,
        1,
        EquipmentSlot.MainHand,
        new PrimaryStats(0, 0, 0, 0),
        "Healthy budget control.",
        WeaponCategory: EquipmentCategoryIds.OneHandSword,
        ItemLevelMin: 10,
        ItemLevelMax: 10,
        GuaranteedAffixStatIds: [ItemStatIds.Strength],
        RandomAffixPoolId: "SWORD_POOL",
        AffixCountProfileId: "SWORD_COUNT",
        ExtraAffixBudgetCap: 0m);

    private static GeneratedItemAffix LegacyAffix(
        string slotKey,
        string statId,
        bool guaranteed,
        int ordinal) =>
        new(
            slotKey,
            statId,
            statId,
            1m,
            1m,
            1m,
            1m,
            3,
            guaranteed,
            false,
            ordinal);

    private static ItemizationDefinition TestItemization() => new(
        TemplateBasePower: 110m,
        LevelLinearCoefficient: 0.055m,
        LevelQuadraticCoefficient: 0.0035m,
        SlotMultipliers: new Dictionary<string, decimal>
        {
            ["MAIN_HAND"] = 1.15m,
            ["OFF_HAND"] = 0.70m
        },
        RarityMultipliers: new Dictionary<string, decimal>
        {
            ["RARE"] = 1.55m,
            ["LEGENDARY"] = 2.30m
        },
        StatPowerWeights: new Dictionary<string, decimal>
        {
            [ItemStatIds.Strength] = 8m,
            [ItemStatIds.Stamina] = 12m,
            [ItemStatIds.Armor] = 0.8m,
            [ItemStatIds.MagicResistance] = 1.2m,
            [ItemStatIds.BlockChance] = 25m,
            [ItemStatIds.BlockValue] = 1.5m
        },
        AffixPools:
        [
            new ItemAffixPoolDefinition(
                "WARRIOR_SHIELD",
                [
                    ItemStatIds.Strength,
                    ItemStatIds.Stamina,
                    ItemStatIds.MagicResistance,
                    ItemStatIds.BlockValue
                ]),
            new ItemAffixPoolDefinition("SWORD_POOL", [ItemStatIds.Strength])
        ],
        AffixCountProfiles:
        [
            new ItemAffixCountProfileDefinition("LEVEL_13_16_TEST", 2, 2, 2),
            new ItemAffixCountProfileDefinition("SWORD_COUNT", 1, 0, 0)
        ],
        QualityProfiles: [new ItemQualityProfileDefinition("TEST", 1m)],
        AffixNames:
        [
            new ItemAffixNameDefinition(
                "PREFIX_STRENGTH",
                ItemStatIds.Strength,
                "PREFIX",
                "Крепкий",
                "Могучий",
                "Титанический")
        ],
        IndividualQualityDeviationPercent: 0m);
}
