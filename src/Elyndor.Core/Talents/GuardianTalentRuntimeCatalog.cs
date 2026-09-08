namespace Elyndor.Core.Talents;

public static class GuardianTalentRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RuntimeKeys =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["G-1-2"] = Keys(TalentModifierKeys.OnDamageTaken),
            ["G-1-4"] = Keys(TalentModifierKeys.OnAutoAttack),
            ["G-2-1"] = Keys(TalentModifierKeys.OnHpThreshold),
            ["G-2-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-2-4"] = Keys(TalentModifierKeys.OnHpThreshold),
            ["G-3-1"] = Keys(TalentModifierKeys.OnDodge),
            ["G-3-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-3-4"] = Keys(TalentModifierKeys.OnDamageTaken),
            ["G-4-1"] = Keys(TalentModifierKeys.OnHpThreshold),
            ["G-4-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-4-4"] = Keys(TalentModifierKeys.OnDodge),
            ["G-5-2"] = Keys(TalentModifierKeys.OnDamageTaken),
            ["G-6-1"] = Keys(TalentModifierKeys.OnAutoAttack),
            ["G-6-2"] = Keys(TalentModifierKeys.OnHpThreshold),
            ["G-6-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-6-4"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-7-2"] = Keys(TalentModifierKeys.OnHpThreshold),
            ["G-7-4"] = Keys(TalentModifierKeys.OnHpThreshold),
            ["G-8-1"] = Keys(TalentModifierKeys.OnDamageTaken),
            ["G-8-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
            ["G-8-3"] = Keys(TalentModifierKeys.OnDamageTaken),
            ["G-9-1"] = Keys(TalentModifierKeys.OnDamageTaken)
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
