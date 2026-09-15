namespace Elyndor.Core.Talents;
public static class ArcherTalentRuntimeCatalog
{
private static readonly Dictionary<string, IReadOnlySet<string>> EventKeysByTalentId =
new(StringComparer.Ordinal)
{
["M-1-1"] = Keys(TalentModifierKeys.OnAbilityUsed),
["M-1-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
["M-2-4"] = Keys(TalentModifierKeys.OnAutoAttack),
["M-3-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
["M-4-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
["M-4-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
["M-4-4"] = Keys(TalentModifierKeys.OnAbilityUsed),
["M-5-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
["M-5-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
["M-6-1"] = Keys(TalentModifierKeys.OnPartyEvent),
["M-6-2"] = Keys(TalentModifierKeys.OnCriticalHit),
["M-6-4"] = Keys(TalentModifierKeys.OnCriticalHit),
["M-7-1"] = Keys(TalentModifierKeys.OnAbilityUsed),
["M-7-2"] = Keys(TalentModifierKeys.OnCriticalHit),
["M-7-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
["M-8-1"] = Keys(TalentModifierKeys.OnHpThreshold),
["M-8-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
["M-9-1"] = Keys(TalentModifierKeys.OnAbilityUsed, TalentModifierKeys.OnCriticalHit),
["B-1-1"] = Keys(TalentModifierKeys.OnPartyEvent),
["B-1-2"] = Keys(TalentModifierKeys.OnPartyEvent),
["B-1-3"] = Keys(TalentModifierKeys.OnAutoAttack),
["B-1-4"] = Keys(TalentModifierKeys.OnPartyEvent),
["B-2-2"] = Keys(TalentModifierKeys.OnPartyEvent),
["B-2-3"] = Keys(TalentModifierKeys.OnPartyEvent),
["B-3-1"] = Keys(TalentModifierKeys.OnAbilityUsed),
["B-3-2"] = Keys(TalentModifierKeys.OnPartyEvent),
["B-3-3"] = Keys(TalentModifierKeys.OnPartyEvent),
["B-4-2"] = Keys(TalentModifierKeys.OnPartyEvent),
["B-4-4"] = Keys(TalentModifierKeys.OnCriticalHit),
["B-5-1"] = Keys(TalentModifierKeys.OnCriticalHit),
["B-5-3"] = Keys(TalentModifierKeys.OnDamageTaken),
["B-5-4"] = Keys(TalentModifierKeys.OnAbilityUsed),
["B-6-1"] = Keys(TalentModifierKeys.OnHpThreshold),
["B-6-2"] = Keys(TalentModifierKeys.OnDamageTaken),
["B-6-3"] = Keys(TalentModifierKeys.OnCriticalHit),
["B-7-1"] = Keys(TalentModifierKeys.OnPartyEvent),
["B-7-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
["B-7-3"] = Keys(TalentModifierKeys.OnCriticalHit),
["B-7-4"] = Keys(TalentModifierKeys.OnPartyEvent),
["B-8-1"] = Keys(TalentModifierKeys.OnPartyEvent),
["B-8-2"] = Keys(TalentModifierKeys.OnAbilityUsed),
["B-8-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
["B-9-1"] = Keys(TalentModifierKeys.OnCriticalHit, TalentModifierKeys.OnPartyEvent),
["S-1-2"] = Keys(TalentModifierKeys.OnDamageTaken),
["S-2-2"] = Keys(TalentModifierKeys.OnPartyEvent),
["S-2-4"] = Keys(TalentModifierKeys.OnPartyEvent),
["S-3-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
["S-3-4"] = Keys(TalentModifierKeys.OnPartyEvent),
["S-4-4"] = Keys(TalentModifierKeys.OnDamageTaken),
["S-5-2"] = Keys(TalentModifierKeys.OnPartyEvent),
["S-5-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
["S-5-4"] = Keys(TalentModifierKeys.OnDamageTaken),
["S-6-3"] = Keys(TalentModifierKeys.OnPartyEvent),
["S-6-4"] = Keys(TalentModifierKeys.OnHpThreshold),
["S-7-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
["S-7-4"] = Keys(TalentModifierKeys.OnPartyEvent),
["S-8-2"] = Keys(TalentModifierKeys.OnPartyEvent),
["S-8-3"] = Keys(TalentModifierKeys.OnAbilityUsed),
["S-9-1"] = Keys(TalentModifierKeys.OnAbilityUsed, TalentModifierKeys.OnPartyEvent),
};
public static bool OwnsTalentId(string talentId) =>
EventKeysByTalentId.ContainsKey(talentId);
public static bool TryGetEventKey(string talentId, out string eventKey)
{
if (EventKeysByTalentId.TryGetValue(talentId, out IReadOnlySet<string>? keys)
&& keys.Count > 0)
{
eventKey = keys.OrderBy(key => key, StringComparer.Ordinal).First();
return true;
}
eventKey = string.Empty;
return false;
}
public static bool SupportsLegacyDeferred(
TalentDefinition node,
TalentModifierDefinition modifier) =>
SupportsRuntime(node, modifier);
public static bool SupportsRuntime(
TalentDefinition node,
TalentModifierDefinition modifier) =>
node.BranchId is "MARKSMAN" or "BEAST_MASTERY" or "SURVIVAL"
&& modifier.Type == TalentModifierType.EventTriggered
&& EventKeysByTalentId.TryGetValue(node.Id, out IReadOnlySet<string>? keys)
&& keys.Contains(modifier.Key);
private static HashSet<string> Keys(params string[] keys) =>
new(keys, StringComparer.Ordinal);
}
