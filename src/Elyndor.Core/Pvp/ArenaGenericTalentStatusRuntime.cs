using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Pvp;

/// <summary>
/// Timed stat effects produced by combat events. Content IDs select the PvE
/// contract; the shared descriptor handles actor routing and effect creation.
/// </summary>
public static class ArenaGenericTalentStatusRuntime
{
    private sealed record StatusRule(
        string TalentId,
        string Key,
        string? TargetId,
        ArenaTalentEventType Trigger,
        ArenaTalentEffectKind Kind,
        EffectStat Stat,
        string EffectId,
        decimal MinimumCost = 0);

    private static readonly StatusRule[] Rules =
    [
        new("B-2-4", TalentModifierKeys.OnAbilityUsed, null,
            ArenaTalentEventType.OnCast, ArenaTalentEffectKind.ApplyBuff,
            EffectStat.AttackSpeed, "BERSERKER_MOMENTUM_ATTACK_SPEED", 20),
        new("B-6-2", TalentModifierKeys.OnCriticalHit, "AUTO_ATTACK",
            ArenaTalentEventType.OnCrit, ArenaTalentEffectKind.ApplyDebuff,
            EffectStat.IncomingPhysicalDamageMultiplier, "BERSERKER_DEVASTATING_VULNERABILITY")
    ];

    public static bool Supports(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return RuleFor(hook) is not null && hook.Rank > 0 && hook.Value > 0
            && hook.Duration > TimeSpan.Zero;
    }

    public static IReadOnlyList<ArenaTalentRuntimeEffect> Dispatch(
        ResolvedTalentModifiers talents,
        ArenaTalentCombatEvent combatEvent)
    {
        ArgumentNullException.ThrowIfNull(talents);
        ArgumentNullException.ThrowIfNull(combatEvent);

        List<ArenaTalentRuntimeEffect> effects = [];
        foreach (ResolvedTalentEventHook hook in talents.EventHooks.Where(Supports))
        {
            StatusRule rule = RuleFor(hook)!;
            if (!Matches(rule, hook, combatEvent))
                continue;

            Guid target = rule.Kind == ArenaTalentEffectKind.ApplyBuff
                ? combatEvent.SourceActorId
                : combatEvent.TargetActorId;
            var definition = new EffectDefinition(
                rule.EffectId,
                EffectKind.StatModifier,
                hook.Duration,
                1,
                EffectStackPolicy.Refresh,
                rule.Kind == ArenaTalentEffectKind.ApplyBuff
                    ? hook.Value / 100m
                    : 1 + hook.Value / 100m,
                ModifiedStat: rule.Stat,
                ModifierMode: rule.Kind == ArenaTalentEffectKind.ApplyBuff
                    ? EffectModifierMode.Percent
                    : EffectModifierMode.Multiplicative,
                SourceSpecific: rule.Kind == ArenaTalentEffectKind.ApplyDebuff);
            effects.Add(new ArenaTalentRuntimeEffect(rule.Kind, hook.TalentId,
                combatEvent.SourceActorId, target, Effect: definition));
        }

        return effects;
    }

    private static bool Matches(StatusRule rule, ResolvedTalentEventHook hook,
        ArenaTalentCombatEvent combatEvent)
    {
        if (combatEvent.Type != rule.Trigger)
            return false;
        if (rule.Trigger == ArenaTalentEventType.OnCast)
            return combatEvent.Ability is not null
                && combatEvent.Ability.ResourceCost >= Math.Max(rule.MinimumCost, hook.Threshold);

        // An unlabelled hit could be a periodic effect; require an explicit
        // auto-attack marker from the Arena event adapter.
        return combatEvent.WasCritical
            && string.Equals(combatEvent.Ability?.Id, "AUTO_ATTACK", StringComparison.Ordinal);
    }

    private static StatusRule? RuleFor(ResolvedTalentEventHook hook) =>
        Rules.FirstOrDefault(rule =>
            string.Equals(rule.TalentId, hook.TalentId, StringComparison.Ordinal)
            && string.Equals(rule.Key, hook.Key, StringComparison.Ordinal)
            && string.Equals(rule.TargetId, hook.TargetId, StringComparison.Ordinal));
}
