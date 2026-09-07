namespace Elyndor.Core.Talents;

public static class WarlordTalentRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RuntimeKeys =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["W-1-1"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-1-2"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-1-4"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-2-1"] = Keys(TalentModifierKeys.UnlockAbility),
            ["W-2-2"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-2-4"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-3-1"] = Keys(TalentModifierKeys.UnlockAbility),
            ["W-3-2"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-3-3"] = Keys(TalentModifierKeys.UnlockAbility),
            ["W-3-4"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-4-1"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-4-2"] = Keys(TalentModifierKeys.UnlockAbility),
            ["W-4-3"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-4-4"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-5-1"] = Keys(TalentModifierKeys.UnlockAbility),
            ["W-5-2"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-5-3"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-5-4"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-6-1"] = Keys(TalentModifierKeys.UnlockAbility),
            ["W-6-2"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-6-3"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-6-4"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-7-1"] = Keys(TalentModifierKeys.UnlockAbility),
            ["W-7-2"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-7-3"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-7-4"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-8-1"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-8-2"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-8-3"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-8-4"] = Keys(TalentModifierKeys.OnPartyEvent),
            ["W-9-1"] = Keys(TalentModifierKeys.OnPartyEvent)
        };

    public static bool SupportsLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        node.BranchId == "WARLORD"
        && modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
        && string.Equals(
            modifier.DeferredOwner,
            TalentRuntimeOwners.Party,
            StringComparison.Ordinal)
        && RuntimeKeys.TryGetValue(node.Id, out IReadOnlySet<string>? keys)
        && keys.Contains(modifier.Key);

    public static IReadOnlyCollection<string> SupportedTalentIds => RuntimeKeys.Keys.ToArray();

    private static HashSet<string> Keys(params string[] keys) =>
        new HashSet<string>(keys, StringComparer.Ordinal);
}
