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

        List<SetPassiveActionInvocation> invocations = new();

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
            if (combatEvent.Tick < state.CooldownUntilTick)
            {
                continue;
            }

            int occurrences = state.Occurrences + 1;
            if (occurrences < definition.Trigger.RequiredOccurrences)
            {
                runtimeState.Set(state with { Occurrences = occurrences });
                continue;
            }

            int cooldownUntilTick = definition.InternalCooldownTicks > 0
                ? checked(combatEvent.Tick + definition.InternalCooldownTicks)
                : 0;

            runtimeState.Set(state with
            {
                Occurrences = 0,
                CooldownUntilTick = cooldownUntilTick
            });

            foreach (SetPassiveActionDefinition action in definition.Actions)
            {
                invocations.Add(new SetPassiveActionInvocation(
                    definition.Id,
                    definition.SetId,
                    actorId.Value,
                    combatEvent.Tick,
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
        ArgumentNullException.ThrowIfNull(definition.Actions);

        if (definition.RequiredPieces <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition.RequiredPieces));
        }

        if (definition.Trigger.RequiredOccurrences <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition.Trigger.RequiredOccurrences));
        }

        if (definition.InternalCooldownTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition.InternalCooldownTicks));
        }

        if (definition.Actions.Count == 0)
        {
            throw new ArgumentException("Set passive must define at least one action.", nameof(definition));
        }
    }
}
