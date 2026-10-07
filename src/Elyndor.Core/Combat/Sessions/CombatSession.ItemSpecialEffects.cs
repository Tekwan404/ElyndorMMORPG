using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.ItemEffects;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private ItemSpecialEffectRuntime _itemSpecialEffectRuntime = new([]);
    private IReadOnlyDictionary<Guid, IReadOnlySet<string>> _itemSpecialEffectLoadoutSnapshot =
        new Dictionary<Guid, IReadOnlySet<string>>();

    private void InitializeItemSpecialEffectLoadoutSnapshot(
        IReadOnlyList<ItemSpecialEffectDefinition> definitions)
    {
        _itemSpecialEffectRuntime = new ItemSpecialEffectRuntime(definitions);
        _itemSpecialEffectLoadoutSnapshot = _playerStatesByActorId.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Definition.EquippedSpecialEffectIds is { } effectIds
                ? (IReadOnlySet<string>)effectIds.ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal));
    }

    private void ApplyItemSpecialEffectHooks(CombatEvent combatEvent)
    {
        if (!_itemSpecialEffectRuntime.HandlesEventType(combatEvent.Type))
            return;

        IReadOnlyList<ItemSpecialEffectInvocation> invocations =
            _itemSpecialEffectRuntime.Evaluate(
                combatEvent,
                _itemSpecialEffectLoadoutSnapshot,
                _procGuard);

        foreach (ItemSpecialEffectInvocation invocation in invocations)
        {
            if (!_playerStatesByActorId.TryGetValue(
                    invocation.OwnerActorId,
                    out CombatPlayerRuntimeState? ownerState)
                || ownerState.Definition.Actor.IsDead)
            {
                continue;
            }

            IReadOnlyList<CombatEvent> actionEvents =
                ExecuteItemSpecialEffect(invocation, ownerState);
            if (actionEvents.Count == 0)
                continue;

            Guid targetActorId = ResolveItemSpecialEffectTargetActorId(
                invocation,
                ownerState.Runtime)
                ?? invocation.OwnerActorId;
            ApplyKernelEvents(
                actionEvents,
                invocation.OwnerActorId,
                targetActorId,
                invocation.EffectId);
        }
    }

    private IReadOnlyList<CombatEvent> ExecuteItemSpecialEffect(
        ItemSpecialEffectInvocation invocation,
        CombatPlayerRuntimeState ownerState)
    {
        ItemSpecialEffectActionDefinition action = invocation.Action;
        CombatRuntimeState runtime = ownerState.Runtime;
        Guid? targetActorId = ResolveItemSpecialEffectTargetActorId(
            invocation,
            runtime);
        if (targetActorId is null
            || !runtime.Actors.TryGetValue(
                targetActorId.Value,
                out CombatActorState? target)
            || target.IsDead)
        {
            return [];
        }

        return action.Kind switch
        {
            ItemSpecialEffectActionKind.ApplyEffect =>
                ApplyItemSpecialEffectStat(invocation, target),
            ItemSpecialEffectActionKind.AddShield =>
                ApplyItemSpecialEffectShield(invocation, target),
            ItemSpecialEffectActionKind.TriggerDamage =>
                ResolveItemSpecialEffectDamage(
                    invocation,
                    ownerState,
                    targetActorId.Value),
            ItemSpecialEffectActionKind.Heal =>
                ResolveItemSpecialEffectHealing(
                    invocation,
                    ownerState,
                    targetActorId.Value,
                    target),
            ItemSpecialEffectActionKind.RestoreResource =>
                ResolveItemSpecialEffectResource(
                    invocation,
                    ownerState),
            ItemSpecialEffectActionKind.ModifyCooldown =>
                ResolveItemSpecialEffectCooldown(
                    invocation,
                    ownerState),
            _ => []
        };
    }

    private static IReadOnlyList<CombatEvent> ApplyItemSpecialEffectStat(
        ItemSpecialEffectInvocation invocation,
        CombatActorState target)
    {
        ItemSpecialEffectActionDefinition action = invocation.Action;
        if (action.Duration is not { } duration
            || action.ModifiedStat is not { } modifiedStat)
        {
            return [];
        }

        return EffectEngine.Apply(
            target,
            invocation.OwnerActorId,
            new EffectDefinition(
                action.ReferenceId ?? invocation.EffectId,
                EffectKind.StatModifier,
                duration,
                action.MaxStacks,
                action.StackPolicy,
                action.Magnitude,
                ModifiedStat: modifiedStat,
                ModifierMode: action.ModifierMode,
                DisplayName: action.DisplayName,
                Description: action.Description,
                IconId: action.IconId),
            invocation.OccurredAtUtc);
    }

    private static IReadOnlyList<CombatEvent> ApplyItemSpecialEffectShield(
        ItemSpecialEffectInvocation invocation,
        CombatActorState target)
    {
        ItemSpecialEffectActionDefinition action = invocation.Action;
        if (action.Duration is not { } duration)
            return [];

        decimal magnitude = action.ScaleWithMaxHp
            ? target.MaxHp * action.Magnitude
            : action.Magnitude;
        if (magnitude <= 0)
            return [];

        return EffectEngine.Apply(
            target,
            invocation.OwnerActorId,
            new EffectDefinition(
                action.ReferenceId ?? invocation.EffectId,
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

    private IReadOnlyList<CombatEvent> ResolveItemSpecialEffectDamage(
        ItemSpecialEffectInvocation invocation,
        CombatPlayerRuntimeState ownerState,
        Guid targetActorId)
    {
        ItemSpecialEffectActionDefinition action = invocation.Action;
        decimal amount = action.Magnitude
            + invocation.TriggerEvent.Amount
                * action.EventAmountPercent
                / 100m;
        AbilityActionDefinition triggered = new(
            AbilityActionType.Damage,
            Amount: Math.Max(0, amount),
            DamageType: action.DamageType,
            CanMiss: false,
            CanCrit: false,
            CanDodge: false,
            AttackPowerCoefficient: action.AttackPowerCoefficient,
            SpellPowerCoefficient: action.SpellPowerCoefficient);

        return ResolveItemSpecialEffectAbilityAction(
            invocation,
            ownerState,
            targetActorId,
            triggered);
    }

    private IReadOnlyList<CombatEvent> ResolveItemSpecialEffectHealing(
        ItemSpecialEffectInvocation invocation,
        CombatPlayerRuntimeState ownerState,
        Guid targetActorId,
        CombatActorState target)
    {
        ItemSpecialEffectActionDefinition action = invocation.Action;
        decimal amount = action.ScaleWithMaxHp
            ? target.MaxHp * action.Magnitude
            : action.Magnitude;
        AbilityActionDefinition triggered = new(
            AbilityActionType.Healing,
            Amount: Math.Max(0, amount),
            CanMiss: false,
            CanCrit: false,
            CanDodge: false);

        return ResolveItemSpecialEffectAbilityAction(
            invocation,
            ownerState,
            targetActorId,
            triggered);
    }

    private IReadOnlyList<CombatEvent> ResolveItemSpecialEffectResource(
        ItemSpecialEffectInvocation invocation,
        CombatPlayerRuntimeState ownerState)
    {
        AbilityActionDefinition triggered = new(
            AbilityActionType.ResourceChange,
            Amount: invocation.Action.Magnitude,
            CanMiss: false,
            CanCrit: false,
            CanDodge: false);

        return ResolveItemSpecialEffectAbilityAction(
            invocation,
            ownerState,
            invocation.OwnerActorId,
            triggered);
    }

    private static IReadOnlyList<CombatEvent> ResolveItemSpecialEffectCooldown(
        ItemSpecialEffectInvocation invocation,
        CombatPlayerRuntimeState ownerState)
    {
        ItemSpecialEffectActionDefinition action = invocation.Action;
        if (string.IsNullOrWhiteSpace(action.AbilityId))
            return [];

        ownerState.Runtime.ModifyCooldown(
            action.AbilityId,
            TimeSpan.FromSeconds(-(double)action.Magnitude),
            invocation.OccurredAtUtc);
        return [];
    }

    private IReadOnlyList<CombatEvent> ResolveItemSpecialEffectAbilityAction(
        ItemSpecialEffectInvocation invocation,
        CombatPlayerRuntimeState ownerState,
        Guid targetActorId,
        AbilityActionDefinition action)
    {
        AbilityDefinition ability = new(
            invocation.EffectId,
            AbilityType.Instant,
            targetActorId == invocation.OwnerActorId
                ? AbilityTargetType.Self
                : AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            action.DamageType == Damage.DamageType.Magical,
            action.DamageType == Damage.DamageType.Magical
                ? "ARCANE"
                : "PHYSICAL",
            Actions: [action]);

        return AbilityEngine.ResolveTriggeredAction(
            ownerState.Runtime,
            ability,
            action,
            targetActorId,
            invocation.OccurredAtUtc,
            _random);
    }

    private static Guid? ResolveItemSpecialEffectTargetActorId(
        ItemSpecialEffectInvocation invocation,
        CombatRuntimeState runtime) =>
        invocation.Action.TargetRole switch
        {
            ItemSpecialEffectTargetRole.Owner => invocation.OwnerActorId,
            ItemSpecialEffectTargetRole.EventSource =>
                invocation.TriggerEvent.SourceActorId,
            ItemSpecialEffectTargetRole.EventTarget =>
                invocation.TriggerEvent.TargetActorId,
            _ => null
        };
}
