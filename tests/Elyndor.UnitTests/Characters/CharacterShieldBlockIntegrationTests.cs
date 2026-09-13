using Elyndor.Core.Characters;
using Elyndor.Core.Content;

namespace Elyndor.UnitTests.Characters;

public sealed class CharacterShieldBlockIntegrationTests
{
    [Fact]
    public void ShieldRatingResolvesChanceAndStrengthResolvesBlockValue()
    {
        CharacterStatCalculator calculator = new(Formula(), Profiles());
        CharacterStatInputs inputs = CharacterStatInputs.Empty with
        {
            Equipment = new PrimaryStats(18, 0, 0, 0),
            EquipmentDerived = new CharacterEquipmentDerivedModifiers(
                BlockChancePercent: 2.4m,
                BlockValueMin: 50,
                BlockValueMax: 70)
        };

        CharacterStatCalculation result = calculator.CalculateDetailed("WARRIOR", 1, inputs);

        // 12 class Strength + 18 equipment Strength = 30 Strength.
        Assert.Equal(8, result.Stats.BlockChance);
        Assert.Equal(22.5m, result.Breakdown["blockValueMin"].Contributions
            .Single(contribution => contribution.Source == "STRENGTH").Value);
        Assert.Equal(72.5m, result.Stats.BlockValueMin);
        Assert.Equal(92.5m, result.Stats.BlockValueMax);
        Assert.Equal(0, result.Stats.Armor);
    }

    [Fact]
    public void StrengthAloneDoesNotCreateBlockOrArmor()
    {
        CharacterStatCalculator calculator = new(Formula(), Profiles());
        CharacterStatInputs inputs = CharacterStatInputs.Empty with
        {
            Equipment = new PrimaryStats(100, 0, 0, 0)
        };

        CharacterStats result = calculator.Calculate("WARRIOR", 1, inputs);

        Assert.Equal(0, result.BlockChance);
        Assert.Equal(0, result.BlockValueMin);
        Assert.Equal(0, result.BlockValueMax);
        Assert.Equal(0, result.Armor);
    }

    private static StatFormulaProfile Formula() => new(
        "PROTOTYPE_STATS_V1",
        MaxHpBase: 50,
        MaxHpPerStamina: 10,
        AttackPowerPerStrength: 2,
        AttackPowerPerAgility: 1,
        SpellPowerPerIntellect: 2,
        ArmorPerStamina: 2,
        ArmorPerStrength: 1,
        MagicResistancePerStamina: 1,
        MagicResistancePerIntellect: 1,
        CriticalChanceBase: 5,
        CriticalChancePerAgility: 0.25m,
        CriticalDamageBase: 100,
        AccuracyBase: 95,
        DodgePerAgility: 0.2m,
        AttackSpeedBase: 1);

    private static IReadOnlyList<ClassProfile> Profiles() =>
    [
        new ClassProfile(
            "WARRIOR",
            "STRENGTH",
            "RAGE",
            new PrimaryStats(12, 6, 4, 10),
            new PrimaryStats(3, 1, 0.5m, 2),
            ["ONE_HAND_SWORD"],
            ["HEAVY"],
            "Prototype identity")
    ];
}
