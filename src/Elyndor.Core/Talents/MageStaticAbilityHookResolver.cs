using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Talents;

/// <summary>
/// Applies Mage Arcane/Frost event hooks whose complete runtime behaviour is a
/// stateless transformation of an ability definition. Stateful effects and proc
/// chains remain owned by the combat runtime and are not accepted here.
/// </summary>
public static class MageStaticAbilityHookResolver
{
    public const string ArcaneFocusTalentId = "A-1-1";
    public const string ImprovedArcaneMissilesTalentId = "A-2-3";
    public const string ImprovedArcaneExplosionTalentId = "A-3-1";
    public const string ArcaneInstabilityTalentId = "A-4-3";
    public const string QuickThinkingTalentId = "A-7-2";

    public const string ImprovedIceShardTalentId = "I-1-1";
    public const string FrostPrecisionTalentId = "I-1-2";
    public const string PiercingIceTalentId = "I-1-3";
    public const string IceShardsTalentId = "I-1-4";
    public const string FocusedIceTalentId = "I-3-4";
    public const string ImprovedConeOfColdTalentId = "I-4-4";

    private const string ArcaneMissilesId = "MAGE_ARCANE_MISSILES";
    private const string ArcaneExplosionId = "MAGE_ARCANE_EXPLOSION";
    private const string PresenceOfMindId = "MAGE_PRESENCE_OF_MIND";
    private const string IceShardId = "MAGE_ICE_SHARD";
    private const string ConeOfColdId = "MAGE_CONE_OF_COLD";

    private static readonly HashSet<string> SupportedTalentIds = new(StringComparer.Ordinal)
    {
        ArcaneFocusTalentId,
        ImprovedArcaneMissilesTalentId,
        ImprovedArcaneExplosionTalentId,
        ArcaneInstabilityTalentId,
        QuickThinkingTalentId,
        ImprovedIceShardTalentId,
        FrostPrecisionTalentId,
        PiercingIceTalentId,
        IceShardsTalentId,
        FocusedIceTalentId,
        ImprovedConeOfColdTalentId
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

        if (!ability.IsSpell)
            return ability;

        decimal resourceCost = ability.ResourceCost;
        decimal damageMultiplier = ability.DamageMultiplier;
        decimal accuracyBonus = ability.AccuracyBonus;
        decimal criticalChanceBonus = ability.CriticalChanceBonus;
        decimal criticalDamageBonus = ability.CriticalDamageBonus;
        TimeSpan castTime = ability.CastTime;
        TimeSpan cooldown = ability.Cooldown;

        if (string.Equals(ability.School, "ARCANE", StringComparison.Ordinal)
            && TryGet(talents, ArcaneFocusTalentId, out ResolvedTalentEventHook focus))
        {
            accuracyBonus += focus.Value;
        }

        if (TryGet(talents, ArcaneInstabilityTalentId, out ResolvedTalentEventHook instability))
        {
            damageMultiplier *= 1 + instability.Value / 100m;
            criticalChanceBonus += instability.SecondaryValue;
        }

        if (string.Equals(ability.Id, ArcaneMissilesId, StringComparison.Ordinal)
            && TryGet(talents, ImprovedArcaneMissilesTalentId, out ResolvedTalentEventHook improvedMissiles))
        {
            damageMultiplier *= 1 + improvedMissiles.Value / 100m;
            resourceCost *= Math.Max(0, 1 - improvedMissiles.SecondaryValue / 100m);
        }
        else if (string.Equals(ability.Id, ArcaneExplosionId, StringComparison.Ordinal)
            && TryGet(talents, ImprovedArcaneExplosionTalentId, out ResolvedTalentEventHook improvedExplosion))
        {
            resourceCost *= Math.Max(0, 1 - improvedExplosion.Value / 100m);
            damageMultiplier *= 1 + improvedExplosion.SecondaryValue / 100m;
        }
        else if (string.Equals(ability.Id, PresenceOfMindId, StringComparison.Ordinal)
            && TryGet(talents, QuickThinkingTalentId, out ResolvedTalentEventHook quickThinking))
        {
            cooldown = Reduce(cooldown, quickThinking.Value);
        }

        if (string.Equals(ability.School, "FROST", StringComparison.Ordinal))
        {
            if (TryGet(talents, FrostPrecisionTalentId, out ResolvedTalentEventHook precision))
            {
                accuracyBonus += precision.Value;
                resourceCost *= Math.Max(0, 1 - precision.SecondaryValue / 100m);
            }
            if (TryGet(talents, PiercingIceTalentId, out ResolvedTalentEventHook piercing))
                damageMultiplier *= 1 + piercing.Value / 100m;
            if (TryGet(talents, IceShardsTalentId, out ResolvedTalentEventHook iceShards))
                criticalDamageBonus += iceShards.Value;
            if (TryGet(talents, FocusedIceTalentId, out ResolvedTalentEventHook focusedIce))
                resourceCost *= Math.Max(0, 1 - focusedIce.Value / 100m);

            if (string.Equals(ability.Id, IceShardId, StringComparison.Ordinal)
                && TryGet(talents, ImprovedIceShardTalentId, out ResolvedTalentEventHook improvedShard))
            {
                castTime = Reduce(castTime, improvedShard.Value);
            }

            if (string.Equals(ability.Id, ConeOfColdId, StringComparison.Ordinal)
                && TryGet(talents, ImprovedConeOfColdTalentId, out ResolvedTalentEventHook improvedCone))
            {
                damageMultiplier *= 1 + improvedCone.Value / 100m;
            }
        }

        return ability with
        {
            ResourceCost = Math.Max(0, resourceCost),
            Cooldown = cooldown,
            CastTime = castTime,
            DamageMultiplier = Math.Max(0, damageMultiplier),
            AccuracyBonus = accuracyBonus,
            CriticalChanceBonus = criticalChanceBonus,
            CriticalDamageBonus = criticalDamageBonus
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
