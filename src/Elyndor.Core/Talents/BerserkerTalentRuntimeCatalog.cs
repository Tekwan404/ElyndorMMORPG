namespace Elyndor.Core.Talents;

public sealed record BerserkerTalentRuntimeRule(
    string TalentId,
    string EventKey,
    IReadOnlyList<decimal> Values,
    string? TargetId = null,
    IReadOnlyList<decimal>? SecondaryValues = null,
    decimal Threshold = 0,
    decimal ChancePercent = 100,
    TimeSpan? Duration = null,
    TimeSpan? TickInterval = null,
    TimeSpan? InternalCooldown = null,
    bool CanTriggerFromProc = false)
{
    public decimal ValueForRank(int rank) => RankValue(Values, rank);

    public decimal SecondaryValueForRank(int rank) =>
        SecondaryValues is null ? 0 : RankValue(SecondaryValues, rank);

    private static decimal RankValue(IReadOnlyList<decimal> values, int rank)
    {
        if (rank <= 0 || rank > values.Count)
            throw new ArgumentOutOfRangeException(nameof(rank));
        return values[rank - 1];
    }
}

public static class BerserkerTalentRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, BerserkerTalentRuntimeRule> Rules =
        new Dictionary<string, BerserkerTalentRuntimeRule>(StringComparer.Ordinal)
        {
            ["B-2-1"] = new("B-2-1", TalentModifierKeys.OnHpThreshold, [7, 12],
                SecondaryValues: [4, 8], Threshold: 50),
            ["B-2-4"] = new("B-2-4", TalentModifierKeys.OnAbilityUsed, [2, 4, 6],
                Threshold: 20, Duration: TimeSpan.FromSeconds(3)),
            ["B-3-4"] = new("B-3-4", TalentModifierKeys.OnCriticalHit, [4, 7],
                TargetId: "WILD_STRIKE", Duration: TimeSpan.FromSeconds(4),
                TickInterval: TimeSpan.FromSeconds(1)),
            ["B-4-1"] = new("B-4-1", TalentModifierKeys.OnAutoAttack, [45],
                ChancePercent: 15, InternalCooldown: TimeSpan.FromSeconds(2)),
            ["B-4-4"] = new("B-4-4", TalentModifierKeys.OnHpThreshold, [2, 4],
                SecondaryValues: [1, 2], Threshold: 50),
            ["B-5-4"] = new("B-5-4", TalentModifierKeys.OnAbilityUsed, [10, 20],
                TargetId: "BERSERK"),
            ["B-6-2"] = new("B-6-2", TalentModifierKeys.OnCriticalHit, [5],
                TargetId: "AUTO_ATTACK", Duration: TimeSpan.FromSeconds(8)),
            ["B-6-3"] = new("B-6-3", TalentModifierKeys.OnDamageTaken, [3, 5],
                TargetId: "BERSERK"),
            ["B-6-4"] = new("B-6-4", TalentModifierKeys.OnCriticalHit, [1, 2, 3],
                TargetId: "BERSERK", InternalCooldown: TimeSpan.FromSeconds(3)),
            ["B-7-1"] = new("B-7-1", TalentModifierKeys.OnAutoAttack, [30],
                TargetId: "BERSERK"),
            ["B-7-2"] = new("B-7-2", TalentModifierKeys.OnHpThreshold, [3, 6, 9, 12],
                Threshold: 25),
            ["B-7-3"] = new("B-7-3", TalentModifierKeys.OnAbilityUsed, [5, 8],
                TargetId: "WHIRLWIND", Duration: TimeSpan.FromSeconds(6),
                TickInterval: TimeSpan.FromSeconds(1)),
            ["B-7-4"] = new("B-7-4", TalentModifierKeys.OnHpThreshold, [5, 10],
                Threshold: 20),
            ["B-8-1"] = new("B-8-1", TalentModifierKeys.OnAbilityUsed, [15],
                TargetId: "WHIRLWIND"),
            ["B-8-2"] = new("B-8-2", TalentModifierKeys.OnEnemyKilled, [0, 0],
                TargetId: "WILD_STRIKE"),
            ["B-8-3"] = new("B-8-3", TalentModifierKeys.OnHpThreshold, [200],
                Threshold: 10),
            ["B-9-1"] = new("B-9-1", TalentModifierKeys.OnCriticalHit, [10],
                ChancePercent: 20)
        };

    public static bool TryGetRule(string talentId, out BerserkerTalentRuntimeRule rule) =>
        Rules.TryGetValue(talentId, out rule!);

    public static bool SupportsLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier)
    {
        return string.Equals(node.BranchId, "BERSERKER", StringComparison.Ordinal)
               && modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
               && string.Equals(
                   modifier.DeferredOwner,
                   TalentRuntimeOwners.CombatSession,
                   StringComparison.Ordinal)
               && Rules.TryGetValue(node.Id, out BerserkerTalentRuntimeRule? rule)
               && string.Equals(modifier.Key, rule.EventKey, StringComparison.Ordinal);
    }

    public static bool TryResolveLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier,
        int rank,
        out ResolvedTalentEventHook hook)
    {
        hook = null!;
        if (!SupportsLegacyDeferred(node, modifier)
            || !Rules.TryGetValue(node.Id, out BerserkerTalentRuntimeRule? rule)
            || rank <= 0
            || rank > rule.Values.Count)
        {
            return false;
        }

        hook = new ResolvedTalentEventHook(
            node.Id,
            rule.EventKey,
            rank,
            rule.ValueForRank(rank),
            rule.TargetId,
            rule.InternalCooldown ?? TimeSpan.Zero,
            rule.CanTriggerFromProc);
        return true;
    }

    public static IReadOnlyCollection<string> SupportedTalentIds => Rules.Keys.ToArray();
}
