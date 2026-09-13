using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Targeting;

namespace Elyndor.Core.Combat.Sessions;

public sealed record CombatThreatEntrySnapshot(
    Guid ActorId,
    string Name,
    decimal Threat,
    bool IsCurrentTarget,
    Guid? SelectedTargetActorId);

public sealed record CombatThreatSnapshot(
    Guid EnemyActorId,
    string EnemyName,
    Guid? CurrentTargetActorId,
    Guid? ForcedTargetActorId,
    IReadOnlyList<CombatThreatEntrySnapshot> Entries);

public sealed partial class CombatSession
{
    public CombatThreatSnapshot? GetThreatSnapshot(
        Guid participantCharacterId,
        DateTimeOffset now)
    {
        if (!_playerStatesByActorId.TryGetValue(participantCharacterId, out CombatPlayerRuntimeState? viewerState))
            return null;

        Guid enemyActorId = viewerState.SelectedTargetActorId;
        if (!_enemiesById.TryGetValue(enemyActorId, out CombatParticipantDefinition? enemy)
            || !_enemyThreatTables.TryGetValue(enemyActorId, out ThreatTable? threatTable)
            || !_enemyForcedTargets.TryGetValue(enemyActorId, out ForcedTargetState? forcedTarget))
        {
            return null;
        }

        HashSet<Guid> activePlayerActorIds = ActivePlayerActorIds().ToHashSet();
        Guid? currentTargetActorId = GetEnemyCurrentTargetActorId(enemyActorId, now);
        Guid? forcedTargetActorId = forcedTarget.GetActive(now);

        List<CombatThreatEntrySnapshot> entries = [];
        foreach (CombatPlayerRuntimeState state in _playerStatesByActorId.Values)
        {
            Guid actorId = state.Definition.Actor.ActorId;
            if (!activePlayerActorIds.Contains(actorId))
                continue;

            entries.Add(new CombatThreatEntrySnapshot(
                actorId,
                state.Definition.Name,
                threatTable.GetThreat(actorId),
                actorId == currentTargetActorId,
                state.SelectedTargetActorId == Guid.Empty ? null : state.SelectedTargetActorId));
        }

        if (_companion is not null && !_companion.Actor.IsDead)
        {
            entries.Add(new CombatThreatEntrySnapshot(
                _companion.Actor.ActorId,
                _companion.Name,
                threatTable.GetThreat(_companion.Actor.ActorId),
                _companion.Actor.ActorId == currentTargetActorId,
                null));
        }

        return new CombatThreatSnapshot(
            enemyActorId,
            enemy.Name,
            currentTargetActorId,
            forcedTargetActorId,
            entries
                .OrderByDescending(entry => entry.Threat)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                .ToArray());
    }

    private Guid? GetEnemyCurrentTargetActorId(Guid enemyActorId, DateTimeOffset now)
    {
        if (!_enemyThreatTables.TryGetValue(enemyActorId, out ThreatTable? threatTable)
            || !_enemyForcedTargets.TryGetValue(enemyActorId, out ForcedTargetState? forcedTarget))
        {
            return null;
        }

        List<CombatActor> candidates = ActivePlayerActorIds()
            .Select(actorId => new CombatActor(actorId, CombatActorSide.Friendly))
            .ToList();
        if (_companion is not null && !_companion.Actor.IsDead)
            candidates.Add(new CombatActor(_companion.Actor.ActorId, CombatActorSide.Friendly));

        return TargetSelectionPolicy.SelectForcedOrThreatTarget(
            forcedTarget,
            threatTable,
            candidates,
            now);
    }
}
