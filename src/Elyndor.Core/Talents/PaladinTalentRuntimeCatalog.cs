namespace Elyndor.Core.Talents;

public static class PaladinTalentRuntimeCatalog
{
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
        && modifier.Type == TalentModifierType.EventTriggered
        && modifier.TargetId?.StartsWith("PALADIN_", StringComparison.Ordinal) == true;

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
