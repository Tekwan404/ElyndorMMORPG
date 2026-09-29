using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Talents;

/// <summary>
/// Applies Pyromancer event hooks whose complete runtime behaviour is a stateless
/// transformation of an ability definition. Stateful procs/effects stay in their
/// dedicated runtime handlers and are not accepted here.
/// </summary>
public static class PyromancerStaticAbilityHookResolver
{
    public const string ImprovedFireballTalentId = "F-1-1";
    public const string IncinerationTalentId = "F-1-2";
    public const string EfficientMagicTalentId = "F-1-4";
    public const string ImprovedFireBlastTalentId = "F-2-4";
    public const string CriticalMassTalentId = "F-3-4";
    public const string FirePowerTalentId = "F-5-2";

    private const string FireballId = "MAGE_FIREBALL";
    private const string FireBlastId = "MAGE_FIRE_BLAST";
    private const string ScorchId = "MAGE_SCORCH";

    private static readonly HashSet<string> SupportedTalentIds = new(StringComparer.Ordinal)
    {
        ImprovedFireballTalentId,
        IncinerationTalentId,
        EfficientMagicTalentId,
        ImprovedFireBlastTalentId,
        CriticalMassTalentId,
        FirePowerTalentId
    };

    public static bool Supports(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return string.Equals(hook.Key, TalentModifierKeys.OnAbilityUsed, StringComparison.Ordinal)
            && SupportedTalentIds.Contains(hook.TalentId);
    }

    public static AbilityDefinition Apply(
        AbilityDefinition ability,
        ResolvedTalentModifiers talents)
    {
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(talents);

        if (!string.Equals(ability.School, "FIRE", StringComparison.Ordinal))
            return ability;

        decimal damageMultiplier = ability.DamageMultiplier;
        decimal criticalChanceBonus = ability.CriticalChanceBonus;
        decimal resourceCost = ability.ResourceCost;
        TimeSpan castTime = ability.CastTime;
        TimeSpan cooldown = ability.Cooldown;

        if (TryGet(talents, EfficientMagicTalentId, out ResolvedTalentEventHook efficient))
            resourceCost *= Math.Max(0, 1 - efficient.Value / 100m);
        if (TryGet(talents, FirePowerTalentId, out ResolvedTalentEventHook firePower))
            damageMultiplier *= 1 + firePower.Value / 100m;
        if (TryGet(talents, CriticalMassTalentId, out ResolvedTalentEventHook criticalMass))
            criticalChanceBonus += criticalMass.Value;

        if (string.Equals(ability.Id, FireballId, StringComparison.Ordinal)
            && TryGet(talents, ImprovedFireballTalentId, out ResolvedTalentEventHook improvedFireball))
        {
            castTime = Reduce(castTime, improvedFireball.Value);
        }
        else if (string.Equals(ability.Id, ScorchId, StringComparison.Ordinal)
            && TryGet(talents, IncinerationTalentId, out ResolvedTalentEventHook incineration))
        {
            criticalChanceBonus += incineration.Value;
        }
        else if (string.Equals(ability.Id, FireBlastId, StringComparison.Ordinal))
        {
            if (TryGet(talents, IncinerationTalentId, out ResolvedTalentEventHook incineration))
                criticalChanceBonus += incineration.Value;
            if (TryGet(talents, ImprovedFireBlastTalentId, out ResolvedTalentEventHook improvedBlast))
            {
                cooldown = Reduce(cooldown, improvedBlast.Value);
                criticalChanceBonus += improvedBlast.SecondaryValue;
            }
        }

        return ability with
        {
            ResourceCost = Math.Max(0, resourceCost),
            Cooldown = cooldown,
            CastTime = castTime,
            DamageMultiplier = Math.Max(0, damageMultiplier),
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

    private static TimeSpan Reduce(TimeSpan value, decimal seconds) =>
        TimeSpan.FromSeconds(Math.Max(0, value.TotalSeconds - (double)seconds));
}
