using Elyndor.Core.Combat.Damage;

namespace Elyndor.UnitTests.Combat;

public sealed class DefenseMitigationFormulaTests
{
    [Fact]
    public void ReductionPercentMatchesLevelScaledMitigationFormula()
    {
        const decimal defense = 627.5m;
        const int referenceLevel = 18;

        decimal reduction = DefenseMitigationFormula.CalculateReductionPercent(
            defense,
            referenceLevel);

        decimal mitigationConstant = DefenseMitigationFormula.BaseMitigationConstant
            + (DefenseMitigationFormula.MitigationConstantPerLevel * referenceLevel);
        decimal expected = defense / (mitigationConstant + defense) * 100m;
        Assert.Equal(expected, reduction, 10);
    }

    [Fact]
    public void SameArmorIsLessEffectiveAgainstHigherLevelAttackers()
    {
        const decimal defense = 745m;

        decimal level18 = DefenseMitigationFormula.CalculateReductionPercent(defense, 18);
        decimal level60 = DefenseMitigationFormula.CalculateReductionPercent(defense, 60);

        Assert.True(level18 > level60);
        Assert.InRange(level18, 27m, 29m);
        Assert.InRange(level60, 11m, 13m);
    }

    [Fact]
    public void ReductionIsHardCappedAtSeventyFivePercent()
    {
        decimal reduction = DefenseMitigationFormula.CalculateReductionPercent(1_000_000m, 1);

        Assert.Equal(DefenseMitigationFormula.MaximumReductionPercent, reduction);
    }

    [Fact]
    public void LevelSixtyThreeReferenceMatchesTargetCurve()
    {
        const int attackerLevel = 63;

        decimal constant = DefenseMitigationFormula.BaseMitigationConstant
            + (DefenseMitigationFormula.MitigationConstantPerLevel * attackerLevel);

        Assert.Equal(5_755m, constant);
        Assert.InRange(
            DefenseMitigationFormula.CalculateReductionPercent(2_000m, attackerLevel),
            25.7m,
            25.9m);
        Assert.InRange(
            DefenseMitigationFormula.CalculateReductionPercent(4_000m, attackerLevel),
            40.9m,
            41.1m);
        Assert.Equal(
            50m,
            DefenseMitigationFormula.CalculateReductionPercent(5_755m, attackerLevel));
        Assert.InRange(
            DefenseMitigationFormula.CalculateReductionPercent(8_000m, attackerLevel),
            58.1m,
            58.3m);
        Assert.InRange(
            DefenseMitigationFormula.CalculateReductionPercent(12_000m, attackerLevel),
            67.5m,
            67.7m);
    }

    [Fact]
    public void SeventyFivePercentCapAtLevelSixtyThreeStartsAtThreeTimesMitigationConstant()
    {
        const int attackerLevel = 63;
        const decimal armorAtCap = 17_265m;

        Assert.Equal(
            75m,
            DefenseMitigationFormula.CalculateReductionPercent(armorAtCap, attackerLevel));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-25, 0)]
    public void NonPositiveDefenseHasNoReduction(decimal defense, decimal expected)
    {
        Assert.Equal(expected, DefenseMitigationFormula.CalculateReductionPercent(defense, 18));
    }

    [Fact]
    public void ReferenceLevelMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DefenseMitigationFormula.CalculateReductionPercent(100m, 0));
    }
}
