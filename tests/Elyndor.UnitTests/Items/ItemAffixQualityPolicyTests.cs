using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class ItemAffixQualityPolicyTests
{
    [Fact]
    public void TierUsesRealizedAffixQualityInsteadOfItemLevel()
    {
        Assert.Equal(1, ItemAffixQualityPolicy.Tier(10m, 10m, 20m));
        Assert.Equal(2, ItemAffixQualityPolicy.Tier(13m, 10m, 20m));
        Assert.Equal(3, ItemAffixQualityPolicy.Tier(15m, 10m, 20m));
        Assert.Equal(4, ItemAffixQualityPolicy.Tier(17m, 10m, 20m));
        Assert.Equal(5, ItemAffixQualityPolicy.Tier(19m, 10m, 20m));
        Assert.Equal(5, ItemAffixQualityPolicy.Tier(20m, 10m, 20m));
    }

    [Fact]
    public void PercentReportsRealizedAffixRoll()
    {
        Assert.Equal(73.33m, ItemAffixQualityPolicy.Percent(17.333m, 10m, 20m));
    }
}
