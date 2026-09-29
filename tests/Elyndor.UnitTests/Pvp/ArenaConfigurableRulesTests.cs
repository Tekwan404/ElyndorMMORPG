using Elyndor.Core.Pvp;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaConfigurableRulesTests
{
    [Fact]
    public void RatingFactorAndHonorComeFromConfiguration()
    {
        ArenaProgression result = ArenaProgressionRules.Calculate(1000, 1000, ArenaMatchOutcome.WinnerA,
            ratingChangeFactor: 32, victoryHonor: 25);
        Assert.Equal(1016, result.RatingA);
        Assert.Equal(984, result.RatingB);
        Assert.Equal(25, result.HonorA);
        Assert.Equal(0, result.HonorB);
    }

    [Fact]
    public void DefaultsMatchTheDocumentedFormulaVersion()
    {
        ArenaProgression result = ArenaProgressionRules.Calculate(1000, 1000, ArenaMatchOutcome.WinnerB);
        Assert.Equal(988, result.RatingA);
        Assert.Equal(1012, result.RatingB);
        Assert.Equal(ArenaProgressionRules.VictoryHonor, result.HonorB);
        Assert.Equal(1, ArenaProgressionRules.FormulaVersion);
    }

    [Fact]
    public void NewMatchesRecordTheFormulaVersionUsed()
    {
        var match = new ArenaMatch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 29, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(ArenaProgressionRules.FormulaVersion, match.FormulaVersion);
    }
}
