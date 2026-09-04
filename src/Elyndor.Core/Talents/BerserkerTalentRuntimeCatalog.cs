namespace Elyndor.Core.Talents;

public static class BerserkerTalentRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, string> EventKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["B-2-1"] = TalentModifierKeys.OnHpThreshold,
            ["B-2-4"] = TalentModifierKeys.OnAbilityUsed,
            ["B-3-4"] = TalentModifierKeys.OnCriticalHit,
            ["B-4-1"] = TalentModifierKeys.OnAutoAttack,
            ["B-4-4"] = TalentModifierKeys.OnHpThreshold,
            ["B-5-4"] = TalentModifierKeys.OnAbilityUsed,
            ["B-6-2"] = TalentModifierKeys.OnCriticalHit,
            ["B-6-3"] = TalentModifierKeys.OnDamageTaken,
            ["B-6-4"] = TalentModifierKeys.OnCriticalHit,
            ["B-7-1"] = TalentModifierKeys.OnAutoAttack,
            ["B-7-2"] = TalentModifierKeys.OnHpThreshold,
            ["B-7-3"] = TalentModifierKeys.OnAbilityUsed,
            ["B-7-4"] = TalentModifierKeys.OnHpThreshold,
            ["B-8-1"] = TalentModifierKeys.OnAbilityUsed,
            ["B-8-2"] = TalentModifierKeys.OnEnemyKilled,
            ["B-8-3"] = TalentModifierKeys.OnHpThreshold,
            ["B-9-1"] = TalentModifierKeys.OnCriticalHit
        };

    public static bool TryGetEventKey(string talentId, out string eventKey) =>
        EventKeys.TryGetValue(talentId, out eventKey!);

    public static bool SupportsLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        string.Equals(node.BranchId, "BERSERKER", StringComparison.Ordinal)
        && modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
        && string.Equals(
            modifier.DeferredOwner,
            TalentRuntimeOwners.CombatSession,
            StringComparison.Ordinal)
        && EventKeys.TryGetValue(node.Id, out string? expectedKey)
        && string.Equals(modifier.Key, expectedKey, StringComparison.Ordinal);

    public static IReadOnlyCollection<string> SupportedTalentIds => EventKeys.Keys.ToArray();
}
