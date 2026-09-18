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
    }

    public IReadOnlyList<SetPassiveActionInvocation> Evaluate(
        CombatEvent combatEvent,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> equippedSetPieceCounts,
        SetPassiveRuntimeState runtimeState)
    {
        ArgumentNullException.ThrowIfNull(combatEvent);
        ArgumentNullException.ThrowIfNull(equippedSetPieceCounts);
        ArgumentNullException.ThrowIfNull(runtimeState);

        List<SetPassiveActionInvocation> invocations = [];

        foreach (SetPassiveDefinition definition in _definitions)
        {
            if (definition.Trigger.EventType != combatEvent.Type)
            {
                continue;
            }

            Guid? actorId = ResolveActorId(combatEvent, definition.Trigger.ActorRole);
            if (actorId is null || !HasRequiredPieces(actorId.Value, definition, equippedSetPieceCounts))
            {
                continue;
            }

            SetPassiveProcState state = runtimeState.Get(actorId.Value, definition.Id);
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

            DateTimeOffset? nextCooldown = definition.Conditions.InternalCooldown is { } internalCooldown
                && internalCooldown > TimeSpan.Zero
                ? combatEvent.OccurredAtUtc + internalCooldown
                : null;

            runtimeState.Set(state with
            {
                EventCounter = 0,
                CooldownUntil = nextCooldown,
                LastProcAt = combatEvent.OccurredAtUtc
            });

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

    private static void Validate(SetPassiveDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.SetId);
        ArgumentNullException.ThrowIfNull(definition.Trigger);
        ArgumentNullException.ThrowIfNull(definition.Conditions);
        ArgumentNullException.ThrowIfNull(definition.Actions);

        if (definition.RequiredPieces <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition.RequiredPieces));
        }

        if (definition.Conditions.EveryNth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition.Conditions.EveryNth));
        }

        if (definition.Conditions.InternalCooldown is { } internalCooldown
            && internalCooldown < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(definition.Conditions.InternalCooldown));
        }

        if (definition.Actions.Count == 0)
        {
            throw new ArgumentException("Set passive must define at least one action.", nameof(definition));
        }

        foreach (SetPassiveActionDefinition action in definition.Actions)
        {
            if (action.MaxStacks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(action.MaxStacks));
            }

            if (action.Duration is { } duration && duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(action.Duration));
            }
        }
    }
}
