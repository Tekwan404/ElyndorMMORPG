using Elyndor.Core.Afk;

namespace Elyndor.UnitTests.Afk;

public sealed class AfkFarmIntervalGrantTests
{
    [Fact]
    public void CreateCapturesOneProcessedInterval()
    {
        DateTimeOffset start = new(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        AfkFarmIntervalGrant grant = new(
            Guid.NewGuid(),
            3,
            start,
            start.AddMinutes(15),
            kills: 12,
            xpEarned: 84,
            goldEarned: 15,
            lootJson: "[]",
            grantedAtUtc: start.AddMinutes(15));

        Assert.Equal(3, grant.IntervalIndex);
        Assert.Equal(12, grant.Kills);
        Assert.Equal(84, grant.XpEarned);
        Assert.Equal(15, grant.GoldEarned);
    }
}
