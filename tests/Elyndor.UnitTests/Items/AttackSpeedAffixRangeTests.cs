using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class AttackSpeedAffixRangeTests
{
    [Fact]
    public void AttackSpeedAffixUsesSixtyPercentMinimumRollFloor()
    {
        ItemDefinition template = new(
            "TEST_SPEED_WEAPON",
            "Test Speed Weapon",
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(0, 0, 0, 0),
            "Attack-speed generation test.",
            ItemLevelMin: 1,
            ItemLevelMax: 1,
            GuaranteedAffixStatIds: [ItemStatIds.AttackSpeed],
            RandomAffixPoolId: "SPEED_ONLY",
            AffixCountProfileId: "ONE_GUARANTEED",
            GenerationMode: ItemGenerationMode.Rolled);

        ItemizationDefinition itemization = new(
            TemplateBasePower: 110,
            LevelLinearCoefficient: 0.055m,
            LevelQuadraticCoefficient: 0.0035m,
            SlotMultipliers: new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["MAIN_HAND"] = 1.15m
            },
            RarityMultipliers: new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                ["COMMON"] = 1m
            },
            StatPowerWeights: new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                [ItemStatIds.AttackSpeed] = 25m
            },
            AffixPools:
            [
                new ItemAffixPoolDefinition(
                    "SPEED_ONLY",
                    [ItemStatIds.AttackSpeed])
            ],
            AffixCountProfiles:
            [
                new ItemAffixCountProfileDefinition(
                    "ONE_GUARANTEED",
                    GuaranteedCount: 1,
                    MinimumBonusCount: 0,
                    MaximumBonusCount: 0)
            ],
            QualityProfiles:
            [
                new ItemQualityProfileDefinition("NORMAL", BiasExponent: 1m)
            ],
            AffixNames: []);

        GeneratedItemInstance generated = ItemInstanceGenerator.Generate(
            template,
            itemization,
            "NORMAL",
            new SequenceGameRandom(0m, 0m));

        GeneratedItemAffix affix = Assert.Single(generated.Affixes);
        Assert.Equal(ItemStatIds.AttackSpeed, affix.StatId);
        Assert.Equal(5.0m, affix.MaxAtGeneration);
        Assert.Equal(3.0m, affix.MinAtGeneration);
        Assert.Equal(3.0m, affix.Value);
    }
}
