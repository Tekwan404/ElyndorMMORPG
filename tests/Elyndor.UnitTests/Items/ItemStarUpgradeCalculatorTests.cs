using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class ItemStarUpgradeCalculatorTests
{
    [Fact]
    public void UpgradeRaisesOneStarItemToTheNextStarWithoutChangingAffixIdentity()
    {
        GeneratedItemAffix affix = new("AFFIX_1", "STRENGTH", "STRENGTH", 3, 2, 10, 1, 1, false, false, 0);

        GeneratedItemAffix[] result = ItemStarUpgradeCalculator.IncreaseToTargetStar([affix], 2);

        Assert.Single(result);
        Assert.Equal("AFFIX_1", result[0].SlotKey);
        Assert.Equal("STRENGTH", result[0].StatId);
        Assert.True(result[0].Value > affix.Value);
        Assert.InRange(result[0].Value, affix.MinAtGeneration, affix.MaxAtGeneration);
    }

    [Fact]
    public void UpgradeToFiveStarsSetsEveryAffixToItsMaximum()
    {
        GeneratedItemAffix[] result = ItemStarUpgradeCalculator.IncreaseToTargetStar(
        [
            new("AFFIX_1", "STRENGTH", "STRENGTH", 3, 2, 10, 1, 1, false, false, 0),
            new("AFFIX_2", "STAMINA", "STAMINA", 2, 1, 8, 1, 1, false, false, 1),
        ], 5);

        Assert.Equal([10m, 8m], result.Select(affix => affix.Value).ToArray());
    }
}
