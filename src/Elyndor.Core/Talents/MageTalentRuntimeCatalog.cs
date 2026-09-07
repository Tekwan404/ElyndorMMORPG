namespace Elyndor.Core.Talents;

public static class MageTalentRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, string> EventKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A-1-4"] = TalentModifierKeys.OnAbilityUsed,
            ["A-2-1"] = TalentModifierKeys.OnAbilityUsed,
            ["A-2-2"] = TalentModifierKeys.OnAbilityUsed,
            ["A-2-3"] = TalentModifierKeys.OnHpThreshold,
            ["A-2-4"] = TalentModifierKeys.OnAbilityUsed,
            ["A-3-2"] = TalentModifierKeys.OnAbilityUsed,
            ["A-3-4"] = TalentModifierKeys.OnAbilityUsed,
            ["A-4-1"] = TalentModifierKeys.OnAbilityUsed,
            ["A-4-2"] = TalentModifierKeys.OnHpThreshold,
            ["A-4-3"] = TalentModifierKeys.OnHpThreshold,
            ["A-4-4"] = TalentModifierKeys.OnAbilityUsed,
            ["A-5-1"] = TalentModifierKeys.OnAbilityUsed,
            ["A-5-2"] = TalentModifierKeys.OnAbilityUsed,
            ["A-5-3"] = TalentModifierKeys.OnAbilityUsed,
            ["A-5-4"] = TalentModifierKeys.OnCriticalHit,
            ["A-6-1"] = TalentModifierKeys.OnAbilityUsed,
            ["A-6-2"] = TalentModifierKeys.OnAbilityUsed,
            ["A-6-3"] = TalentModifierKeys.OnAbilityUsed,
            ["A-7-1"] = TalentModifierKeys.OnAbilityUsed,
            ["A-7-2"] = TalentModifierKeys.OnAbilityUsed,
            ["A-7-3"] = TalentModifierKeys.OnAbilityUsed,
            ["A-7-4"] = TalentModifierKeys.OnHpThreshold,
            ["A-8-1"] = TalentModifierKeys.OnAbilityUsed,
            ["A-8-2"] = TalentModifierKeys.OnCriticalHit,
            ["A-8-3"] = TalentModifierKeys.OnAbilityUsed,
            ["A-9-1"] = TalentModifierKeys.OnAbilityUsed,

            ["I-1-1"] = TalentModifierKeys.OnAbilityUsed,
            ["I-1-2"] = TalentModifierKeys.OnAbilityUsed,
            ["I-1-4"] = TalentModifierKeys.OnAbilityUsed,
            ["I-2-1"] = TalentModifierKeys.OnAbilityUsed,
            ["I-2-2"] = TalentModifierKeys.OnAbilityUsed,
            ["I-2-3"] = TalentModifierKeys.OnDamageTaken,
            ["I-2-4"] = TalentModifierKeys.OnAbilityUsed,
            ["I-3-1"] = TalentModifierKeys.OnAbilityUsed,
            ["I-3-2"] = TalentModifierKeys.OnCriticalHit,
            ["I-3-3"] = TalentModifierKeys.OnAbilityUsed,
            ["I-3-4"] = TalentModifierKeys.OnAbilityUsed,
            ["I-4-1"] = TalentModifierKeys.OnAbilityUsed,
            ["I-4-2"] = TalentModifierKeys.OnAbilityUsed,
            ["I-4-3"] = TalentModifierKeys.OnDamageTaken,
            ["I-4-4"] = TalentModifierKeys.OnAbilityUsed,
            ["I-5-1"] = TalentModifierKeys.OnAbilityUsed,
            ["I-5-2"] = TalentModifierKeys.OnAbilityUsed,
            ["I-5-3"] = TalentModifierKeys.OnCriticalHit,
            ["I-5-4"] = TalentModifierKeys.OnHpThreshold,
            ["I-6-2"] = TalentModifierKeys.OnDamageTaken,
            ["I-6-3"] = TalentModifierKeys.OnAbilityUsed,
            ["I-6-4"] = TalentModifierKeys.OnDamageTaken,
            ["I-7-1"] = TalentModifierKeys.OnAbilityUsed,
            ["I-7-2"] = TalentModifierKeys.OnAbilityUsed,
            ["I-7-3"] = TalentModifierKeys.OnAbilityUsed,
            ["I-7-4"] = TalentModifierKeys.OnAbilityUsed,
            ["I-8-1"] = TalentModifierKeys.OnAbilityUsed,
            ["I-8-2"] = TalentModifierKeys.OnAbilityUsed,
            ["I-8-3"] = TalentModifierKeys.OnHpThreshold,
            ["I-9-1"] = TalentModifierKeys.OnAbilityUsed
        };

    public static bool TryGetEventKey(string talentId, out string eventKey) =>
        EventKeys.TryGetValue(talentId, out eventKey!);

    public static bool SupportsLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        node.BranchId is "ARCANE" or "FROST"
        && modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
        && string.Equals(
            modifier.DeferredOwner,
            TalentRuntimeOwners.CombatSession,
            StringComparison.Ordinal)
        && EventKeys.TryGetValue(node.Id, out string? expectedKey)
        && string.Equals(modifier.Key, expectedKey, StringComparison.Ordinal);

    public static IReadOnlyCollection<string> SupportedTalentIds => EventKeys.Keys.ToArray();
}
