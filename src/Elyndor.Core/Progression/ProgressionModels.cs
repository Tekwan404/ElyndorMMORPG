namespace Elyndor.Core.Progression;

public sealed record LevelProgressionDefinition(
    string Id,
    int MaxLevel,
    int BaseXpToNext,
    decimal GrowthFactor)
{
    public int XpToNext(int level)
    {
        if (level < 1 || level >= MaxLevel)
            return 0;

        decimal value = BaseXpToNext;
        for (var current = 1; current < level; current++)
            value *= GrowthFactor;

        return decimal.ToInt32(decimal.Ceiling(value));
    }
}
