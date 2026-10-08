using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Items;

public static class ItemAffixEligibilityPolicy
{
    public static bool IsAllowed(ItemDefinition item, ItemAffixRuleDefinition rule, int? itemLevel = null) =>
        (itemLevel ?? item.ItemLevelMin ?? item.RequiredLevel) >= rule.MinimumItemLevel
        && (rule.AllowedClassIds is not { Count: > 0 }
            || item.AllowedClassIds is not { Count: > 0 }
            || item.AllowedClassIds.Any(rule.AllowedClassIds.Contains))
        && (rule.AllowedSlots is not { Count: > 0 }
            || item.Slot.HasValue && rule.AllowedSlots.Contains(item.Slot.Value))
        && (!rule.RequiresShield || item.OffHandCategory == "SHIELD")
        && (!rule.RequiresWeaponDamage || item.WeaponDamageMin.HasValue && item.WeaponDamageMax.HasValue);

    public static bool IsCompatible(ItemAffixPoolDefinition pool, string statId, IEnumerable<string> occupied) =>
        !occupied.Contains(statId, StringComparer.Ordinal)
        && !(pool.ExclusiveStatGroups ?? []).Any(group =>
            group.Contains(statId, StringComparer.Ordinal) && occupied.Any(group.Contains));

    public static IReadOnlyList<string> GetCandidates(
        ItemDefinition item, ItemizationDefinition itemization, ItemAffixPoolDefinition pool,
        IEnumerable<string> occupied, int? itemLevel = null) =>
        pool.StatIds.Distinct(StringComparer.Ordinal)
            .Where(statId => IsCompatible(pool, statId, occupied))
            .Where(statId => item.GenerationVersion < 2
                || itemization.AffixRules?.FirstOrDefault(rule => rule.StatId == statId) is { } rule
                    && IsAllowed(item, rule, itemLevel))
            .ToArray();

    public static string SelectWeighted(ItemAffixPoolDefinition pool, IReadOnlyList<string> candidates, IGameRandom random, int itemLevel = 1)
    {
        if (candidates.Count == 0)
            throw new InvalidOperationException("Affix selection has no eligible candidate.");
        var levelWeights = pool.LevelSelectionWeights?.FirstOrDefault(band =>
            itemLevel >= band.MinimumItemLevel && itemLevel <= band.MaximumItemLevel)?.Weights;
        decimal Weight(string statId) => levelWeights?.GetValueOrDefault(statId,
            pool.SelectionWeights?.GetValueOrDefault(statId, 1m) ?? 1m)
            ?? pool.SelectionWeights?.GetValueOrDefault(statId, 1m) ?? 1m;
        decimal total = candidates.Sum(Weight);
        if (total <= 0 || candidates.Any(statId => Weight(statId) <= 0))
            throw new InvalidOperationException("Affix selection weights must be positive.");
        decimal roll = random.NextUnit() * total;
        foreach (string statId in candidates)
        {
            roll -= Weight(statId);
            if (roll < 0) return statId;
        }
        return candidates[^1];
    }

    // Every reachable roll must be completable, not merely the best combination.
    public static bool CanAlwaysFill(ItemAffixPoolDefinition pool, IReadOnlyList<string> candidates,
        IReadOnlyList<string> occupied, int remaining)
    {
        if (remaining <= 0) return true;
        string[] available = candidates.Where(statId => IsCompatible(pool, statId, occupied)).ToArray();
        if (pool.ExclusiveStatGroups is not { Count: > 0 }) return available.Length >= remaining;
        string[][] groups = pool.ExclusiveStatGroups
            .Select(group => available.Where(group.Contains).ToArray())
            .Where(group => group.Length > 1).ToArray();
        string[] grouped = groups.SelectMany(group => group).ToArray();
        if (grouped.Distinct(StringComparer.Ordinal).Count() == grouped.Length)
            return available.Length - grouped.Length + groups.Length >= remaining;
        Dictionary<(int Remaining, string Available), bool> results = [];
        return CanFill(available, remaining);

        bool CanFill(string[] choices, int count)
        {
            if (count <= 0) return true;
            if (choices.Length < count) return false;
            // Different roll orders reach the same remaining candidate set.
            // Validate that state once instead of exploring every permutation.
            var key = (count, string.Join('\0', choices));
            if (results.TryGetValue(key, out bool cached)) return cached;
            bool valid = choices.All(statId => CanFill(
                choices.Where(candidate => IsCompatible(pool, candidate, [statId])).ToArray(), count - 1));
            results[key] = valid;
            return valid;
        }
    }
}
