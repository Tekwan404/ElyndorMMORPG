namespace Elyndor.Core.Talents;

public static class PaladinTalentRuntimeCatalog
{
    // Audited, stable node identities. A new PALADIN_* hook is never implicitly
    // accepted as implemented solely because it carries the correct prefix.
    // Known partially implemented contracts are tracked in paladin-runtime-contracts-p0.
    private static readonly HashSet<string> RegisteredEventTalentIds =
    [
        "H-1-2",
        "H-1-3",
        "H-1-4",
        "H-2-1",
        "H-2-2",
        "H-2-3",
        "H-2-4",
        "H-3-1",
        "H-3-2",
        "H-3-3",
        "H-3-4",
        "H-4-1",
        "H-4-2",
        "H-4-3",
        "H-4-4",
        "H-5-1",
        "H-5-2",
        "H-5-3",
        "H-5-4",
        "H-6-1",
        "H-6-2",
        "H-6-3",
        "H-6-4",
        "H-7-1",
        "H-7-2",
        "H-7-3",
        "H-7-4",
        "H-8-1",
        "H-8-2",
        "H-8-3",
        "H-9-1",
        "P-1-1",
        "P-1-3",
        "P-1-4",
        "P-2-1",
        "P-2-3",
        "P-2-4",
        "P-3-1",
        "P-3-2",
        "P-3-3",
        "P-3-4",
        "P-4-1",
        "P-4-3",
        "P-4-4",
        "P-5-1",
        "P-5-2",
        "P-5-3",
        "P-5-4",
        "P-6-1",
        "P-6-2",
        "P-6-3",
        "P-6-4",
        "P-7-1",
        "P-7-2",
        "P-7-3",
        "P-7-4",
        "P-8-1",
        "P-8-2",
        "P-8-3",
        "P-9-1",
        "R-1-2",
        "R-1-3",
        "R-1-4",
        "R-2-1",
        "R-2-2",
        "R-2-3",
        "R-2-4",
        "R-3-1",
        "R-3-2",
        "R-3-3",
        "R-3-4",
        "R-4-1",
        "R-4-2",
        "R-4-3",
        "R-4-4",
        "R-5-1",
        "R-5-2",
        "R-5-4",
        "R-6-1",
        "R-6-2",
        "R-6-3",
        "R-6-4",
        "R-7-1",
        "R-7-2",
        "R-7-3",
        "R-7-4",
        "R-8-1",
        "R-8-2",
        "R-8-3",
        "R-9-1",
    ];

    private static readonly Dictionary<string, string[]> UnlockedAbilities =
        new(StringComparer.Ordinal)
        {
            ["H-1-4"] = ["BLESSING_OF_WISDOM"],
            ["H-2-4"] = ["CLEANSE"],
            ["H-3-1"] = ["DIVINE_FAVOR"],
            ["H-4-1"] = ["HOLY_SHOCK", "HOLY_SHOCK_OFFENSIVE"],
            ["H-5-2"] = ["BEACON_OF_LIGHT"],
            ["H-5-4"] = ["CONCENTRATION_AURA"],
            ["H-6-3"] = ["DIVINE_REPLENISHMENT"],
            ["H-6-4"] = ["BLESSING_OF_PROTECTION"],
            ["H-7-4"] = ["AURA_MASTERY"],
            ["P-2-3"] = ["CONSECRATION"],
            ["P-3-1"] = ["HOLY_SHIELD"],
            ["P-3-3"] = ["BLESSING_OF_SANCTUARY"],
            ["P-5-1"] = ["AVENGERS_SHIELD"],
            ["P-6-1"] = ["HAMMER_OF_THE_RIGHTEOUS"],
            ["P-6-4"] = ["DIVINE_PROTECTION"],
            ["P-7-3"] = ["INTERCESSION"],
            ["P-7-4"] = ["HAMMER_OF_JUSTICE"],
            ["R-1-4"] = ["SEAL_OF_COMMAND"],
            ["R-2-2"] = ["CRUSADER_STRIKE"],
            ["R-3-3"] = ["CONSECRATION"],
            ["R-4-1"] = ["SANCTITY_AURA"],
            ["R-4-2"] = ["REPENTANCE"],
            ["R-5-1"] = ["TEMPLARS_VERDICT"],
            ["R-5-4"] = ["BLESSING_OF_MIGHT"],
            ["R-6-2"] = ["DIVINE_STORM"],
            ["R-7-1"] = ["AVENGING_WRATH"]
        };

    public static bool OwnsTalentId(string talentId) =>
        talentId.StartsWith("H-", StringComparison.Ordinal)
        || talentId.StartsWith("P-", StringComparison.Ordinal)
        || talentId.StartsWith("R-", StringComparison.Ordinal);

    public static bool SupportsRuntime(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        OwnsTalentId(node.Id)
        && RegisteredEventTalentIds.Contains(node.Id)
        && modifier.Type == TalentModifierType.EventTriggered
        && string.Equals(modifier.TargetId,
            "PALADIN_" + node.Id.Replace('-', '_'), StringComparison.Ordinal);

    // The original 96-node design contract was authored with Deferred markers while
    // the runtime was being built. Once the Paladin runtime owns a target, the marker
    // is treated as executable legacy metadata so the tree can be published without
    // rewriting or losing the approved node contract.
    public static bool SupportsLegacyDeferred(
        TalentDefinition node,
        TalentModifierDefinition modifier) =>
        modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred
        && SupportsRuntime(node, modifier);

    public static IReadOnlyList<string> ResolveUnlockedAbilityIds(string talentId) =>
        UnlockedAbilities.TryGetValue(talentId, out string[]? abilityIds)
            ? abilityIds
            : [];
}
