namespace Elyndor.Core.Items;

public static class ItemAffixQualityPolicy
{
    public static decimal Normalize(decimal value, decimal minimum, decimal maximum)
    {
        if (maximum <= minimum)
            return 0m;

        return decimal.Clamp((value - minimum) / (maximum - minimum), 0m, 1m);
    }

    public static decimal Percent(decimal value, decimal minimum, decimal maximum) =>
        decimal.Round(
            Normalize(value, minimum, maximum) * 100m,
            2,
            MidpointRounding.AwayFromZero);

    public static int Tier(decimal value, decimal minimum, decimal maximum) =>
        TierFromQuality(Normalize(value, minimum, maximum));

    public static int TierFromQuality(decimal quality)
    {
        decimal normalized = decimal.Clamp(quality, 0m, 1m);
        return normalized >= 0.90m ? 5
            : normalized >= 0.70m ? 4
            : normalized >= 0.50m ? 3
            : normalized >= 0.30m ? 2
            : 1;
    }
}
