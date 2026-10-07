using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

/// <summary>Content contracts owned by the existing player class runtimes.</summary>
internal static class PlayerCombatMechanicsCapabilities
{
    internal static bool HasClassAbilityHandler(string id, string classId) =>
        HasClassAbilityHandler(id) && classId == (id.StartsWith("MAGE_", StringComparison.Ordinal)
            ? "MAGE" : id is "CHALLENGING_SHOUT" or "CRY_OF_VENGEANCE" ? "WARRIOR" : "ARCHER");

    internal static bool HasClassAbilityHandler(string id) => id is
        "MAGE_COMBUSTION" or "MAGE_MANA_SHIELD" or "MAGE_COUNTERSPELL" or "MAGE_PRESENCE_OF_MIND"
        or "MAGE_ARCANE_POWER" or "MAGE_EVOCATION" or "MAGE_FROST_NOVA"
        or "MAGE_BLIZZARD" or "MAGE_COLD_SNAP" or "MAGE_ICE_BLOCK"
        or "MAGE_CONE_OF_COLD" or "MAGE_ICE_BARRIER" or "MAGE_ICE_LANCE"
        or "SNIPER_FOCUS" or "HEAVY_ARROW" or "SERPENT_STING"
        or "FREEZING_TRAP" or "IMMOLATION_TRAP" or "EXPLOSIVE_TRAP"
        or "DETERRENCE" or "WYVERN_STING" or "PREPARATION"
        or "CHALLENGING_SHOUT" or "CRY_OF_VENGEANCE";

    internal static bool SupportsHook(string classId, ResolvedTalentEventHook hook)
    {
        if (string.IsNullOrEmpty(hook.TalentId))
            return false;
        string? branch = classId switch
        {
            "WARRIOR" => hook.TalentId[0] switch
            { 'B' => "BERSERKER", 'G' => "GUARDIAN", 'W' => "WARLORD", _ => null },
            "MAGE" => hook.TalentId[0] switch
            { 'F' => "FIRE", 'A' => "ARCANE", 'I' => "FROST", _ => null },
            "ARCHER" => hook.TalentId[0] switch
            { 'M' => "MARKSMAN", 'B' => "BEAST_MASTERY", 'S' => "SURVIVAL", _ => null },
            "PALADIN" => hook.TalentId[0] switch
            { 'H' => "HOLY", 'P' => "PROTECTION", 'R' => "RETRIBUTION", _ => null },
            _ => null
        };
        if (branch is null)
            return false;
        var node = new TalentDefinition(hook.TalentId, branch, 1, 0, hook.TalentId,
            hook.TalentId, hook.Rank, [], "");
        var modifier = new TalentModifierDefinition(TalentModifierType.EventTriggered,
            hook.Key, [hook.Value], hook.TargetId);
        return TalentRuntimeAvailability.IsModifierSupported(node, modifier);
    }
}
