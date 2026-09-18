namespace Elyndor.Core.Combat.Encounters;

public sealed class EncounterPhaseRuntimeState
{
    private readonly HashSet<string> _firedOnce = new(StringComparer.Ordinal);

    public string? CurrentPhaseId { get; private set; }

    public bool HasFired(string phaseId) => _firedOnce.Contains(phaseId);

    internal void MarkFired(EncounterPhaseDefinition phase)
    {
        if (phase.Trigger.Once)
            _firedOnce.Add(phase.Id);
        CurrentPhaseId = phase.Id;
    }

    public void Reset()
    {
        _firedOnce.Clear();
        CurrentPhaseId = null;
    }
}

public sealed record EncounterPhaseResolution(
    string PhaseId,
    IReadOnlyList<EncounterActionDefinition> Actions,
    IReadOnlyList<string>? AbilityIds,
    string? DisplayName);

public static class EncounterPhaseEngine
{
    public static IReadOnlyList<EncounterPhaseResolution> Evaluate(
        EncounterDefinition definition,
        EncounterRuntimeSnapshot snapshot,
        EncounterPhaseRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(state);

        IReadOnlyList<string> errors = EncounterDefinitionValidator.Validate(definition);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Encounter '{definition.Id}' is invalid: {string.Join(" | ", errors)}");
        }

        List<EncounterPhaseResolution> resolutions = [];
        foreach (EncounterPhaseDefinition phase in definition.Phases)
        {
            if (phase.Trigger.Once && state.HasFired(phase.Id))
                continue;
            if (!Matches(phase.Trigger, snapshot, state))
                continue;

            state.MarkFired(phase);
            resolutions.Add(new EncounterPhaseResolution(
                phase.Id,
                phase.Actions,
                phase.AbilityIds,
                phase.DisplayName));
        }

        return resolutions;
    }

    private static bool Matches(
        EncounterTriggerDefinition trigger,
        EncounterRuntimeSnapshot snapshot,
        EncounterPhaseRuntimeState state) =>
        trigger.Type switch
        {
            EncounterTriggerType.CombatStart => snapshot.Now <= snapshot.StartedAtUtc,
            EncounterTriggerType.HpAtOrBelow => snapshot.HpPercent <= trigger.Threshold,
            EncounterTriggerType.ElapsedTime => trigger.Elapsed is { } elapsed
                && snapshot.Now >= snapshot.StartedAtUtc + elapsed,
            EncounterTriggerType.ResourceAtOrBelow =>
                snapshot.ResourcePercent <= trigger.Threshold,
            EncounterTriggerType.ResourceAtOrAbove =>
                snapshot.ResourcePercent >= trigger.Threshold,
            EncounterTriggerType.AddDeath => MatchesEvent(
                trigger,
                snapshot,
                EncounterTriggerType.AddDeath),
            EncounterTriggerType.EffectExpired => MatchesEvent(
                trigger,
                snapshot,
                EncounterTriggerType.EffectExpired),
            EncounterTriggerType.CastInterrupted => MatchesEvent(
                trigger,
                snapshot,
                EncounterTriggerType.CastInterrupted),
            EncounterTriggerType.PhaseStart => string.Equals(
                trigger.PhaseId,
                snapshot.CurrentPhaseId ?? state.CurrentPhaseId,
                StringComparison.Ordinal),
            _ => false
        };

    private static bool MatchesEvent(
        EncounterTriggerDefinition trigger,
        EncounterRuntimeSnapshot snapshot,
        EncounterTriggerType expectedType) =>
        snapshot.EventType == expectedType
        && string.Equals(
            snapshot.EventDefinitionId,
            trigger.DefinitionId,
            StringComparison.Ordinal);
}