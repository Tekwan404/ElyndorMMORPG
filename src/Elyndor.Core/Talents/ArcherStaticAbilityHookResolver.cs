using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;

namespace Elyndor.Core.Talents;

/// <summary>
/// Applies Archer event hooks whose complete runtime behaviour is a stateless
/// transformation of an ability definition. Hooks that also affect auto-attacks,
/// target state, procs or companions are intentionally excluded.
/// </summary>
public static class ArcherStaticAbilityHookResolver
{
    public const string EfficiencyTalentId = "M-1-2";
    public const string FlawlessAimTalentId = "M-4-2";

    private const string EfficiencyTargetId = "PHYSICAL_FOCUS_COST";
    private const string FlawlessAimTargetId = "AIMED_ACCURACY_CRIT";
    private const string AimedShotId = "AIMED_SHOT";

    public static bool Supports(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        if (!string.Equals(hook.Key, TalentModifierKeys.OnAbilityUsed, StringComparison.Ordinal))
            return false;

        return hook.TalentId switch
        {
            EfficiencyTalentId => string.Equals(hook.TargetId, EfficiencyTargetId, StringComparison.Ordinal),
            FlawlessAimTalentId => string.Equals(hook.TargetId, FlawlessAimTargetId, StringComparison.Ordinal),
            _ => false
        };
    }

    public static AbilityDefinition Apply(
        AbilityDefinition ability,
        ResolvedTalentModifiers talents)
    {
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(talents);

        decimal resourceCost = ability.ResourceCost;
        decimal accuracyBonus = ability.AccuracyBonus;
        decimal criticalChanceBonus = ability.CriticalChanceBonus;

        if (ability.ResourceCost > 0
            && !ability.IsSpell
            && TryGet(talents, EfficiencyTalentId, out ResolvedTalentEventHook efficiency))
        {
            resourceCost *= Math.Max(0, 1 - efficiency.Value / 100m);
        }

        if (IsPhysicalShotAbility(ability)
            && string.Equals(ability.Id, AimedShotId, StringComparison.Ordinal)
            && TryGet(talents, FlawlessAimTalentId, out ResolvedTalentEventHook flawlessAim))
        {
            accuracyBonus += flawlessAim.Value;
            criticalChanceBonus += flawlessAim.SecondaryValue;
        }

        return ability with
        {
            ResourceCost = Math.Max(0, resourceCost),
            AccuracyBonus = accuracyBonus,
            CriticalChanceBonus = criticalChanceBonus
        };
    }

    private static bool TryGet(
        ResolvedTalentModifiers talents,
        string talentId,
        out ResolvedTalentEventHook hook)
    {
        ResolvedTalentEventHook? resolved = talents.EventHooks.FirstOrDefault(candidate =>
            string.Equals(candidate.TalentId, talentId, StringComparison.Ordinal)
            && Supports(candidate));
        hook = resolved!;
        return resolved is not null;
    }

    private static bool IsPhysicalShotAbility(AbilityDefinition ability) =>
        ability.Actions?.Any(action =>
            action.Type == AbilityActionType.Damage
            && action.DamageType == DamageType.Physical) == true;
}
