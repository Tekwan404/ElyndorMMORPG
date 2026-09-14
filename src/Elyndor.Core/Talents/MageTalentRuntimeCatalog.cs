namespace Elyndor.Core.Talents;

public static class MageTalentRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, string> EventKeys = BuildEventKeys();

    private static Dictionary<string, string> BuildEventKeys()
    {
        Dictionary<string, string> keys = new(StringComparer.Ordinal);
        for (var tier = 1; tier <= 9; tier++)
        {
            int count = tier switch { 8 => 3, 9 => 1, _ => 4 };
            for (var slot = 1; slot <= count; slot++)
            {
                keys[$"A-{tier}-{slot}"] = TalentModifierKeys.OnAbilityUsed;
                keys[$"I-{tier}-{slot}"] = TalentModifierKeys.OnAbilityUsed;
            }
        }
        return keys;
    }

    public static bool TryGetEventKey(string talentId, out string eventKey) =>
        EventKeys.TryGetValue(talentId, out eventKey!);

    public static bool SupportsLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
        && string.Equals(modifier.DeferredOwner, TalentRuntimeOwners.CombatSession, StringComparison.Ordinal)
        && node.BranchId is "ARCANE" or "FROST"
        && EventKeys.TryGetValue(node.Id, out string? expectedKey)
        && string.Equals(modifier.Key, expectedKey, StringComparison.Ordinal);

    public static bool SupportsRuntime(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        node.BranchId is "ARCANE" or "FROST"
        && modifier.RuntimeStatus == TalentModifierRuntimeStatus.Supported
        && EventKeys.TryGetValue(node.Id, out string? expectedKey)
        && string.Equals(modifier.Key, expectedKey, StringComparison.Ordinal);

    public static IReadOnlyCollection<string> SupportedTalentIds => EventKeys.Keys.ToArray();
}
