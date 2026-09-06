using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

public static class ItemInstanceStatRoller
{
    public static PrimaryStats Resolve(
        ItemDefinition definition,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(random);

        PrimaryStatRanges? ranges = definition.PrimaryStatRanges;
        if (ranges is null)
            return definition.Stats;

        return new PrimaryStats(
            Roll(ranges.Strength, definition.Stats.Strength, random),
            Roll(ranges.Agility, definition.Stats.Agility, random),
            Roll(ranges.Intellect, definition.Stats.Intellect, random),
            Roll(ranges.Stamina, definition.Stats.Stamina, random));
    }

    private static decimal Roll(
        ItemStatRange? range,
        decimal fallback,
        IGameRandom random)
    {
        if (range is null) return fallback;
        if (range.Min < 0 || range.Max < range.Min || range.Step <= 0)
            throw new InvalidOperationException("Item stat range is invalid.");
        if (range.Min == range.Max) return range.Min;

        decimal span = range.Max - range.Min;
        decimal steps = decimal.Floor(span / range.Step) + 1;
        decimal index = decimal.Floor(random.NextUnit() * steps);
        return decimal.Min(range.Max, range.Min + (index * range.Step));
    }
}
