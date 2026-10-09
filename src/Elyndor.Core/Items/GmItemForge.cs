namespace Elyndor.Core.Items;

/// <summary>
/// Explicitly admin-only item mutations. Never use this policy for loot, crafting or shops.
/// Custom affixes deliberately bypass the authored item budget for combat testing.
/// </summary>
public static class GmItemForge
{
    public const string SourceType = "GM_FORGE";

    public static GeneratedItemInstance Apply(
        GeneratedItemInstance source,
        bool perfect,
        int? forcedStars,
        IReadOnlyDictionary<string, decimal> statOverrides)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(statOverrides);
        if (forcedStars is < 1 or > 5 || (perfect && forcedStars.HasValue && forcedStars.Value != 5))
            throw new ArgumentOutOfRangeException(nameof(forcedStars));
        if (statOverrides.Count > ItemStatIds.ApprovedV1.Count)
            throw new ArgumentOutOfRangeException(nameof(statOverrides));

        List<GeneratedItemAffix> affixes = source.Affixes
            .Select(affix => perfect ? affix with
            {
                Value = affix.MaxAtGeneration,
                AffixTier = ItemAffixQualityPolicy.Tier(
                    affix.MaxAtGeneration, affix.MinAtGeneration, affix.MaxAtGeneration)
            } : affix)
            .ToList();

        foreach ((string statId, decimal value) in statOverrides)
        {
            if (!ItemStatIds.ApprovedV1.Contains(statId) || value is < 0m or > 1_000_000m)
                throw new ArgumentOutOfRangeException(nameof(statOverrides), "Unsupported GM item stat/value.");

            int index = affixes.FindIndex(affix => affix.StatId == statId);
            if (index >= 0)
            {
                GeneratedItemAffix prior = affixes[index];
                decimal minimum = decimal.Min(prior.MinAtGeneration, value);
                decimal maximum = decimal.Max(prior.MaxAtGeneration, value);
                affixes[index] = prior with
                {
                    Value = value,
                    MinAtGeneration = minimum,
                    MaxAtGeneration = maximum,
                    AffixTier = ItemAffixQualityPolicy.Tier(value, minimum, maximum)
                };
            }
            else
            {
                int ordinal = affixes.Count;
                decimal maximum = decimal.Max(1m, value);
                decimal step = ItemStatIds.IsPercentage(statId) ? 0.1m : 1m;
                affixes.Add(new GeneratedItemAffix(
                    $"AFFIX_{ordinal + 1}", statId, statId, value, 0m, maximum, step,
                    ItemAffixQualityPolicy.Tier(value, 0m, maximum), false, false, ordinal));
            }
        }

        // Stars and Perfect are intentionally overridable only on GM-created instances.
        // An altered stat is not a legitimate natural Perfect drop.
        bool isPerfect = perfect || (statOverrides.Count == 0 && source.IsPerfect);
        return source with
        {
            Affixes = affixes,
            Stars = perfect ? 5 : forcedStars ?? source.Stars,
            RollQuality = perfect ? 100m : source.RollQuality,
            IsPerfect = isPerfect,
            PerfectOrigin = perfect ? SourceType : isPerfect ? source.PerfectOrigin : null
        };
    }
}
