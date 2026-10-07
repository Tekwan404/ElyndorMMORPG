namespace Elyndor.Core.Combat.SetPassives;

public sealed class SetPassiveEvaluator
{
    private readonly IReadOnlyList<SetPassiveDefinition> _definitions;

    public SetPassiveEvaluator(IEnumerable<SetPassiveDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        SetPassiveDefinition[] materialized = definitions.ToArray();
        foreach (SetPassiveDefinition definition in materialized)
        {
            Validate(definition);
        }

        _definitions = materialized;
        TriggerEventTypes = materialized
            .Select(definition => definition.Trigger.EventType)
            .ToHashSet();
    }

    /// <summary>
    /// Event types any definition reacts to, so callers can skip the evaluation entirely.
    /// </summary>
    public IReadOnlySet<CombatEventType> TriggerEventTypes { get; }

    public IReadOnlyList<SetPassiveActionInvocation> Evaluate(
        CombatEvent combatEvent,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> equippedSetPieceCounts,
        SetPassiveRuntimeState runtimeState,
        ProcGuard? guard = null,
        Func<SetPassiveDefinition, CombatEvent, Guid?>? actorResolver = null,
        Func<SetPassiveDefinition, Guid, CombatEvent, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(combatEvent);
        ArgumentNullException.ThrowIfNull(equippedSetPieceCounts);
        ArgumentNullException.ThrowIfNull(runtimeState);
        guard ??= runtimeState.Guard;
        if (!ProcGuard.IsEligible(combatEvent))
            return [];

        List<SetPassiveActionInvocation> invocations = [];

        foreach (SetPassiveDefinition definition in _definitions)
        {
            if (definition.Trigger.EventType != combatEvent.Type)
            {
                continue;
            }

            Guid? actorId = actorResolver is null ? ResolveActorId(combatEvent, definition.Trigger.ActorRole)
                : actorResolver(definition, combatEvent);
            if (actorId is null || !HasRequiredPieces(actorId.Value, definition, equippedSetPieceCounts))
            {
                continue;
            }

            if (definition.Conditions.CriticalOnly && !combatEvent.IsCritical
                || definition.Conditions.PositiveAmountOnly && combatEvent.Amount <= 0
                || definition.Conditions.ResourceSpent > 0 && combatEvent.Amount >= 0
                || definition.Conditions.AbilityIds is { Count: > 0 } ids
                    && !ids.Contains(combatEvent.DefinitionId, StringComparer.Ordinal)
                || filter is not null && !filter(definition, actorId.Value, combatEvent))
                continue;

            SetPassiveProcState state = runtimeState.Get(actorId.Value, definition.Id);
            if (!guard.TryObserve(actorId.Value, definition.Id, combatEvent.ProcDispatchToken, combatEvent.Sequence)
                || !guard.IsReady(actorId.Value, definition.Id, combatEvent.OccurredAtUtc))
                continue;
            if (state.CooldownUntil is { } cooldownUntil
                && combatEvent.OccurredAtUtc < cooldownUntil)
            {
                // Deliberate: events that arrive during the internal cooldown do not advance
                // the EveryNth counter, so the two conditions never stack into a shorter
                // effective cooldown. SetPassiveEvaluatorTests pins this behaviour.
                continue;
            }

            string actionId = $"{combatEvent.SourceActorId}:{combatEvent.DefinitionId}:{combatEvent.WeaponHand}";
            if (definition.Conditions.OncePerAction && state.LastActionAt == combatEvent.OccurredAtUtc
                && state.LastActionId == actionId) continue;
            state = state with { LastActionAt = combatEvent.OccurredAtUtc, LastActionId = actionId };
            if (definition.Conditions.ResourceSpent > 0)
            {
                bool expired = state.WindowStartedAt is null
                    || definition.Conditions.SpendingWindow is { } window
                        && combatEvent.OccurredAtUtc >= state.WindowStartedAt + window;
                decimal spent = (expired ? 0 : state.ResourceTotal) - combatEvent.Amount;
                state = state with { ResourceTotal = spent,
                    WindowStartedAt = expired ? combatEvent.OccurredAtUtc : state.WindowStartedAt };
                if (spent < definition.Conditions.ResourceSpent)
                {
                    runtimeState.Set(state);
                    continue;
                }
            }
            if (definition.Conditions.AlternatingSources && state.LastSourceActorId == combatEvent.SourceActorId)
                continue;
            state = state with { LastSourceActorId = combatEvent.SourceActorId };
            int eventCounter = state.EventCounter + 1;
            if (eventCounter < definition.Conditions.EveryNth)
            {
                runtimeState.Set(state with { EventCounter = eventCounter });
                continue;
            }

            DateTimeOffset? nextCooldown = definition.Conditions.InternalCooldown is { } internalCooldown
                && internalCooldown > TimeSpan.Zero
                ? combatEvent.OccurredAtUtc + internalCooldown
                : null;

            runtimeState.Set(state with
            {
                EventCounter = 0,
                ResourceTotal = 0,
                WindowStartedAt = null,
                CooldownUntil = nextCooldown,
                LastProcAt = combatEvent.OccurredAtUtc
            });
            guard.StartCooldown(actorId.Value, definition.Id, combatEvent.OccurredAtUtc,
                definition.Conditions.InternalCooldown ?? TimeSpan.Zero);

            foreach (SetPassiveActionDefinition action in definition.Actions)
            {
                invocations.Add(new SetPassiveActionInvocation(
                    definition.Id,
                    definition.SetId,
                    actorId.Value,
                    combatEvent.OccurredAtUtc,
                    action));
            }
        }

        return invocations;
    }

    private static Guid? ResolveActorId(CombatEvent combatEvent, SetPassiveActorRole actorRole) => actorRole switch
    {
        SetPassiveActorRole.Source => combatEvent.SourceActorId,
        SetPassiveActorRole.Target => combatEvent.TargetActorId,
        _ => throw new ArgumentOutOfRangeException(nameof(actorRole), actorRole, null)
    };

    private static bool HasRequiredPieces(
        Guid actorId,
        SetPassiveDefinition definition,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> equippedSetPieceCounts)
    {
        return equippedSetPieceCounts.TryGetValue(actorId, out IReadOnlyDictionary<string, int>? setCounts)
            && setCounts.TryGetValue(definition.SetId, out int pieces)
            && pieces >= definition.RequiredPieces;
    }

    public static void Validate(SetPassiveDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.SetId);
        ArgumentNullException.ThrowIfNull(definition.Trigger);
        ArgumentNullException.ThrowIfNull(definition.Conditions);
        ArgumentNullException.ThrowIfNull(definition.Actions);

        if (!Enum.IsDefined(definition.Trigger.ActorRole) || !Enum.IsDefined(definition.Trigger.EventType)
            || definition.Conditions.ResourceSpent < 0
            || definition.Conditions.ResourceSpent > 0 && definition.Conditions.SpendingWindow is not { Ticks: > 0 })
            throw new ArgumentException("Invalid set trigger or resource window.", nameof(definition));

        if (definition.RequiredPieces <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                definition.RequiredPieces,
                "RequiredPieces must be greater than zero.");
        }

        if (definition.Conditions.EveryNth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                definition.Conditions.EveryNth,
                "Conditions.EveryNth must be greater than zero.");
        }

        if (definition.Conditions.InternalCooldown is { } internalCooldown
            && internalCooldown < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                internalCooldown,
                "Conditions.InternalCooldown cannot be negative.");
        }

        if (definition.Actions.Count == 0)
        {
            throw new ArgumentException("Set passive must define at least one action.", nameof(definition));
        }

        foreach (SetPassiveActionDefinition action in definition.Actions)
        {
            if (!Enum.IsDefined(action.Kind) || action.Magnitude < 0
                || action.Kind == SetPassiveActionKind.ReduceCooldown && string.IsNullOrWhiteSpace(action.ReferenceId)
                || !Enum.IsDefined(action.ModifierMode) || !Enum.IsDefined(action.StackPolicy)
                || action.Kind is SetPassiveActionKind.EmpowerNextDirect or SetPassiveActionKind.EmpowerNextEffect
                    && (string.IsNullOrWhiteSpace(action.ReferenceId) || action.Duration is not { Ticks: > 0 })
                || action.Kind == SetPassiveActionKind.ExtendTargetEffect && action.AbilityIds is not { Count: > 0 })
                throw new ArgumentException("Invalid set passive action.", nameof(definition));

            if (action.MaxStacks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    action.MaxStacks,
                    "Action.MaxStacks must be greater than zero.");
            }

            if (action.Duration is { } duration && duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    duration,
                    "Action.Duration cannot be negative.");
            }
        }
    }
}
