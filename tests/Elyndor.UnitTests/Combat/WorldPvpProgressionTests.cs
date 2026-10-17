using Elyndor.Core.World;

namespace Elyndor.UnitTests.Combat;

public sealed class WorldPvpProgressionTests
{
    [Fact]
    public void ContributionCreditsDamageAndSupportWithoutLastHitBonus()
    {
        Guid tank = Guid.NewGuid(), healer = Guid.NewGuid(), tagger = Guid.NewGuid();
        var defeat = new WorldPvpDefeat(Guid.NewGuid(), DateTimeOffset.UtcNow,
            [new(tank, 100, 0), new(healer, 0, 50), new(tagger, 1, 0)]);
        var credits = WorldPvpBalance.Credits(defeat);
        Assert.Equal(2, credits.Count);
        Assert.True(credits.Single(c => c.CharacterId == tank).Victory);
        Assert.False(credits.Single(c => c.CharacterId == healer).Victory);
        Assert.DoesNotContain(credits, c => c.CharacterId == tagger);
    }

    [Theory]
    [InlineData(0, 0, 10)]
    [InlineData(1000, 0, 5)]
    [InlineData(0, 1000, 15)]
    public void StrongerOpponentsGiveMoreRating(int own, int enemy, int expected) =>
        Assert.Equal(expected, WorldPvpBalance.RatingAward(own, enemy));

    [Fact]
    public void SeasonsHaveEightWeeksAndStartOnUtcBoundary()
    {
        var start = WorldPvpBalance.SeasonEpoch;
        Assert.Equal(1, WorldPvpBalance.Season(start));
        Assert.Equal(1, WorldPvpBalance.Season(start.AddDays(56).AddTicks(-1)));
        Assert.Equal(2, WorldPvpBalance.Season(start.AddDays(56)));
    }
}
