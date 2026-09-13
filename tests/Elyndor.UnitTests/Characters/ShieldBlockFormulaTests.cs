using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Damage;

namespace Elyndor.UnitTests.Characters;

public sealed class ShieldBlockFormulaTests
{
    [Fact]
    public void StrengthScalesBlockValueWithoutChangingBaseChance()
    {
        ShieldBlockResult result = ShieldBlockFormula.Resolve(
            baseBlockChancePercent: 9,
            shieldBlockValueMin: 45,
            shieldBlockValueMax: 65,
            strength: 100,
            talentBlockChancePercent: 5,
            talentBlockValueFlat: 10);

        Assert.True(result.HasShieldProfile);
        Assert.Equal(14, result.BlockChancePercent);
        Assert.Equal(75, result.StrengthContribution);
        Assert.Equal(130, result.BlockValueMin);
        Assert.Equal(150, result.BlockValueMax);
    }

    [Fact]
    public void StrengthAndTalentsCannotCreateBlockWithoutShieldProfile()
    {
        ShieldBlockResult result = ShieldBlockFormula.Resolve(
            baseBlockChancePercent: 0,
            shieldBlockValueMin: 0,
            shieldBlockValueMax: 0,
            strength: 500,
            talentBlockChancePercent: 25,
            talentBlockValueFlat: 100);

        Assert.False(result.HasShieldProfile);
        Assert.Equal(0, result.BlockChancePercent);
        Assert.Equal(0, result.BlockValueMin);
        Assert.Equal(0, result.BlockValueMax);
        Assert.Equal(0, result.StrengthContribution);
    }

    [Fact]
    public void FinalBlockChanceRespectsDamagePipelineCap()
    {
        ShieldBlockResult result = ShieldBlockFormula.Resolve(
            baseBlockChancePercent: 55,
            shieldBlockValueMin: 20,
            shieldBlockValueMax: 40,
            strength: 20,
            talentBlockChancePercent: 20);

        Assert.Equal(DamagePipeline.MaximumBlockChancePercent, result.BlockChancePercent);
    }

    [Fact]
    public void NegativeStrengthDoesNotReduceShieldBlockValue()
    {
        ShieldBlockResult result = ShieldBlockFormula.Resolve(
            baseBlockChancePercent: 10,
            shieldBlockValueMin: 30,
            shieldBlockValueMax: 50,
            strength: -100);

        Assert.Equal(0, result.StrengthContribution);
        Assert.Equal(30, result.BlockValueMin);
        Assert.Equal(50, result.BlockValueMax);
    }
}
