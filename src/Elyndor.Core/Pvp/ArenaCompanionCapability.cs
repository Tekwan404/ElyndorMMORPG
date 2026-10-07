using Elyndor.Core.Talents;

namespace Elyndor.Core.Pvp;

/// <summary>
/// Pet-dependent Beast Mastery effects are dormant in the 1v1 arena until a
/// companion runtime exists. Keep this list explicit so new talents fail closed.
/// B-1-3 is deliberately absent: it buffs the owner's own auto-attacks.
/// </summary>
public static class ArenaCompanionCapability
{
    private static readonly HashSet<(string TalentId, string Key, string? TargetId)> CompanionHooks =
    [
        ("B-1-1", TalentModifierKeys.OnPartyEvent, "PET_MAX_HP"),
        ("B-1-2", TalentModifierKeys.OnPartyEvent, "PET_DAMAGE"),
        ("B-1-4", TalentModifierKeys.OnPartyEvent, "PET_FOCUS_PROC"),
        ("B-2-1", TalentModifierKeys.OnAbilityUsed, "COMMAND_ATTACK_DAMAGE"),
        ("B-2-2", TalentModifierKeys.OnPartyEvent, "PET_DAMAGE_REDUCTION"),
        ("B-2-3", TalentModifierKeys.OnPartyEvent, "PET_CRIT"),
        ("B-3-1", TalentModifierKeys.OnAbilityUsed, "MEND_PET_BONUS"),
        ("B-3-2", TalentModifierKeys.OnPartyEvent, "SHARED_TARGET_DAMAGE"),
        ("B-3-3", TalentModifierKeys.OnPartyEvent, "PET_SPECIAL_DAMAGE"),
        ("B-4-2", TalentModifierKeys.OnPartyEvent, "SPIRIT_BOND"),
        ("B-4-4", TalentModifierKeys.OnCriticalHit, "PET_CRIT_OWNER_SHOT"),
        ("B-5-1", TalentModifierKeys.OnCriticalHit, "PET_FRENZY"),
        ("B-5-3", TalentModifierKeys.OnDamageTaken, "PET_AOE_REDUCTION"),
        ("B-5-4", TalentModifierKeys.OnAbilityUsed, "COMMAND_PET_DAMAGE"),
        ("B-6-1", TalentModifierKeys.OnHpThreshold, "PET_EXECUTE"),
        ("B-6-2", TalentModifierKeys.OnDamageTaken, "PET_INTERCEPT"),
        ("B-6-3", TalentModifierKeys.OnCriticalHit, "OWNER_CRIT_PET_DAMAGE"),
        ("B-7-1", TalentModifierKeys.OnPartyEvent, "PERFECT_FRENZY"),
        ("B-7-2", TalentModifierKeys.OnAbilityUsed, "PRIMAL_COMMAND"),
        ("B-7-3", TalentModifierKeys.OnCriticalHit, "BLOOD_AND_FANG"),
        ("B-7-4", TalentModifierKeys.OnPartyEvent, "OWNER_HEAL_PET"),
        ("B-8-1", TalentModifierKeys.OnPartyEvent, "PET_CRIT_MASTERY"),
        ("B-8-2", TalentModifierKeys.OnAbilityUsed, "COMMAND_OWNER_SHOT"),
        ("B-8-3", TalentModifierKeys.OnAbilityUsed, "BESTIAL_WRATH_UNSTOPPABLE"),
        ("B-9-1", TalentModifierKeys.OnPartyEvent, "BEAST_MASTER_BASE"),
        ("B-9-1", TalentModifierKeys.OnCriticalHit, "BEAST_MASTER_EXTRA_ATTACK"),
        ("B-9-1", TalentModifierKeys.OnCriticalHit, "BEAST_MASTER_OWNER_SHOT")
    ];

    private static readonly HashSet<string> CompanionAbilityIds = new(StringComparer.Ordinal)
    {
        "COMMAND_ATTACK", "MEND_PET", "INTIMIDATION", "BESTIAL_WRATH", "RETURN_TO_OWNER"
    };

    public static bool RequiresCompanion(ResolvedTalentEventHook hook) =>
        CompanionHooks.Contains((hook.TalentId, hook.Key, hook.TargetId));

    public static bool RequiresCompanion(string abilityId) =>
        CompanionAbilityIds.Contains(abilityId);
}
