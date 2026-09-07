namespace Elyndor.Core.Combat.Targeting;

public static class TargetSelectionPolicy
{
    public static Guid? SelectForcedOrThreatTarget(
        ForcedTargetState forcedTarget,
        ThreatTable threatTable,
        IReadOnlyList<CombatActor> candidates,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(forcedTarget);
        ArgumentNullException.ThrowIfNull(threatTable);
        ArgumentNullException.ThrowIfNull(candidates);

        HashSet<Guid> aliveCandidates = candidates
            .Select(candidate => candidate.ActorId)
            .ToHashSet();
        Guid? forced = forcedTarget.GetActive(now);
        if (forced is { } forcedActorId && aliveCandidates.Contains(forcedActorId))
            return forcedActorId;

        return threatTable.SelectTarget(candidates.Select(candidate => candidate.ActorId).ToArray());
    }
}
