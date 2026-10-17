using Elyndor.Core.World;

namespace Elyndor.UnitTests.Combat;

public sealed class WorldPvpProgressionTests
{
    [Theory]
    [InlineData(0, true)] [InlineData(25000, true)]
    [InlineData(-1, false)] [InlineData(1000000001, false)]
    public void MoneyRewardsRequireBoundedNonnegativeBronze(long amount, bool expected) =>
        Assert.Equal(expected, new WorldPvpRewardProfile(amount, amount).IsValid());

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
    [InlineData(0, 20)] [InlineData(1, 15)] [InlineData(2, 10)]
    [InlineData(3, 5)] [InlineData(4, 0)] [InlineData(20, 0)]
    public void RepeatHonorDiminishesToZero(int previous, int expected) =>
        Assert.Equal(expected, WorldPvpBalance.RepeatHonor(20, previous));

    [Fact]
    public void DeathResetsStreakButNeverEarnedHonorAndPurchasesOnlySpendWallet()
    {
        var standing = new WorldPvpStanding(Guid.NewGuid(), 1);
        standing.Credit(true, 720);
        standing.RecordDeath();
        standing.Credit(false, 0);
        Assert.Equal(720, standing.SeasonHonor);
        Assert.Equal(0, standing.KillStreak);
        Assert.Equal(1, standing.Assists);
        Assert.Equal(1, standing.Victories);
        var wallet = new WorldPvpWallet(standing.CharacterId);
        wallet.Grant(720, 0);
        Assert.True(wallet.TrySpend(300, false));
        Assert.Equal(420, wallet.FieldHonor);
        Assert.Equal(720, standing.SeasonHonor);
    }

    [Fact]
    public void FourteenRanksAreOrderedAndIncludeEveryThresholdExactly()
    {
        Assert.Equal(14, WorldPvpBalance.Ranks.Count);
        Assert.Equal(1, WorldPvpBalance.Rank(0).Level);
        foreach (var rank in WorldPvpBalance.Ranks.Skip(1))
        {
            Assert.Equal(rank.Level - 1, WorldPvpBalance.Rank(rank.RequiredHonor - 1).Level);
            Assert.Equal(rank, WorldPvpBalance.Rank(rank.RequiredHonor));
        }
        Assert.Equal(14, WorldPvpBalance.Rank(long.MaxValue).Level);
    }

    [Fact]
    public void GroupAssistsDiminishWithoutPenalizingSoloOrWinner()
    {
        Assert.Equal(20, WorldPvpBalance.GroupAward(20, 1, false));
        Assert.Equal(10, WorldPvpBalance.GroupAward(20, 2, false));
        Assert.Equal(5, WorldPvpBalance.GroupAward(20, 4, false));
        Assert.Equal(20, WorldPvpBalance.GroupAward(20, 10, true));
        Assert.Equal(0, WorldPvpBalance.RepeatHonor(1, 1));
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
