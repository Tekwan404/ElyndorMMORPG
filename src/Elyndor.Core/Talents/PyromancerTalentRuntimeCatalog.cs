namespace Elyndor.Core.Talents;

public static class PyromancerTalentRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, string> EventKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["F-1-1"] = TalentModifierKeys.OnAbilityUsed,
            ["F-1-2"] = TalentModifierKeys.OnAbilityUsed,
            ["F-1-3"] = TalentModifierKeys.OnAbilityUsed,
            ["F-1-4"] = TalentModifierKeys.OnCriticalHit,
            ["F-2-1"] = TalentModifierKeys.OnAbilityUsed,
            ["F-2-2"] = TalentModifierKeys.OnCriticalHit,
            ["F-2-3"] = TalentModifierKeys.OnHpThreshold,
            ["F-2-4"] = TalentModifierKeys.OnAbilityUsed,
            ["F-3-2"] = TalentModifierKeys.OnAbilityUsed,
            ["F-3-3"] = TalentModifierKeys.OnCriticalHit,
            ["F-3-4"] = TalentModifierKeys.OnAbilityUsed,
            ["F-4-2"] = TalentModifierKeys.OnCriticalHit,
            ["F-4-3"] = TalentModifierKeys.OnAbilityUsed,
            ["F-4-4"] = TalentModifierKeys.OnHpThreshold,
            ["F-5-1"] = TalentModifierKeys.OnAbilityUsed,
            ["F-5-2"] = TalentModifierKeys.OnAbilityUsed,
            ["F-5-3"] = TalentModifierKeys.OnCriticalHit,
            ["F-5-4"] = TalentModifierKeys.OnAbilityUsed,
            ["F-6-1"] = TalentModifierKeys.OnCriticalHit,
            ["F-6-2"] = TalentModifierKeys.OnAbilityUsed,
            ["F-6-3"] = TalentModifierKeys.OnAbilityUsed,
            ["F-6-4"] = TalentModifierKeys.OnDamageTaken,
            ["F-7-1"] = TalentModifierKeys.OnCriticalHit,
            ["F-7-2"] = TalentModifierKeys.OnCriticalHit,
            ["F-7-3"] = TalentModifierKeys.OnAbilityUsed,
            ["F-7-4"] = TalentModifierKeys.OnEnemyKilled,
            ["F-8-1"] = TalentModifierKeys.OnAbilityUsed,
            ["F-8-2"] = TalentModifierKeys.OnAbilityUsed,
            ["F-8-3"] = TalentModifierKeys.OnHpThreshold,
            ["F-9-1"] = TalentModifierKeys.OnAbilityUsed
        };

    public static bool TryGetEventKey(string talentId, out string eventKey) =>
        EventKeys.TryGetValue(talentId, out eventKey!);

    public static bool SupportsLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        string.Equals(node.BranchId, "FIRE", StringComparison.Ordinal)
        && modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
        && string.Equals(
            modifier.DeferredOwner,
            TalentRuntimeOwners.CombatSession,
            StringComparison.Ordinal)
        && EventKeys.TryGetValue(node.Id, out string? expectedKey)
        && string.Equals(modifier.Key, expectedKey, StringComparison.Ordinal);

    public static IReadOnlyCollection<string> SupportedTalentIds => EventKeys.Keys.ToArray();
}
