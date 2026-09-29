namespace Elyndor.Core.Pvp;

public sealed record ArenaProgression(int RatingA, int RatingB, long HonorA, long HonorB);

public static class ArenaProgressionRules
{
    public const int InitialRating = 1000;
    public const int RatingChangeFactor = 24;
    public const long VictoryHonor = 10;
    public const int FormulaVersion = 1;

    public static ArenaProgression Calculate(int ratingA, int ratingB, ArenaMatchOutcome outcome,
        bool eligibleForProgression = true, int ratingChangeFactor = RatingChangeFactor,
        long victoryHonor = VictoryHonor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ratingChangeFactor);
        ArgumentOutOfRangeException.ThrowIfNegative(victoryHonor);
        ArgumentOutOfRangeException.ThrowIfNegative(ratingA);
        ArgumentOutOfRangeException.ThrowIfNegative(ratingB);

        if (!eligibleForProgression || outcome is not (ArenaMatchOutcome.WinnerA or ArenaMatchOutcome.WinnerB))
            return new ArenaProgression(ratingA, ratingB, 0, 0);

        double expectedA = 1d / (1d + Math.Pow(10d, (ratingB - ratingA) / 400d));
        double scoreA = outcome == ArenaMatchOutcome.WinnerA ? 1d : 0d;
        int deltaA = (int)Math.Round(ratingChangeFactor * (scoreA - expectedA), MidpointRounding.AwayFromZero);
        int deltaB = (int)Math.Round(ratingChangeFactor * (expectedA - scoreA), MidpointRounding.AwayFromZero);

        return new ArenaProgression(
            (int)Math.Clamp((long)ratingA + deltaA, 0, int.MaxValue),
            (int)Math.Clamp((long)ratingB + deltaB, 0, int.MaxValue),
            outcome == ArenaMatchOutcome.WinnerA ? victoryHonor : 0,
            outcome == ArenaMatchOutcome.WinnerB ? victoryHonor : 0);
    }
}
