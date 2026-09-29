using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Pvp;

public enum ArenaTalentEventType
{
    OnIncomingDamage,
    OnAutoAttack,
    OnCast,
    OnHit,
    OnCrit,
    OnDamageTaken
}

public enum ArenaTalentConditionKind
{
    None,
    WasCritical,
    WasBlocked,
    SelfHealthBelowPercent,
    AbilityId
}

public enum ArenaTalentEffectKind
{
    GainResource,
    ModifyIncomingDamage,
    ModifyCooldown,
    ApplyBuff,
    ApplyDebuff,
    ApplyAbilityAction
}

public enum ArenaTalentActorRole
{
    Source,
    Target
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
    bool WasBlocked = false,
    decimal CurrentDamage = 0,
    decimal TargetHealthPercent = 100);

public sealed record ArenaTalentRuntimeEffect(
    ArenaTalentEffectKind Kind,
    string SourceTalentId,
    Guid SourceActorId,
    Guid TargetActorId,
    decimal Amount = 0,
    AbilityActionDefinition? Action = null,
    string? AbilityId = null,
    TimeSpan CooldownDelta = default,
    bool ResetCooldown = false,
    EffectDefinition? Effect = null);

public sealed record ArenaTalentEventRule(
    ArenaTalentEventType Trigger,
    ArenaTalentConditionKind Condition,
    ArenaTalentEffectKind Effect,
    ResolvedTalentEventHook Hook,
    string? ConditionValue = null,
    ArenaTalentActorRole EffectSource = ArenaTalentActorRole.Source,
    ArenaTalentActorRole EffectTarget = ArenaTalentActorRole.Target,
    string? AbilityId = null,
    TimeSpan CooldownDelta = default,
    bool ResetCooldown = false,
    EffectDefinition? StatusEffect = null);

/// <summary>
/// Content-key registration is isolated in normalization. Once a hook is normalized,
/// matching and execution depend only on trigger, condition and effect semantics.
/// </summary>
public static class ArenaTalentEventDispatcher
{
    public const string GuardianAnticipationTalentId = "G-1-5";
    public const string IncomingCriticalDamageTargetId = "INCOMING_CRITICAL_DAMAGE";
    public const string GuardianShieldFuryTalentId = "G-2-5";
    public const string BlockTargetId = "BLOCK";
    public const string ArcherSurvivalInstinctTalentId = "S-6-4";
    public const string LowHpReductionTargetId = "LOW_HP_REDUCTION";
    public const string ArcherHawkSpiritTalentId = "M-2-4";
    public const string HawkSpiritTargetId = "HAWK_SPIRIT";
    public const string PyromancerBlastWaveTalentId = "F-5-1";
    public const string MageBlastWaveAbilityId = "MAGE_BLAST_WAVE";

    public static bool Supports(ResolvedTalentEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        if (PyromancerImpactRuntime.SupportsArenaHook(hook))
            return true;

        return TryNormalize(hook, out ArenaTalentEventRule rule)
            && SupportsRuntimeTrigger(rule.Trigger);
    }

    private static bool SupportsRuntimeTrigger(ArenaTalentEventType trigger) => trigger is
        ArenaTalentEventType.OnIncomingDamage
        or ArenaTalentEventType.OnCast
        or ArenaTalentEventType.OnHit
        or ArenaTalentEventType.OnCrit
        or ArenaTalentEventType.OnDamageTaken;

    public static bool TryNormalize(
        ResolvedTalentEventHook hook,
        out ArenaTalentEventRule rule)
    {
        ArgumentNullException.ThrowIfNull(hook);

        // Talent IDs are content keys only at this legacy-content normalization boundary.
        // Runtime matching and execution below use semantic trigger/condition/effect descriptors.
        if (string.Equals(hook.TalentId, GuardianAnticipationTalentId, StringComparison.Ordinal)
            && string.Equals(hook.Key, TalentModifierKeys.OnDamageTaken, StringComparison.Ordinal)
            && string.Equals(hook.TargetId, IncomingCriticalDamageTargetId, StringComparison.Ordinal))
        {
            rule = new ArenaTalentEventRule(
                ArenaTalentEventType.OnIncomingDamage,
                ArenaTalentConditionKind.WasCritical,
                ArenaTalentEffectKind.ModifyIncomingDamage,
                hook,
                EffectSource: ArenaTalentActorRole.Target,
                EffectTarget: ArenaTalentActorRole.Target);
            return true;
        }

        if (string.Equals(hook.TalentId, ArcherSurvivalInstinctTalentId, StringComparison.Ordinal)
            && string.Equals(hook.Key, TalentModifierKeys.OnHpThreshold, StringComparison.Ordinal)
            && string.Equals(hook.TargetId, LowHpReductionTargetId, StringComparison.Ordinal)
            && hook.Threshold > 0)
        {
            rule = new ArenaTalentEventRule(
                ArenaTalentEventType.OnIncomingDamage,
                ArenaTalentConditionKind.SelfHealthBelowPercent,
                ArenaTalentEffectKind.ModifyIncomingDamage,
                hook,
                EffectSource: ArenaTalentActorRole.Target,
                EffectTarget: ArenaTalentActorRole.Target);
            return true;
        }

        if (string.Equals(hook.TalentId, GuardianShieldFuryTalentId, StringComparison.Ordinal)
            && string.Equals(hook.Key, TalentModifierKeys.OnDamageTaken, StringComparison.Ordinal)
            && string.Equals(hook.TargetId, BlockTargetId, StringComparison.Ordinal))
        {
            rule = new ArenaTalentEventRule(
                ArenaTalentEventType.OnDamageTaken,
                ArenaTalentConditionKind.WasBlocked,
                ArenaTalentEffectKind.GainResource,
                hook,
                EffectSource: ArenaTalentActorRole.Target,
                EffectTarget: ArenaTalentActorRole.Target);
            return true;
        }

        if (string.Equals(hook.TalentId, ArcherHawkSpiritTalentId, StringComparison.Ordinal)
            && string.Equals(hook.Key, TalentModifierKeys.OnAutoAttack, StringComparison.Ordinal)
            && string.Equals(hook.TargetId, HawkSpiritTargetId, StringComparison.Ordinal)
            && hook.Duration > TimeSpan.Zero
            && hook.Value > 0
            && hook.ChancePercent > 0)
        {
            rule = new ArenaTalentEventRule(
                ArenaTalentEventType.OnAutoAttack,
                ArenaTalentConditionKind.None,
                ArenaTalentEffectKind.ApplyBuff,
                hook,
                EffectSource: ArenaTalentActorRole.Source,
                EffectTarget: ArenaTalentActorRole.Source,
                StatusEffect: new EffectDefinition(
                    $"ARENA_TALENT_{hook.TalentId}_ATTACK_SPEED",
                    EffectKind.StatModifier,
                    hook.Duration,
                    1,
                    EffectStackPolicy.Refresh,
                    1 + hook.Value / 100m,
                    ModifiedStat: EffectStat.AttackSpeed,
                    ModifierMode: EffectModifierMode.Multiplicative,
                    SourceSpecific: true));
            return true;
        }

        if (string.Equals(hook.TalentId, PyromancerBlastWaveTalentId, StringComparison.Ordinal)
            && string.Equals(hook.Key, TalentModifierKeys.OnAbilityUsed, StringComparison.Ordinal)
            && hook.Duration > TimeSpan.Zero
            && hook.Value > 0)
        {
            rule = new ArenaTalentEventRule(
                ArenaTalentEventType.OnCast,
                ArenaTalentConditionKind.AbilityId,
                ArenaTalentEffectKind.ApplyDebuff,
                hook,
                ConditionValue: MageBlastWaveAbilityId,
                StatusEffect: new EffectDefinition(
                    $"ARENA_TALENT_{hook.TalentId}_ATTACK_SPEED",
                    EffectKind.StatModifier,
                    hook.Duration,
                    1,
                    EffectStackPolicy.Refresh,
                    Math.Max(0, 1 - hook.Value / 100m),
                    ModifiedStat: EffectStat.AttackSpeed,
                    ModifierMode: EffectModifierMode.Multiplicative,
                    SourceSpecific: true));
            return true;
        }

        rule = null!;
        return false;
    }

    public static decimal ApplyIncomingDamageModifiers(
        ResolvedTalentModifiers talents,
        IncomingDamageContext context,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(talents);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(random);

        decimal healthPercent = context.Target.MaxHp <= 0
            ? 0
            : context.Target.CurrentHp / context.Target.MaxHp * 100m;
        var combatEvent = new ArenaTalentCombatEvent(
            ArenaTalentEventType.OnIncomingDamage,
            context.Source.ActorId,
            context.Target.ActorId,
            context.OccurredAtUtc,
            DamageType: context.DamageType,
            WasCritical: context.WasCritical,
            CurrentDamage: context.CurrentAmount,
            TargetHealthPercent: healthPercent);

        decimal damage = Math.Max(0, context.CurrentAmount);
        foreach (ArenaTalentRuntimeEffect effect in Dispatch(talents, combatEvent, random))
        {
            if (effect.Kind != ArenaTalentEffectKind.ModifyIncomingDamage)
            {
                throw new NotSupportedException(
                    $"Arena pre-damage event produced unsupported effect {effect.Kind}.");
            }

            decimal reductionPercent = Math.Clamp(effect.Amount, 0, 100);
            damage = Math.Max(0, damage * (1 - reductionPercent / 100m));
        }

        return damage;
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
                || !ConditionMatches(rule, combatEvent)
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
        ArenaTalentCombatEvent combatEvent)
    {
        Guid sourceActorId = ResolveActor(rule.EffectSource, combatEvent);
        Guid targetActorId = ResolveActor(rule.EffectTarget, combatEvent);

        return rule.Effect switch
        {
            ArenaTalentEffectKind.GainResource => new ArenaTalentRuntimeEffect(
                ArenaTalentEffectKind.GainResource,
                rule.Hook.TalentId,
                sourceActorId,
                targetActorId,
                Math.Max(0, rule.Hook.Value)),
            ArenaTalentEffectKind.ModifyIncomingDamage => new ArenaTalentRuntimeEffect(
                ArenaTalentEffectKind.ModifyIncomingDamage,
                rule.Hook.TalentId,
                sourceActorId,
                targetActorId,
                Math.Max(0, rule.Hook.Value)),
            ArenaTalentEffectKind.ModifyCooldown when !string.IsNullOrWhiteSpace(rule.AbilityId) =>
                new ArenaTalentRuntimeEffect(
                    ArenaTalentEffectKind.ModifyCooldown,
                    rule.Hook.TalentId,
                    sourceActorId,
                    targetActorId,
                    AbilityId: rule.AbilityId,
                    CooldownDelta: rule.CooldownDelta,
                    ResetCooldown: rule.ResetCooldown),
            ArenaTalentEffectKind.ApplyBuff or ArenaTalentEffectKind.ApplyDebuff
                when rule.StatusEffect is not null => new ArenaTalentRuntimeEffect(
                    rule.Effect,
                    rule.Hook.TalentId,
                    sourceActorId,
                    targetActorId,
                    Effect: rule.StatusEffect),
            _ => throw new NotSupportedException($"Arena talent effect {rule.Effect} has no executable descriptor.")
        };
    }

    private static Guid ResolveActor(
        ArenaTalentActorRole role,
        ArenaTalentCombatEvent combatEvent) => role switch
    {
        ArenaTalentActorRole.Source => combatEvent.SourceActorId,
        ArenaTalentActorRole.Target => combatEvent.TargetActorId,
        _ => throw new NotSupportedException($"Arena talent actor role {role} is unsupported.")
    };

    private static bool ConditionMatches(
        ArenaTalentEventRule rule,
        ArenaTalentCombatEvent combatEvent) => rule.Condition switch
    {
        ArenaTalentConditionKind.None => true,
        ArenaTalentConditionKind.WasCritical => combatEvent.WasCritical,
        ArenaTalentConditionKind.WasBlocked => combatEvent.WasBlocked,
        ArenaTalentConditionKind.SelfHealthBelowPercent =>
            combatEvent.TargetHealthPercent < rule.Hook.Threshold,
        ArenaTalentConditionKind.AbilityId =>
            combatEvent.Ability is not null
            && string.Equals(combatEvent.Ability.Id, rule.ConditionValue, StringComparison.Ordinal),
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
