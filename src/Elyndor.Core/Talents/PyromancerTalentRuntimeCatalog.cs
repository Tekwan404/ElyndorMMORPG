namespace Elyndor.Core.Talents;

public sealed record PyromancerTalentRuntimeRule(
    string TalentId,
    string EventKey,
    IReadOnlyList<decimal> Values,
    string? TargetId = null,
    IReadOnlyList<decimal>? SecondaryValues = null,
    decimal Threshold = 0,
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

public static class PyromancerTalentRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, PyromancerTalentRuntimeRule> Rules =
        new Dictionary<string, PyromancerTalentRuntimeRule>(StringComparer.Ordinal)
        {
            ["F-1-1"] = new("F-1-1", TalentModifierKeys.OnAbilityUsed, [2, 4, 6, 8], "FIRE_ACCURACY"),
            ["F-1-2"] = new("F-1-2", TalentModifierKeys.OnAbilityUsed, [2, 4, 6, 8], "FIRE_CRITICAL_CHANCE"),
            ["F-1-3"] = new("F-1-3", TalentModifierKeys.OnAbilityUsed, [3, 6, 9, 12], "FIRE_SPELL_POWER"),
            ["F-1-4"] = new("F-1-4", TalentModifierKeys.OnCriticalHit, [2, 4],
                InternalCooldown: TimeSpan.FromSeconds(1)),
            ["F-2-1"] = new("F-2-1", TalentModifierKeys.OnAbilityUsed, [4, 8, 12], "MAGE_FIREBALL"),
            ["F-2-2"] = new("F-2-2", TalentModifierKeys.OnCriticalHit, [4, 7], "MAGE_FIREBALL",
                Duration: TimeSpan.FromSeconds(4), TickInterval: TimeSpan.FromSeconds(1)),
            ["F-2-3"] = new("F-2-3", TalentModifierKeys.OnHpThreshold, [4, 8], "ENEMY_ABOVE", Threshold: 80),
            ["F-2-4"] = new("F-2-4", TalentModifierKeys.OnAbilityUsed, [0.10m, 0.20m], "MAGE_FIREBALL",
                Duration: TimeSpan.FromSeconds(5)),
            ["F-3-2"] = new("F-3-2", TalentModifierKeys.OnAbilityUsed, [2, 4, 6], "BURN"),
            ["F-3-3"] = new("F-3-3", TalentModifierKeys.OnCriticalHit, [10, 20], "MAGE_FIREBALL",
                Duration: TimeSpan.FromSeconds(5)),
            ["F-3-4"] = new("F-3-4", TalentModifierKeys.OnAbilityUsed, [3, 6, 9, 12], "FIRE_MAGIC_PENETRATION"),
            ["F-4-2"] = new("F-4-2", TalentModifierKeys.OnCriticalHit, [10, 20], "BURN"),
            ["F-4-3"] = new("F-4-3", TalentModifierKeys.OnAbilityUsed, [4, 8], "FIRE_RHYTHM"),
            ["F-4-4"] = new("F-4-4", TalentModifierKeys.OnHpThreshold, [5, 10], "ENEMY_BELOW", Threshold: 25),
            ["F-5-1"] = new("F-5-1", TalentModifierKeys.OnAbilityUsed, [15], "COMBUSTION",
                SecondaryValues: [8], Duration: TimeSpan.FromSeconds(10)),
            ["F-5-2"] = new("F-5-2", TalentModifierKeys.OnAbilityUsed, [8, 15], "MAGE_FIREBALL",
                Duration: TimeSpan.FromSeconds(5)),
            ["F-5-3"] = new("F-5-3", TalentModifierKeys.OnCriticalHit, [0.5m, 1, 1.5m, 2], "FLAME_FLASH",
                InternalCooldown: TimeSpan.FromSeconds(1)),
            ["F-5-4"] = new("F-5-4", TalentModifierKeys.OnAbilityUsed, [8, 15], "FIRE_CRITICAL_DAMAGE"),
            ["F-6-1"] = new("F-6-1", TalentModifierKeys.OnCriticalHit, [3], "MAGE_FIREBALL",
                Duration: TimeSpan.FromSeconds(8)),
            ["F-6-2"] = new("F-6-2", TalentModifierKeys.OnAbilityUsed, [5, 10], "FIRE_COMET",
                SecondaryValues: [4, 7]),
            ["F-6-3"] = new("F-6-3", TalentModifierKeys.OnAbilityUsed, [0.15m, 0.30m], "MAGE_FIREBALL"),
            ["F-6-4"] = new("F-6-4", TalentModifierKeys.OnDamageTaken, [5, 10], "MAGICAL_CRITICAL",
                Duration: TimeSpan.FromSeconds(6), InternalCooldown: TimeSpan.FromSeconds(8)),
            ["F-7-1"] = new("F-7-1", TalentModifierKeys.OnCriticalHit, [35], "FIRE_COMET",
                Duration: TimeSpan.FromSeconds(1), TickInterval: TimeSpan.FromSeconds(1)),
            ["F-7-2"] = new("F-7-2", TalentModifierKeys.OnCriticalHit, [5, 10], "MAGE_FIREBALL",
                Duration: TimeSpan.FromSeconds(6)),
            ["F-7-3"] = new("F-7-3", TalentModifierKeys.OnAbilityUsed, [2, 3, 4], "COMBUSTION"),
            ["F-7-4"] = new("F-7-4", TalentModifierKeys.OnEnemyKilled, [5, 8], "FIRE",
                InternalCooldown: TimeSpan.FromSeconds(8)),
            ["F-8-1"] = new("F-8-1", TalentModifierKeys.OnAbilityUsed, [15], "COMBUSTION"),
            ["F-8-2"] = new("F-8-2", TalentModifierKeys.OnAbilityUsed, [5, 10], "BURN"),
            ["F-8-3"] = new("F-8-3", TalentModifierKeys.OnHpThreshold, [20], "FIRE_COMET", Threshold: 30),
            ["F-9-1"] = new("F-9-1", TalentModifierKeys.OnAbilityUsed, [8], "FIRE",
                SecondaryValues: [3], InternalCooldown: TimeSpan.FromSeconds(6))
        };

    public static bool TryGetRule(string talentId, out PyromancerTalentRuntimeRule rule) =>
        Rules.TryGetValue(talentId, out rule!);

    public static bool SupportsLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        string.Equals(node.BranchId, "FIRE", StringComparison.Ordinal)
        && modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
        && string.Equals(modifier.DeferredOwner, TalentRuntimeOwners.CombatSession, StringComparison.Ordinal)
        && Rules.TryGetValue(node.Id, out PyromancerTalentRuntimeRule? rule)
        && string.Equals(modifier.Key, rule.EventKey, StringComparison.Ordinal);

    public static bool TryResolveLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier,
        int rank,
        out ResolvedTalentEventHook hook)
    {
        hook = null!;
        if (!SupportsLegacyDeferred(node, modifier)
            || !Rules.TryGetValue(node.Id, out PyromancerTalentRuntimeRule? rule)
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
