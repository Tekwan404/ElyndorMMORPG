namespace Elyndor.Core.Talents;

public static class GuardianTalentRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RuntimeKeys =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["G-1-1"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-1-5"] = Keys(TalentModifierKeys.OnDamageTaken),
            ["G-2-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-2-4"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-2-5"] = Keys(TalentModifierKeys.OnDamageTaken),
            ["G-3-6"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-4-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-4-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-4-4"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-4-5"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-5-4"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-5-5"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["G-6-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-6-4"] = Keys(TalentModifierKeys.OnHpThreshold),
            ["G-6-5"] = Keys(TalentModifierKeys.OnDamageTaken)
        };

    public static bool SupportsLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
        && string.Equals(
            modifier.DeferredOwner,
            TalentRuntimeOwners.CombatSession,
            StringComparison.Ordinal)
        && node.BranchId == "GUARDIAN"
        && RuntimeKeys.TryGetValue(node.Id, out IReadOnlySet<string>? keys)
        && keys.Contains(modifier.Key);

    public static bool SupportsRuntime(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        node.BranchId == "GUARDIAN"
        && modifier.RuntimeStatus == TalentModifierRuntimeStatus.Supported
        && RuntimeKeys.TryGetValue(node.Id, out IReadOnlySet<string>? keys)
        && keys.Contains(modifier.Key);

    public static IReadOnlyCollection<string> SupportedTalentIds => RuntimeKeys.Keys.ToArray();

    private static HashSet<string> Keys(params string[] keys) =>
        new HashSet<string>(keys, StringComparer.Ordinal);
}
