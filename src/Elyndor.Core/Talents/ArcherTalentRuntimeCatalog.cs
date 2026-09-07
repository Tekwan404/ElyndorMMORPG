namespace Elyndor.Core.Talents;

public static class ArcherTalentRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> EventKeys =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["M-1-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["M-1-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["M-2-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["M-2-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_HP_THRESHOLD" },
            ["M-3-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["M-3-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_CRITICAL_HIT" },
            ["M-4-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_AUTO_ATTACK" },
            ["M-4-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["M-4-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["M-4-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_CRITICAL_HIT" },
            ["M-5-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["M-5-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["M-6-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_CRITICAL_HIT" },
            ["M-6-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_HP_THRESHOLD" },
            ["M-6-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_DAMAGE_TAKEN" },
            ["M-7-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["M-7-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_CRITICAL_HIT" },
            ["M-7-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["M-7-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ENEMY_KILLED" },
            ["M-8-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_HP_THRESHOLD" },
            ["M-8-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["M-9-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED", "ON_CRITICAL_HIT" },
            ["B-1-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-1-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-1-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-2-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-2-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-3-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_CRITICAL_HIT" },
            ["B-3-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_DAMAGE_TAKEN" },
            ["B-3-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-3-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-4-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_CRITICAL_HIT" },
            ["B-4-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-4-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["B-5-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_DAMAGE_TAKEN" },
            ["B-5-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-6-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_HP_THRESHOLD" },
            ["B-6-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-6-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["B-6-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-7-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-7-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_DAMAGE_TAKEN" },
            ["B-7-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-7-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-8-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["B-8-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["B-8-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["B-9-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT", "ON_CRITICAL_HIT" },
            ["A-1-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["A-1-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["A-2-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["A-2-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["A-3-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["A-3-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["A-3-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["A-4-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_CRITICAL_HIT" },
            ["A-4-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_CRITICAL_HIT" },
            ["A-4-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["A-5-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["A-5-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["A-5-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_CRITICAL_HIT" },
            ["A-6-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["A-6-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_DAMAGE_TAKEN" },
            ["A-6-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_HP_THRESHOLD" },
            ["A-7-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["A-7-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_HP_THRESHOLD" },
            ["A-7-4"] = new HashSet<string>(StringComparer.Ordinal) { "ON_HP_THRESHOLD" },
            ["A-8-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["A-8-2"] = new HashSet<string>(StringComparer.Ordinal) { "ON_PARTY_EVENT" },
            ["A-8-3"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" },
            ["A-9-1"] = new HashSet<string>(StringComparer.Ordinal) { "ON_ABILITY_USED" }
        };

    public static bool OwnsTalentId(string talentId) =>
        EventKeys.ContainsKey(talentId);

    public static bool SupportsLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        node.BranchId is "MARKSMAN" or "BEAST_MASTERY" or "ARCANE_ARCHER"
        && modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
        && string.Equals(
            modifier.DeferredOwner,
            TalentRuntimeOwners.CombatSession,
            StringComparison.Ordinal)
        && EventKeys.TryGetValue(node.Id, out IReadOnlySet<string>? expectedKeys)
        && expectedKeys.Contains(modifier.Key);

    public static IReadOnlyCollection<string> SupportedTalentIds =>
        EventKeys.Keys.ToArray();
}
