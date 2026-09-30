using Elyndor.Core.Pvp;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaProgressionRulesTests
{
    [Theory]
    [InlineData(ArenaMatchOutcome.WinnerA, 1012, 988, 10)]
    [InlineData(ArenaMatchOutcome.WinnerB, 988, 1012, 0)]
    [InlineData(ArenaMatchOutcome.Draw, 1000, 1000, 0)]
    [InlineData(ArenaMatchOutcome.Cancelled, 1000, 1000, 0)]
    public void EqualRatedMatchSettlesBothRatingsAndOnlyWinnerHonor(ArenaMatchOutcome outcome,
        int expectedA, int expectedB, long expectedHonorA)
    {
        ArenaProgression change = ArenaProgressionRules.Calculate(1000, 1000, outcome);
        Assert.Equal(expectedA, change.RatingA);
        Assert.Equal(expectedB, change.RatingB);
        Assert.Equal(expectedHonorA, change.HonorA);
        Assert.Equal(outcome == ArenaMatchOutcome.WinnerB ? 10 : 0, change.HonorB);
    }

    [Fact]
    public void RatingsNeverBecomeNegative()
    {
        ArenaProgression change = ArenaProgressionRules.Calculate(0, 1000, ArenaMatchOutcome.WinnerB);
        Assert.Equal(0, change.RatingA);
        Assert.True(change.RatingB >= 1000);
    }

    [Fact]
    public void IneligibleRematchCannotGrantHonorOrChangeRating()
    {
        ArenaProgression change = ArenaProgressionRules.Calculate(1000, 1000,
            ArenaMatchOutcome.WinnerA, eligibleForProgression: false);
        Assert.Equal(1000, change.RatingA);
        Assert.Equal(1000, change.RatingB);
        Assert.Equal(0, change.HonorA);
    }

    [Fact]
    public void MaximumPersistedRatingCannotOverflowOnVictory()
    {
        ArenaProgression change = ArenaProgressionRules.Calculate(int.MaxValue, int.MaxValue,
            ArenaMatchOutcome.WinnerA);
        Assert.Equal(int.MaxValue, change.RatingA);
        Assert.Equal(int.MaxValue - 12, change.RatingB);
    }
}
