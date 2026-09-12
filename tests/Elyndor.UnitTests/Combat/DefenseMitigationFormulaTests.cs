using Elyndor.Core.Combat.Damage;

namespace Elyndor.UnitTests.Combat;

public sealed class DefenseMitigationFormulaTests
{
    [Fact]
    public void ReductionPercentMatchesCombatMitigationFormula()
    {
        decimal defense = 291.5m;

        decimal reduction = DefenseMitigationFormula.CalculateReductionPercent(defense);

        decimal expected = defense / (DefenseMitigationFormula.MitigationConstant + defense) * 100m;
        Assert.Equal(expected, reduction, 10);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-25, 0)]
    public void NonPositiveDefenseHasNoReduction(decimal defense, decimal expected)
    {
        Assert.Equal(expected, DefenseMitigationFormula.CalculateReductionPercent(defense));
    }
}
