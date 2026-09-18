using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;

namespace Elyndor.Core.Combat.Simulation;

public enum CombatSimulationEndReason
{
    Victory,
    Defeat,
    Cancelled,
    Timeout,
    Stalled,
    StepLimit
}

public sealed record CombatSimulationDecisionContext(
    CombatSessionSnapshot Snapshot,
    IReadOnlyList<CombatEvent> EventsSinceLastDecision,
    CombatCommandResult? LastCommandResult,
    int Step);

public sealed record CombatSimulationScenario(
    string Id,
    int Seed,
    Func<IGameRandom, CombatSession> SessionFactory,
    TimeSpan MaximumDuration,
    int MaximumSteps = 10_000,
    Func<CombatSimulationDecisionContext, CombatCommand?>? DecisionPolicy = null,
    IReadOnlySet<string>? DispelAbilityIds = null);

public sealed record CombatSimulationMetrics(
    TimeSpan SimulatedDuration,
    TimeSpan? TimeToKill,
    decimal IncomingDamage,
    decimal IncomingDamagePerSecond,
    int InterruptCount,
    int DispelCount,
    TimeSpan AddUptime,
    int PeakActiveAdds,
    int PartyDeaths,
    Guid? LastPartyDeathActorId,
    string? LastPartyDeathCauseId);

public sealed record CombatSimulationResult(
    string ScenarioId,
    int Seed,
    CombatSimulationEndReason EndReason,
    CombatSessionSnapshot FinalSnapshot,
    CombatSimulationMetrics Metrics,
    IReadOnlyList<CombatEvent> Events);

public static class CombatScenarioSimulator
{
    public static CombatSimulationResult Run(CombatSimulationScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenario.Id);
        ArgumentNullException.ThrowIfNull(scenario.SessionFactory);
        if (scenario.MaximumDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(scenario),
                "Simulation duration must be positive.");
        if (scenario.MaximumSteps <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(scenario),
                "Simulation step limit must be positive.");

        CombatSession session = scenario.SessionFactory(new SeededGameRandom(scenario.Seed));
        if (session is null)
            throw new InvalidOperationException("Combat simulation session factory returned null.");

        CombatSessionSnapshot initialSnapshot = session.Snapshot();
        DateTimeOffset startedAtUtc = initialSnapshot.ServerTimeUtc;
        DateTimeOffset deadlineUtc = startedAtUtc + scenario.MaximumDuration;
        HashSet<Guid> partyActorIds = ResolvePartyActorIds(initialSnapshot);

        List<CombatEvent> events = session.GetEventsAfter(0)
            .OrderBy(item => item.Sequence)
            .ToList();
        long lastSequence = events.Count == 0 ? 0 : events[^1].Sequence;
        IReadOnlyList<CombatEvent> recentEvents = events.ToArray();
        CombatCommandResult? lastCommandResult = null;

        for (var step = 0; step < scenario.MaximumSteps; step++)
        {
            CombatSessionSnapshot snapshot = session.Snapshot();
            if (snapshot.Status != CombatSessionStatus.Active)
            {
                return BuildResult(
                    scenario,
                    initialSnapshot,
                    snapshot,
                    MapEndReason(snapshot.Status),
                    partyActorIds,
                    events);
            }

            if (snapshot.ServerTimeUtc >= deadlineUtc)
            {
                return BuildResult(
                    scenario,
                    initialSnapshot,
                    snapshot,
                    CombatSimulationEndReason.Timeout,
                    partyActorIds,
                    events);
            }

            CombatCommand? command = scenario.DecisionPolicy?.Invoke(
                new CombatSimulationDecisionContext(
                    snapshot,
                    recentEvents,
                    lastCommandResult,
                    step));
            if (command is not null)
            {
                lastCommandResult = session.Handle(command, snapshot.ServerTimeUtc);
                recentEvents = CollectNewEvents(session, events, ref lastSequence);
                continue;
            }

            DateTimeOffset? nextDueAtUtc = session.NextDueAtUtc;
            if (nextDueAtUtc is null)
            {
                return BuildResult(
                    scenario,
                    initialSnapshot,
                    snapshot,
                    CombatSimulationEndReason.Stalled,
                    partyActorIds,
                    events);
            }

            if (nextDueAtUtc > deadlineUtc)
            {
                session.AdvanceTo(deadlineUtc);
                recentEvents = CollectNewEvents(session, events, ref lastSequence);
                CombatSessionSnapshot deadlineSnapshot = session.Snapshot();
                CombatSimulationEndReason endReason = deadlineSnapshot.Status == CombatSessionStatus.Active
                    ? CombatSimulationEndReason.Timeout
                    : MapEndReason(deadlineSnapshot.Status);
                return BuildResult(
                    scenario,
                    initialSnapshot,
                    deadlineSnapshot,
                    endReason,
                    partyActorIds,
                    events);
            }

            DateTimeOffset previousTimeUtc = snapshot.ServerTimeUtc;
            session.AdvanceTo(nextDueAtUtc.Value);
            recentEvents = CollectNewEvents(session, events, ref lastSequence);
            lastCommandResult = null;
            CombatSessionSnapshot advanced = session.Snapshot();
            if (advanced.Status == CombatSessionStatus.Active
                && advanced.ServerTimeUtc == previousTimeUtc
                && recentEvents.Count == 0)
            {
                return BuildResult(
                    scenario,
                    initialSnapshot,
                    advanced,
                    CombatSimulationEndReason.Stalled,
                    partyActorIds,
                    events);
            }
        }

        CombatSessionSnapshot finalSnapshot = session.Snapshot();
        CombatSimulationEndReason finalReason = finalSnapshot.Status == CombatSessionStatus.Active
            ? CombatSimulationEndReason.StepLimit
            : MapEndReason(finalSnapshot.Status);
        return BuildResult(
            scenario,
            initialSnapshot,
            finalSnapshot,
            finalReason,
            partyActorIds,
            events);
    }

    private static CombatEvent[] CollectNewEvents(
        CombatSession session,
        List<CombatEvent> events,
        ref long lastSequence)
    {
        CombatEvent[] newEvents = session.GetEventsAfter(lastSequence)
            .OrderBy(item => item.Sequence)
            .ToArray();
        if (newEvents.Length == 0)
            return newEvents;

        events.AddRange(newEvents);
        lastSequence = newEvents[^1].Sequence;
        return newEvents;
    }

    private static HashSet<Guid> ResolvePartyActorIds(CombatSessionSnapshot snapshot)
    {
        HashSet<Guid> actorIds = snapshot.Players is { Count: > 0 }
            ? snapshot.Players.Select(item => item.ActorId).ToHashSet()
            : new HashSet<Guid> { snapshot.Player.ActorId };
        if (snapshot.Companion is not null)
            actorIds.Add(snapshot.Companion.ActorId);
        return actorIds;
    }

    private static CombatSimulationResult BuildResult(
        CombatSimulationScenario scenario,
        CombatSessionSnapshot initialSnapshot,
        CombatSessionSnapshot finalSnapshot,
        CombatSimulationEndReason endReason,
        IReadOnlySet<Guid> partyActorIds,
        IReadOnlyList<CombatEvent> events)
    {
        CombatSimulationMetrics metrics = BuildMetrics(
            scenario,
            initialSnapshot,
            finalSnapshot,
            endReason,
            partyActorIds,
            events);
        return new CombatSimulationResult(
            scenario.Id,
            scenario.Seed,
            endReason,
            finalSnapshot,
            metrics,
            events.ToArray());
    }

    private static CombatSimulationMetrics BuildMetrics(
        CombatSimulationScenario scenario,
        CombatSessionSnapshot initialSnapshot,
        CombatSessionSnapshot finalSnapshot,
        CombatSimulationEndReason endReason,
        IReadOnlySet<Guid> partyActorIds,
        IReadOnlyList<CombatEvent> events)
    {
        TimeSpan duration = finalSnapshot.ServerTimeUtc - initialSnapshot.ServerTimeUtc;
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        decimal incomingDamage = events
            .Where(item =>
                item.Type == CombatEventType.DamageDealt
                && item.TargetActorId is { } targetActorId
                && partyActorIds.Contains(targetActorId))
            .Sum(item => item.Amount);
        decimal incomingDps = duration.TotalSeconds <= 0
            ? 0
            : incomingDamage / (decimal)duration.TotalSeconds;

        HashSet<Guid> enemyActorIds = (initialSnapshot.Enemies ?? [initialSnapshot.Enemy])
            .Select(item => item.ActorId)
            .ToHashSet();
        foreach (CombatEvent summoned in events.Where(item => item.Type == CombatEventType.ActorSummoned))
            enemyActorIds.Add(summoned.ActorId);

        int interruptCount = events.Count(item =>
            item.Type == CombatEventType.AbilityInterrupted
            && enemyActorIds.Contains(item.ActorId));
        IReadOnlySet<string> dispelAbilityIds = scenario.DispelAbilityIds
            ?? new HashSet<string>(StringComparer.Ordinal);
        int dispelCount = events.Count(item =>
            item.Type == CombatEventType.AbilityUsed
            && item.SourceActorId is { } sourceActorId
            && partyActorIds.Contains(sourceActorId)
            && item.DefinitionId is { } definitionId
            && dispelAbilityIds.Contains(definitionId));

        (TimeSpan addUptime, int peakActiveAdds) = CalculateAddUptime(
            events,
            finalSnapshot.ServerTimeUtc);
        CombatEvent[] partyDeaths = events
            .Where(item =>
                item.Type == CombatEventType.ActorDied
                && partyActorIds.Contains(item.ActorId))
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Sequence)
            .ToArray();
        CombatEvent? lastPartyDeath = partyDeaths.LastOrDefault();

        return new CombatSimulationMetrics(
            duration,
            endReason == CombatSimulationEndReason.Victory ? duration : null,
            incomingDamage,
            incomingDps,
            interruptCount,
            dispelCount,
            addUptime,
            peakActiveAdds,
            partyDeaths.Length,
            lastPartyDeath?.ActorId,
            lastPartyDeath?.DefinitionId);
    }

    private static (TimeSpan TotalUptime, int PeakActive) CalculateAddUptime(
        IReadOnlyList<CombatEvent> events,
        DateTimeOffset finishedAtUtc)
    {
        Dictionary<Guid, DateTimeOffset> activeAdds = [];
        TimeSpan totalUptime = TimeSpan.Zero;
        var peakActive = 0;
        foreach (CombatEvent combatEvent in events
                     .OrderBy(item => item.OccurredAtUtc)
                     .ThenBy(item => item.Sequence))
        {
            if (combatEvent.Type == CombatEventType.ActorSummoned)
            {
                activeAdds[combatEvent.ActorId] = combatEvent.OccurredAtUtc;
                peakActive = Math.Max(peakActive, activeAdds.Count);
                continue;
            }

            if (combatEvent.Type != CombatEventType.ActorDied
                || !activeAdds.Remove(combatEvent.ActorId, out DateTimeOffset spawnedAtUtc))
            {
                continue;
            }

            if (combatEvent.OccurredAtUtc > spawnedAtUtc)
                totalUptime += combatEvent.OccurredAtUtc - spawnedAtUtc;
        }

        foreach (DateTimeOffset spawnedAtUtc in activeAdds.Values)
        {
            if (finishedAtUtc > spawnedAtUtc)
                totalUptime += finishedAtUtc - spawnedAtUtc;
        }

        return (totalUptime, peakActive);
    }

    private static CombatSimulationEndReason MapEndReason(CombatSessionStatus status) =>
        status switch
        {
            CombatSessionStatus.Victory => CombatSimulationEndReason.Victory,
            CombatSessionStatus.Defeat => CombatSimulationEndReason.Defeat,
            CombatSessionStatus.Cancelled => CombatSimulationEndReason.Cancelled,
            _ => CombatSimulationEndReason.Stalled
        };
}
