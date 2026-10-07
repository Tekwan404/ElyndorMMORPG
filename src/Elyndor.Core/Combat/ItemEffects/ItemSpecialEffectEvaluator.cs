namespace Elyndor.Core.Combat.ItemEffects;

public sealed class ItemSpecialEffectEvaluator
{
    private readonly IReadOnlyList<ItemSpecialEffectDefinition> _definitions;

    public ItemSpecialEffectEvaluator(IEnumerable<ItemSpecialEffectDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        ItemSpecialEffectDefinition[] materialized = definitions.ToArray();
        foreach (ItemSpecialEffectDefinition definition in materialized)
            Validate(definition);

        _definitions = materialized;
        TriggerEventTypes = materialized
            .Select(definition => definition.Trigger.EventType)
            .ToHashSet();
    }

    public IReadOnlySet<CombatEventType> TriggerEventTypes { get; }

    public IReadOnlyList<ItemSpecialEffectInvocation> Evaluate(
        CombatEvent combatEvent,
        IReadOnlyDictionary<Guid, IReadOnlySet<string>> equippedSpecialEffectIds,
        ItemSpecialEffectRuntimeState runtimeState,
        ProcGuard? guard = null)
    {
        ArgumentNullException.ThrowIfNull(combatEvent);
        ArgumentNullException.ThrowIfNull(equippedSpecialEffectIds);
        ArgumentNullException.ThrowIfNull(runtimeState);
        guard ??= runtimeState.Guard;

        if (!ProcGuard.IsEligible(combatEvent))
            return [];

        List<ItemSpecialEffectInvocation> invocations = [];
        foreach (ItemSpecialEffectDefinition definition in _definitions)
        {
            if (definition.Trigger.EventType != combatEvent.Type)
                continue;

            Guid? ownerActorId = ResolveOwnerActorId(combatEvent, definition.Trigger.ActorRole);
            if (ownerActorId is null
                || !equippedSpecialEffectIds.TryGetValue(ownerActorId.Value, out IReadOnlySet<string>? equipped)
                || !equipped.Contains(definition.Id))
            {
                continue;
            }

            if (definition.Trigger.DefinitionIds is { Count: > 0 }
                && (combatEvent.DefinitionId is null
                    || !definition.Trigger.DefinitionIds.Contains(
                        combatEvent.DefinitionId,
                        StringComparer.Ordinal)))
            {
                continue;
            }

            if (definition.Trigger.DamageType is { } damageType
                && combatEvent.DamageType != damageType)
            {
                continue;
            }

            ItemSpecialEffectProcState state = runtimeState.Get(ownerActorId.Value, definition.Id);
            if (!guard.TryObserve(
                    ownerActorId.Value,
                    definition.Id,
                    combatEvent.ProcDispatchToken,
                    combatEvent.Sequence)
                || !guard.IsReady(
                    ownerActorId.Value,
                    definition.Id,
                    combatEvent.OccurredAtUtc))
            {
                continue;
            }

            if (state.CooldownUntil is { } cooldownUntil
                && combatEvent.OccurredAtUtc < cooldownUntil)
            {
                continue;
            }

            int eventCounter = state.EventCounter + 1;
            if (eventCounter < definition.Conditions.EveryNth)
            {
                runtimeState.Set(state with { EventCounter = eventCounter });
                continue;
            }

            DateTimeOffset? nextCooldown =
                definition.Conditions.InternalCooldown is { } internalCooldown
                && internalCooldown > TimeSpan.Zero
                    ? combatEvent.OccurredAtUtc + internalCooldown
                    : null;

            runtimeState.Set(state with
            {
                EventCounter = 0,
                CooldownUntil = nextCooldown,
                LastProcAt = combatEvent.OccurredAtUtc
            });
            guard.StartCooldown(
                ownerActorId.Value,
                definition.Id,
                combatEvent.OccurredAtUtc,
                definition.Conditions.InternalCooldown ?? TimeSpan.Zero);

            foreach (ItemSpecialEffectActionDefinition action in definition.Actions)
            {
                invocations.Add(new ItemSpecialEffectInvocation(
                    definition.Id,
                    ownerActorId.Value,
                    combatEvent.OccurredAtUtc,
                    combatEvent,
                    action));
            }
        }

        return invocations;
    }

    private static Guid? ResolveOwnerActorId(
        CombatEvent combatEvent,
        ItemSpecialEffectActorRole actorRole) =>
        actorRole switch
        {
            ItemSpecialEffectActorRole.Source => combatEvent.SourceActorId,
            ItemSpecialEffectActorRole.Target => combatEvent.TargetActorId,
            _ => throw new ArgumentOutOfRangeException(nameof(actorRole), actorRole, null)
        };

    private static void Validate(ItemSpecialEffectDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);
        ArgumentNullException.ThrowIfNull(definition.Trigger);
        ArgumentNullException.ThrowIfNull(definition.Conditions);
        ArgumentNullException.ThrowIfNull(definition.Actions);

        if (!Enum.IsDefined(definition.Trigger.EventType)
            || !Enum.IsDefined(definition.Trigger.ActorRole)
            || definition.Trigger.DamageType is { } triggerDamage && !Enum.IsDefined(triggerDamage)
            || definition.Conditions.EveryNth <= 0
            || definition.Conditions.InternalCooldown is { } cooldown
                && cooldown < TimeSpan.Zero
            || definition.Actions.Count == 0)
        {
            throw new ArgumentException(
                "Item special effect definition contains invalid trigger or condition values.",
                nameof(definition));
        }

        if (definition.Trigger.DefinitionIds is { Count: > 0 } ids
            && (ids.Any(string.IsNullOrWhiteSpace)
                || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count))
        {
            throw new ArgumentException(
                "Item special effect trigger contains invalid definition filters.",
                nameof(definition));
        }

        foreach (ItemSpecialEffectActionDefinition action in definition.Actions)
        {
            if (!Enum.IsDefined(action.Kind)
                || !Enum.IsDefined(action.TargetRole)
                || !Enum.IsDefined(action.DamageType)
                || !Enum.IsDefined(action.ModifierMode)
                || !Enum.IsDefined(action.StackPolicy)
                || action.ModifiedStat is { } stat && !Enum.IsDefined(stat)
                || action.MaxStacks <= 0
                || action.Duration is { } duration && duration < TimeSpan.Zero
                || action.EventAmountPercent < 0)
            {
                throw new ArgumentException(
                    "Item special effect action contains invalid values.",
                    nameof(definition));
            }

            bool requiresDuration =
                action.Kind is ItemSpecialEffectActionKind.ApplyEffect
                    or ItemSpecialEffectActionKind.AddShield;
            if (requiresDuration
                && (action.Duration is null || action.Duration <= TimeSpan.Zero))
            {
                throw new ArgumentException(
                    "Timed item special effect actions require a positive duration.",
                    nameof(definition));
            }

            if (action.Kind == ItemSpecialEffectActionKind.ApplyEffect
                && (action.ModifiedStat is null || action.Magnitude == 0))
            {
                throw new ArgumentException(
                    "Stat item special effects require a modified stat and non-zero magnitude.",
                    nameof(definition));
            }

            if (action.Kind == ItemSpecialEffectActionKind.AddShield
                && action.Magnitude <= 0)
            {
                throw new ArgumentException(
                    "Shield item special effects require positive magnitude.",
                    nameof(definition));
            }

            if (action.Kind == ItemSpecialEffectActionKind.TriggerDamage
                && action.Magnitude <= 0
                && action.EventAmountPercent <= 0
                && action.AttackPowerCoefficient <= 0
                && action.SpellPowerCoefficient <= 0)
            {
                throw new ArgumentException(
                    "Damage item special effects require a positive damage source.",
                    nameof(definition));
            }

            if (action.Kind == ItemSpecialEffectActionKind.Heal
                && action.Magnitude <= 0)
            {
                throw new ArgumentException(
                    "Healing item special effects require positive magnitude.",
                    nameof(definition));
            }

            if (action.Kind == ItemSpecialEffectActionKind.RestoreResource
                && action.Magnitude <= 0)
            {
                throw new ArgumentException(
                    "Resource item special effects require positive magnitude.",
                    nameof(definition));
            }

            if (action.Kind == ItemSpecialEffectActionKind.ModifyCooldown
                && (string.IsNullOrWhiteSpace(action.AbilityId)
                    || action.Magnitude <= 0))
            {
                throw new ArgumentException(
                    "Cooldown item special effects require an ability and positive reduction.",
                    nameof(definition));
            }
        }
    }
}
