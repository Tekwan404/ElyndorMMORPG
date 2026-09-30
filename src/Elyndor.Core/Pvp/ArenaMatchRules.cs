namespace Elyndor.Core.Pvp;

public enum ArenaMatchOutcome
{
    Active,
    WinnerA,
    WinnerB,
    Draw,
    Cancelled
}

public static class ArenaMatchRules
{
    public const int MaximumRankedLevelDifference = 3;

    public static bool CanPair(Guid firstCharacterId, int firstLevel, Guid secondCharacterId, int secondLevel) =>
        CanPairTest(firstCharacterId, firstLevel, secondCharacterId, secondLevel)
        && Math.Abs(firstLevel - secondLevel) <= MaximumRankedLevelDifference;

    public static bool CanPairTest(Guid firstCharacterId, int firstLevel, Guid secondCharacterId, int secondLevel) =>
        firstCharacterId != Guid.Empty
        && secondCharacterId != Guid.Empty
        && firstCharacterId != secondCharacterId
        && firstLevel > 0
        && secondLevel > 0;

    public static ArenaMatchOutcome Resolve(ArenaMatchOutcome current, bool firstAlive,
        bool secondAlive, bool timedOut)
    {
        if (current != ArenaMatchOutcome.Active) return current;
        if (!firstAlive && !secondAlive) return ArenaMatchOutcome.Draw;
        if (!firstAlive) return ArenaMatchOutcome.WinnerB;
        if (!secondAlive) return ArenaMatchOutcome.WinnerA;
        if (timedOut) return ArenaMatchOutcome.Draw;
        return ArenaMatchOutcome.Active;
    }
}
