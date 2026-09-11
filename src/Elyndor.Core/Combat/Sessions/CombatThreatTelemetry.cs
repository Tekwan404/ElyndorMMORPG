using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Targeting;

namespace Elyndor.Core.Combat.Sessions;

public sealed record CombatThreatEntrySnapshot(
    Guid ActorId,
    string Name,
    decimal Threat,
    bool IsCurrentTarget);

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

        HashSet<Guid> activePlayerActorIds = _participantRoster.Participants
            .Where(item => item.Status is CombatParticipantStatus.Active)
            .Select(item => item.ActorId)
            .ToHashSet();

        List<CombatActor> candidates = _playerStatesByActorId.Values
            .Where(state => activePlayerActorIds.Contains(state.Definition.Actor.ActorId)
                && !state.Definition.Actor.IsDead)
            .Select(state => new CombatActor(
                state.Definition.Actor.ActorId,
                CombatActorSide.Friendly))
            .ToList();

        if (_companion is not null && !_companion.Actor.IsDead)
        {
            candidates.Add(new CombatActor(
                _companion.Actor.ActorId,
                CombatActorSide.Friendly));
        }

        Guid? currentTargetActorId = TargetSelectionPolicy.SelectForcedOrThreatTarget(
            forcedTarget,
            threatTable,
            candidates,
            now);
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
                actorId == currentTargetActorId));
        }

        if (_companion is not null && !_companion.Actor.IsDead)
        {
            entries.Add(new CombatThreatEntrySnapshot(
                _companion.Actor.ActorId,
                _companion.Name,
                threatTable.GetThreat(_companion.Actor.ActorId),
                _companion.Actor.ActorId == currentTargetActorId));
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
}
