namespace Elyndor.Core.Combat.Encounters;

public sealed record EncounterPlannedAction(
    string PhaseId,
    EncounterActionDefinition Action,
    DateTimeOffset ExecuteAtUtc,
    string? PhaseDisplayName = null);

public static class EncounterActionPlanner
{
    public static IReadOnlyList<EncounterPlannedAction> Plan(
        EncounterDefinition definition,
        EncounterRuntimeSnapshot snapshot,
        EncounterPhaseRuntimeState state,
        IReadOnlyDictionary<string, int>? activeSummonsByMonsterId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(state);

        Dictionary<string, int> activeSummons = activeSummonsByMonsterId is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : activeSummonsByMonsterId.ToDictionary(
                pair => pair.Key,
                pair => Math.Max(0, pair.Value),
                StringComparer.Ordinal);
        List<EncounterPlannedAction> planned = [];

        foreach (EncounterPhaseResolution resolution in EncounterPhaseEngine.Evaluate(
                     definition,
                     snapshot,
                     state))
        {
            if (resolution.AbilityIds is { Count: > 0 })
            {
                planned.Add(new EncounterPlannedAction(
                    resolution.PhaseId,
                    new EncounterActionDefinition(
                        EncounterActionType.ChangeAbilitySet,
                        AbilityIds: resolution.AbilityIds),
                    snapshot.Now,
                    resolution.DisplayName));
            }

            foreach (EncounterActionDefinition sourceAction in resolution.Actions)
            {
                EncounterActionDefinition? action = ResolveAction(
                    sourceAction,
                    activeSummons);
                if (action is null)
                    continue;

                planned.Add(new EncounterPlannedAction(
                    resolution.PhaseId,
                    action,
                    snapshot.Now + (action.Delay ?? TimeSpan.Zero),
                    resolution.DisplayName));
            }
        }

        return planned;
    }

    private static EncounterActionDefinition? ResolveAction(
        EncounterActionDefinition action,
        Dictionary<string, int> activeSummons)
    {
        if (action.Type != EncounterActionType.Summon || action.Summon is null)
            return action;

        SummonDefinition summon = action.Summon;
        int current = activeSummons.GetValueOrDefault(summon.MonsterId);
        int resolvedCount = summon.MaxActive > 0
            ? Math.Min(summon.Count, Math.Max(0, summon.MaxActive - current))
            : summon.Count;
        if (resolvedCount <= 0)
            return null;

        activeSummons[summon.MonsterId] = current + resolvedCount;
        return action with
        {
            Summon = summon with { Count = resolvedCount }
        };
    }
}
