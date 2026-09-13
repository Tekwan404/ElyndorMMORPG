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
        Assert.InRange(level18, 49m, 50m);
        Assert.InRange(level60, 23m, 24m);
    }

    [Fact]
    public void ReductionIsHardCappedAtSixtyPercent()
    {
        decimal reduction = DefenseMitigationFormula.CalculateReductionPercent(100_000m, 1);

        Assert.Equal(DefenseMitigationFormula.MaximumReductionPercent, reduction);
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
