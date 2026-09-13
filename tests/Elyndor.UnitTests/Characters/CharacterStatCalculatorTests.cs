using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Characters;

public sealed class CharacterStatCalculatorTests
{
    [Theory]
    [InlineData("WARRIOR", 18, 8, 5, 14, 190, 44, 10, 6.9230769231)]
    [InlineData("ARCHER", 7, 15, 7, 11, 160, 29, 14, 8.4883720930)]
    [InlineData("MAGE", 5, 7, 17, 10, 150, 17, 34, 6.6908212560)]
    public void CalculatesApprovedStatsFromClassAndLevel(
        string classId,
        decimal strength,
        decimal agility,
        decimal intellect,
        decimal stamina,
        decimal maxHp,
        decimal attackPower,
        decimal spellPower,
        decimal criticalChance)
    {
        CharacterStatCalculator calculator = new(Formula(), Profiles());

        CharacterStats result = calculator.Calculate(classId, level: 3);

        Assert.Equal(strength, result.Strength);
        Assert.Equal(agility, result.Agility);
        Assert.Equal(intellect, result.Intellect);
        Assert.Equal(stamina, result.Stamina);
        Assert.Equal(maxHp, result.MaxHp);
        Assert.Equal(attackPower, result.AttackPower);
        Assert.Equal(spellPower, result.SpellPower);
        Assert.Equal(criticalChance, result.CriticalChance, 8);
        Assert.Equal(100, result.CriticalDamage);
        Assert.Equal(95, result.Accuracy);
        Assert.Equal(0, result.ArmorPenetration);
        Assert.Equal(0, result.MagicPenetration);
        Assert.Equal(1, result.AttackSpeed);
        Assert.Equal(0, result.Armor);
    }

    [Fact]
    public void AppliesDerivedTalentModifiersAfterPrimaryTalentStage()
    {
        CharacterStatCalculator calculator = new(Formula(), Profiles());
        CharacterStatInputs inputs = CharacterStatInputs.Empty with
        {
            EquipmentDerived = new CharacterEquipmentDerivedModifiers(ArmorFlat: 40),
            TalentDerived = new TalentStatModifiers(
                AttackPowerPercent: 10,
                ArmorPercent: 20,
                AccuracyPercent: 3,
                CriticalChancePercent: 4,
                CriticalDamagePercent: 15,
                ArmorPenetrationPercent: 9,
                AttackSpeedPercent: 6,
                MaxHpPercent: 10)
        };

        CharacterStats result = calculator.Calculate("WARRIOR", 3, inputs);

        Assert.Equal(209, result.MaxHp);
        Assert.Equal(48.4m, result.AttackPower);
        Assert.Equal(98, result.Accuracy);
        Assert.Equal(10.9230769231m, result.CriticalChance, 8);
        Assert.Equal(115, result.CriticalDamage);
        Assert.Equal(9, result.ArmorPenetration);
        Assert.Equal(1.06m, result.AttackSpeed);
        Assert.Equal(48m, result.Armor);
    }

    [Fact]
    public void AppliesEquipmentSecondaryStatsBeforeTalentDerivedModifiers()
    {
        CharacterStatCalculator calculator = new(Formula(), Profiles());
        CharacterStatInputs inputs = CharacterStatInputs.Empty with
        {
            EquipmentDerived = new CharacterEquipmentDerivedModifiers(
                MaxHpFlat: 25,
                AttackPowerFlat: 7,
                SpellPowerFlat: 9,
                CriticalChancePercent: 2,
                CriticalDamagePercent: 10,
                AccuracyPercent: 1,
                AttackSpeedPercent: 5,
                ArmorFlat: 18,
                MagicResistanceFlat: 11,
                DodgePercent: 3,
                ArmorPenetrationPercent: 4,
                MagicPenetrationPercent: 6),
            TalentDerived = new TalentStatModifiers(
                AttackPowerPercent: 10,
                ArmorPercent: 20,
                CriticalChancePercent: 1,
                ArmorPenetrationPercent: 2,
                MaxHpPercent: 10)
        };

        CharacterStats result = calculator.Calculate("WARRIOR", 3, inputs);

        Assert.Equal(236.5m, result.MaxHp);
        Assert.Equal(56.1m, result.AttackPower);
        Assert.Equal(19, result.SpellPower);
        Assert.Equal(9.9230769231m, result.CriticalChance, 8);
        Assert.Equal(110, result.CriticalDamage);
        Assert.Equal(96, result.Accuracy);
        Assert.Equal(6, result.ArmorPenetration);
        Assert.Equal(6, result.MagicPenetration);
        Assert.Equal(1.05m, result.AttackSpeed);
        Assert.Equal(21.6m, result.Armor);
        Assert.Equal(30, result.MagicResistance);
        Assert.Equal(4.5384615385m, result.Dodge, 8);
    }

    [Fact]
    public void FourHundredAgilityUsesDiminishingReturnsInsteadOfOneHundredPercentCrit()
    {
        CharacterStatCalculator calculator = new(Formula(), Profiles());
        CharacterStatInputs inputs = CharacterStatInputs.Empty with
        {
            Equipment = new PrimaryStats(0, 394, 0, 0)
        };

        CharacterStats result = calculator.Calculate("WARRIOR", level: 1, inputs);

        Assert.Equal(400, result.Agility);
        Assert.Equal(38.3333333333m, result.CriticalChance, 8);
        Assert.Equal(26.6666666667m, result.Dodge, 8);
    }

    [Fact]
    public void AgilityCombatStatsRespectHardCapsAfterExternalBonuses()
    {
        CharacterStatCalculator calculator = new(Formula(), Profiles());
        CharacterStatInputs inputs = CharacterStatInputs.Empty with
        {
            Equipment = new PrimaryStats(0, 994, 0, 0),
            EquipmentDerived = new CharacterEquipmentDerivedModifiers(
                CriticalChancePercent: 50,
                DodgePercent: 50)
        };

        CharacterStats result = calculator.Calculate("WARRIOR", level: 1, inputs);

        Assert.Equal(CharacterStatCalculator.CriticalChanceCap, result.CriticalChance);
        Assert.Equal(CharacterStatCalculator.DodgeCap, result.Dodge);
    }

    [Fact]
    public void ShieldBlockProfileUsesTalentBonusesButNeverCreatesGlobalBlock()
    {
        CharacterStatCalculator calculator = new(Formula(), Profiles());
        CharacterStatInputs shieldInputs = CharacterStatInputs.Empty with
        {
            EquipmentDerived = new CharacterEquipmentDerivedModifiers(
                BlockChancePercent: 55,
                BlockValueMin: 20,
                BlockValueMax: 40),
            TalentDerived = new TalentStatModifiers(
                BlockChancePercent: 15,
                BlockValueFlat: 5)
        };

        CharacterStats shielded = calculator.Calculate("WARRIOR", 3, shieldInputs);
        CharacterStats withoutShield = calculator.Calculate("WARRIOR", 3,
            CharacterStatInputs.Empty with
            {
                TalentDerived = new TalentStatModifiers(
                    BlockChancePercent: 15,
                    BlockValueFlat: 5)
            });

        Assert.Equal(60, shielded.BlockChance);
        Assert.Equal(25, shielded.BlockValueMin);
        Assert.Equal(45, shielded.BlockValueMax);
        Assert.Equal(0, withoutShield.BlockChance);
        Assert.Equal(0, withoutShield.BlockValueMin);
        Assert.Equal(0, withoutShield.BlockValueMax);
    }

    [Fact]
    public void StrengthAndStaminaNeverCreateArmorWithoutEquipment()
    {
        CharacterStatCalculator calculator = new(Formula(), Profiles());

        CharacterStats result = calculator.Calculate("WARRIOR", level: 60);

        Assert.True(result.Strength > 0);
        Assert.True(result.Stamina > 0);
        Assert.Equal(0, result.Armor);
    }

    [Fact]
    public void ArmorReductionBreakdownUsesCharacterLevelAsReference()
    {
        CharacterStatCalculator calculator = new(Formula(), Profiles());
        CharacterStatInputs inputs = CharacterStatInputs.Empty with
        {
            EquipmentDerived = new CharacterEquipmentDerivedModifiers(ArmorFlat: 745)
        };

        CharacterStatCalculation result = calculator.CalculateDetailed("WARRIOR", 18, inputs);

        CharacterStatBreakdown reduction = result.Breakdown["armorDamageReductionPercent"];
        Assert.InRange(reduction.FinalValue, 49m, 50m);
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
        Profile("WARRIOR", "STRENGTH", "RAGE", new(12, 6, 4, 10), new(3, 1, 0.5m, 2)),
        Profile("ARCHER", "AGILITY", "FOCUS", new(5, 9, 5, 7), new(1, 3, 1, 2)),
        Profile("MAGE", "INTELLECT", "MANA", new(3, 5, 11, 6), new(1, 1, 3, 2))
    ];

    private static ClassProfile Profile(
        string id,
        string primary,
        string resource,
        PrimaryStats stats,
        PrimaryStats growth) =>
        new(
            id,
            primary,
            resource,
            stats,
            growth,
            ["SWORD"],
            ["LIGHT"],
            "Prototype identity");
}
