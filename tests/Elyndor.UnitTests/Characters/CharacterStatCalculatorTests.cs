using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Characters;

public sealed class CharacterStatCalculatorTests
{
    [Theory]
    [InlineData("WARRIOR", 18, 8, 5, 14, 190, 44, 10, 7)]
    [InlineData("ARCHER", 7, 15, 7, 11, 160, 29, 14, 8.75)]
    [InlineData("MAGE", 5, 7, 17, 10, 150, 17, 34, 6.75)]
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
        Assert.Equal(criticalChance, result.CriticalChance);
        Assert.Equal(100, result.CriticalDamage);
        Assert.Equal(95, result.Accuracy);
        Assert.Equal(0, result.ArmorPenetration);
        Assert.Equal(0, result.MagicPenetration);
        Assert.Equal(1, result.AttackSpeed);
    }

    [Fact]
    public void AppliesDerivedTalentModifiersAfterPrimaryTalentStage()
    {
        CharacterStatCalculator calculator = new(Formula(), Profiles());
        CharacterStatInputs inputs = CharacterStatInputs.Empty with
        {
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
        Assert.Equal(11, result.CriticalChance);
        Assert.Equal(115, result.CriticalDamage);
        Assert.Equal(9, result.ArmorPenetration);
        Assert.Equal(1.06m, result.AttackSpeed);
        Assert.Equal(55.2m, result.Armor);
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
