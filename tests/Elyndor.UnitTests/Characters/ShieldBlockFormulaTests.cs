using Elyndor.Core.Characters;

namespace Elyndor.UnitTests.Characters;

public sealed class ShieldBlockFormulaTests
{
    [Fact]
    public void BlockRatingControlsChanceWithoutChangingBlockValue()
    {
        ShieldBlockResult result = ShieldBlockFormula.Resolve(
            blockRating: 2.4m,
            shieldBlockValueMin: 50,
            shieldBlockValueMax: 70,
            strength: 0);

        Assert.Equal(8, result.BlockChancePercent);
        Assert.Equal(50, result.BlockValueMin);
        Assert.Equal(70, result.BlockValueMax);
        Assert.Equal(0, result.StrengthContribution);
    }

    [Fact]
    public void StrengthScalesBlockValueWithoutChangingBlockChance()
    {
        ShieldBlockResult result = ShieldBlockFormula.Resolve(
            blockRating: 2.4m,
            shieldBlockValueMin: 50,
            shieldBlockValueMax: 70,
            strength: 100);

        Assert.Equal(8, result.BlockChancePercent);
        Assert.Equal(75, result.StrengthContribution);
        Assert.Equal(125, result.BlockValueMin);
        Assert.Equal(145, result.BlockValueMax);
    }

    [Fact]
    public void StrengthDoesNotCreateBlockWithoutShieldProfile()
    {
        ShieldBlockResult result = ShieldBlockFormula.Resolve(
            blockRating: 0,
            shieldBlockValueMin: 0,
            shieldBlockValueMax: 0,
            strength: 500);

        Assert.Equal(ShieldBlockResult.Empty, result);
    }

    [Fact]
    public void BlockChanceIsClampedAtOneHundredPercent()
    {
        ShieldBlockResult result = ShieldBlockFormula.Resolve(
            blockRating: 100,
            shieldBlockValueMin: 1,
            shieldBlockValueMax: 1,
            strength: 0);

        Assert.Equal(100, result.BlockChancePercent);
    }
}
