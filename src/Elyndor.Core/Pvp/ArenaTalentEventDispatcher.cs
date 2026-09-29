using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Pvp;

public enum ArenaTalentEventType
{
    OnCast,
    OnHit,
    OnCrit,
    OnDamageTaken
}

public enum ArenaTalentConditionKind
{
    None,
    WasBlocked
}

public enum ArenaTalentEffectKind
{
    GainResource,
    ApplyAbilityAction
}

public sealed record ArenaTalentCombatEvent(
    ArenaTalentEventType Type,
    Guid SourceActorId,
    Guid TargetActorId,
    DateTimeOffset OccurredAtUtc,
    AbilityDefinition? Ability = null,
    decimal FinalDamage = 0,
    DamageType? DamageType = null,
    bool WasCritical = false,
    bool WasBlocked = false);

public sealed record ArenaTalentRuntimeEffect(
    ArenaTalentEffectKind Kind,
    string SourceTalentId,
    Guid SourceActorId,
    Guid TargetActorId,
    decimal Amount = 0,
    AbilityActionDefinition? Action = null);

public sealed record ArenaTalentEventRule(
    ArenaTalentEventType Trigger,
    ArenaTalentConditionKind Condition,
    ArenaTalentEffectKind Effect,
    ResolvedTalentEventHook Hook);

/// <summary>
/// Content-key registration is isolated in normalization. Once a hook is normalized,
/// matching and execution depend only on trigger, condition and effect semantics.
/// </summary>
public static class ArenaTalentEventDispatcher
{
    public const string GuardianShieldFuryTalentId = "G-2-5";
    public const string BlockTargetId = "BLOCK";

    public static bool Supports(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return PyromancerImpactRuntime.SupportsArenaHook(hook) || TryNormalize(hook, out _);
    }

    public static bool TryNormalize(
        ResolvedTalentEventHook hook,
        out ArenaTalentEventRule rule)
    {
        ArgumentNullException.ThrowIfNull(hook);

        // Talent ID is only the content key here. Runtime execution below never branches on it.
        if (string.Equals(hook.TalentId, GuardianShieldFuryTalentId, StringComparison.Ordinal)
            && string.Equals(hook.Key, TalentModifierKeys.OnDamageTaken, StringComparison.Ordinal)
            && string.Equals(hook.TargetId, BlockTargetId, StringComparison.Ordinal))
        {
            rule = new ArenaTalentEventRule(
                ArenaTalentEventType.OnDamageTaken,
                ArenaTalentConditionKind.WasBlocked,
                ArenaTalentEffectKind.GainResource,
                hook);
            return true;
        }

        rule = null!;
        return false;
    }

    public static IReadOnlyList<ArenaTalentRuntimeEffect> Dispatch(
        ResolvedTalentModifiers talents,
        ArenaTalentCombatEvent combatEvent,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(talents);
        ArgumentNullException.ThrowIfNull(combatEvent);
        ArgumentNullException.ThrowIfNull(random);

        List<ArenaTalentRuntimeEffect> effects = [];

        if (combatEvent.Type == ArenaTalentEventType.OnHit && combatEvent.Ability is not null)
        {
            AbilityActionDefinition? action = PyromancerImpactRuntime.TryResolveStunAction(
                talents,
                combatEvent.Ability,
                random);
            if (action is not null)
            {
                effects.Add(new ArenaTalentRuntimeEffect(
                    ArenaTalentEffectKind.ApplyAbilityAction,
                    PyromancerImpactRuntime.TalentId,
                    combatEvent.SourceActorId,
                    combatEvent.TargetActorId,
                    Action: action));
            }
        }

        foreach (ResolvedTalentEventHook hook in talents.EventHooks)
        {
            if (!TryNormalize(hook, out ArenaTalentEventRule rule)
                || rule.Trigger != combatEvent.Type
                || !ConditionMatches(rule.Condition, combatEvent)
                || !ProcSucceeds(hook, random))
            {
                continue;
            }

            effects.Add(ExecuteRule(rule, combatEvent));
        }

        return effects;
    }

    public static IReadOnlyList<ArenaTalentCombatEvent> FromSuccessfulAbility(
        Guid sourceActorId,
        Guid targetActorId,
        AbilityDefinition ability,
        IReadOnlyList<CombatEvent> events,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(ability);
        List<ArenaTalentCombatEvent> result =
        [
            new(
                ArenaTalentEventType.OnCast,
                sourceActorId,
                targetActorId,
                occurredAtUtc,
                ability)
        ];
        result.AddRange(FromDamageEvents(events, ability));
        return result;
    }

    public static IReadOnlyList<ArenaTalentCombatEvent> FromDamageEvents(
        IReadOnlyList<CombatEvent> events,
        AbilityDefinition? ability = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        List<ArenaTalentCombatEvent> result = [];
        Dictionary<(Guid Source, Guid Target), DamageEventState> states = [];

        foreach (CombatEvent combatEvent in events)
        {
            if (combatEvent.SourceActorId is not Guid sourceActorId
                || combatEvent.TargetActorId is not Guid targetActorId)
            {
                continue;
            }

            var key = (sourceActorId, targetActorId);
            states.TryGetValue(key, out DamageEventState state);

            if (combatEvent.Type == CombatEventType.DamageBlocked)
            {
                states[key] = state with { WasBlocked = true };
                continue;
            }

            if (combatEvent.Type == CombatEventType.CriticalHit)
            {
                states[key] = state with { WasCritical = true };
                continue;
            }

            if (combatEvent.Type != CombatEventType.DamageDealt)
                continue;

            state = states.GetValueOrDefault(key);
            if (combatEvent.Amount > 0)
            {
                result.Add(new ArenaTalentCombatEvent(
                    ArenaTalentEventType.OnHit,
                    sourceActorId,
                    targetActorId,
                    combatEvent.OccurredAtUtc,
                    ability,
                    combatEvent.Amount,
                    combatEvent.DamageType,
                    state.WasCritical,
                    state.WasBlocked));
            }

            if (state.WasCritical)
            {
                result.Add(new ArenaTalentCombatEvent(
                    ArenaTalentEventType.OnCrit,
                    sourceActorId,
                    targetActorId,
                    combatEvent.OccurredAtUtc,
                    ability,
                    combatEvent.Amount,
                    combatEvent.DamageType,
                    true,
                    state.WasBlocked));
            }

            if (combatEvent.Amount > 0 || state.WasBlocked)
            {
                result.Add(new ArenaTalentCombatEvent(
                    ArenaTalentEventType.OnDamageTaken,
                    sourceActorId,
                    targetActorId,
                    combatEvent.OccurredAtUtc,
                    ability,
                    combatEvent.Amount,
                    combatEvent.DamageType,
                    state.WasCritical,
                    state.WasBlocked));
            }

            states.Remove(key);
        }

        return result;
    }

    private static ArenaTalentRuntimeEffect ExecuteRule(
        ArenaTalentEventRule rule,
        ArenaTalentCombatEvent combatEvent) => rule.Effect switch
    {
        ArenaTalentEffectKind.GainResource => new ArenaTalentRuntimeEffect(
            ArenaTalentEffectKind.GainResource,
            rule.Hook.TalentId,
            combatEvent.TargetActorId,
            combatEvent.TargetActorId,
            Math.Max(0, rule.Hook.Value)),
        _ => throw new NotSupportedException($"Arena talent effect {rule.Effect} has no executor.")
    };

    private static bool ConditionMatches(
        ArenaTalentConditionKind condition,
        ArenaTalentCombatEvent combatEvent) => condition switch
    {
        ArenaTalentConditionKind.None => true,
        ArenaTalentConditionKind.WasBlocked => combatEvent.WasBlocked,
        _ => false
    };

    private static bool ProcSucceeds(ResolvedTalentEventHook hook, IGameRandom random)
    {
        if (hook.ChancePercent <= 0)
            return false;
        if (hook.ChancePercent >= 100)
            return true;
        return random.NextUnit() < hook.ChancePercent / 100m;
    }

    private readonly record struct DamageEventState(bool WasCritical = false, bool WasBlocked = false);
}
