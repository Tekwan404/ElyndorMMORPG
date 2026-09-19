using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Combat.SetPassives;

public static class SetPassiveActionExecutor
{
    public static IReadOnlyList<CombatEvent> Execute(
        SetPassiveActionInvocation invocation,
        CombatActorState owner)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(owner);

        return invocation.Action.Kind switch
        {
            SetPassiveActionKind.ApplyEffect => ApplyStatEffect(invocation, owner),
            SetPassiveActionKind.AddShield => ApplyShield(invocation, owner),
            // An unknown kind must never abort the combat event that triggered it.
            _ => []
        };
    }

    private static IReadOnlyList<CombatEvent> ApplyStatEffect(
        SetPassiveActionInvocation invocation,
        CombatActorState owner)
    {
        SetPassiveActionDefinition action = invocation.Action;
        if (string.IsNullOrWhiteSpace(action.ReferenceId)
            || action.Duration is not { } duration
            || duration <= TimeSpan.Zero
            || action.ModifiedStat is null
            || action.Magnitude == 0)
        {
            return [];
        }

        decimal magnitude = ResolveMagnitude(action, owner);
        return EffectEngine.Apply(
            owner,
            owner.ActorId,
            new EffectDefinition(
                action.ReferenceId,
                EffectKind.StatModifier,
                duration,
                action.MaxStacks,
                action.StackPolicy,
                magnitude,
                ModifiedStat: action.ModifiedStat,
                ModifierMode: action.ModifierMode,
                DisplayName: action.DisplayName,
                Description: action.Description,
                IconId: action.IconId),
            invocation.OccurredAtUtc);
    }

    private static IReadOnlyList<CombatEvent> ApplyShield(
        SetPassiveActionInvocation invocation,
        CombatActorState owner)
    {
        SetPassiveActionDefinition action = invocation.Action;
        if (string.IsNullOrWhiteSpace(action.ReferenceId)
            || action.Duration is not { } duration
            || duration <= TimeSpan.Zero
            || action.Magnitude <= 0)
        {
            return [];
        }

        decimal magnitude = ResolveMagnitude(action, owner);
        if (magnitude <= 0)
        {
            return [];
        }

        return EffectEngine.Apply(
            owner,
            owner.ActorId,
            new EffectDefinition(
                action.ReferenceId,
                EffectKind.Shield,
                duration,
                action.MaxStacks,
                action.StackPolicy,
                magnitude,
                DisplayName: action.DisplayName,
                Description: action.Description,
                IconId: action.IconId),
            invocation.OccurredAtUtc);
    }

    private static decimal ResolveMagnitude(
        SetPassiveActionDefinition action,
        CombatActorState owner) =>
        action.ScaleWithMaxHp
            ? owner.MaxHp * action.Magnitude
            : action.Magnitude;
}
