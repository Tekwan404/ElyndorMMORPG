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

    [Theory]
    [InlineData(5, 3)]
    [InlineData(10, 5)]
    [InlineData(15, 8)]
    public void RatingLossIsHalfWinnerAwardRoundedUp(int award, int loss) =>
        Assert.Equal(loss, WorldPvpBalance.RatingLoss(award));

    [Fact]
    public void RatingLossNeverDropsBelowZero()
    {
        var standing = new WorldPvpStanding(Guid.NewGuid(), 1);
        standing.Credit(true, 12);
        standing.LoseRating(8);
        Assert.Equal(4, standing.Rating);
        standing.LoseRating(30);
        Assert.Equal(0, standing.Rating);
    }

    [Fact]
    public void GroupAssistsDiminishWithoutPenalizingSoloOrWinner()
    {
        Assert.Equal(5, WorldPvpBalance.GroupAward(5, 1, false));
        Assert.Equal(3, WorldPvpBalance.GroupAward(5, 2, false));
        Assert.Equal(2, WorldPvpBalance.GroupAward(5, 4, false));
        Assert.Equal(5, WorldPvpBalance.GroupAward(5, 10, true));
    }

    [Fact]
    public void SeasonsHaveEightWeeksAndStartOnUtcBoundary()
    {
        var start = WorldPvpBalance.SeasonEpoch;
        Assert.Equal(1, WorldPvpBalance.Season(start));
        Assert.Equal(1, WorldPvpBalance.Season(start.AddDays(56).AddTicks(-1)));
        Assert.Equal(2, WorldPvpBalance.Season(start.AddDays(56)));
    }
}
