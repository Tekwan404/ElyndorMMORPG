using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private readonly Dictionary<Guid, Dictionary<Guid, decimal>>
        _threatByEnemyActorId = [];

    private void InitializeThreatTables()
    {
        foreach (CombatParticipantDefinition enemy in _enemies)
            EnsureThreatTable(enemy.Actor.ActorId);
    }

    private void EnsureThreatTable(Guid enemyActorId)
    {
        if (_threatByEnemyActorId.ContainsKey(enemyActorId))
            return;

        Dictionary<Guid, decimal> threat = new()
        {
            [_player.Actor.ActorId] = 1m
        };
        if (_companion is not null)
            threat[_companion.Actor.ActorId] = 0m;

        _threatByEnemyActorId[enemyActorId] = threat;
    }

    private void RegisterThreat(CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.DamageDealt
            || combatEvent.Amount <= 0
            || combatEvent.SourceActorId is not { } sourceActorId
            || combatEvent.TargetActorId is not { } targetActorId
            || !_enemiesById.ContainsKey(targetActorId)
            || sourceActorId != _player.Actor.ActorId
                && (_companion is null
                    || sourceActorId != _companion.Actor.ActorId))
        {
            return;
        }

        EnsureThreatTable(targetActorId);
        decimal multiplier = 1m;
        if (sourceActorId == _player.Actor.ActorId
            && string.Equals(
                combatEvent.DefinitionId,
                "AUTO_ATTACK",
                StringComparison.Ordinal)
            && TryGetGuardianHook(
                "G-1-4",
                out ResolvedTalentEventHook heavyPresence))
        {
            multiplier += heavyPresence.Value / 100m;
        }

        Dictionary<Guid, decimal> threat = _threatByEnemyActorId[targetActorId];
        threat[sourceActorId] =
            threat.GetValueOrDefault(sourceActorId) + combatEvent.Amount * multiplier;
    }

    private CombatParticipantDefinition ResolveEnemyPrimaryTarget(
        CombatParticipantDefinition enemy)
    {
        EnsureThreatTable(enemy.Actor.ActorId);
        Dictionary<Guid, decimal> threat =
            _threatByEnemyActorId[enemy.Actor.ActorId];

        IEnumerable<CombatParticipantDefinition> candidates = [_player];
        if (_companion is not null && !_companion.Actor.IsDead)
            candidates = candidates.Append(_companion);

        return candidates
            .Where(candidate => !candidate.Actor.IsDead)
            .OrderByDescending(candidate =>
                threat.GetValueOrDefault(candidate.Actor.ActorId))
            .ThenBy(candidate =>
                candidate.Kind == CombatActorKind.Player ? 0 : 1)
            .First();
    }

    private Guid[] ResolveEnemyHostileActorIds(
        CombatParticipantDefinition enemy)
    {
        EnsureThreatTable(enemy.Actor.ActorId);
        Dictionary<Guid, decimal> threat =
            _threatByEnemyActorId[enemy.Actor.ActorId];

        IEnumerable<CombatParticipantDefinition> candidates = [_player];
        if (_companion is not null && !_companion.Actor.IsDead)
            candidates = candidates.Append(_companion);

        return candidates
            .Where(candidate => !candidate.Actor.IsDead)
            .OrderByDescending(candidate =>
                threat.GetValueOrDefault(candidate.Actor.ActorId))
            .ThenBy(candidate =>
                candidate.Kind == CombatActorKind.Player ? 0 : 1)
            .Select(candidate => candidate.Actor.ActorId)
            .ToArray();
    }

    private bool TryGetGuardianHook(
        string talentId,
        out ResolvedTalentEventHook hook)
    {
        hook = IsWarrior
            ? _playerTalents.EventHooks.FirstOrDefault(item =>
                string.Equals(item.TalentId, talentId, StringComparison.Ordinal))!
            : null!;
        return hook is not null;
    }
}
