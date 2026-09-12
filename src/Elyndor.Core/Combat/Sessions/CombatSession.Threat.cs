using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const decimal HealingThreatCoefficient = 0.5m;

    // Compatibility hook kept because the constructor still calls it before the
    // authoritative _enemyThreatTables dictionary is created. Threat state now lives
    // exclusively in _enemyThreatTables.
    private void InitializeThreatTables()
    {
    }

    private void RegisterThreat(CombatEvent combatEvent)
    {
        if (combatEvent.SourceActorId is not { } sourceActorId
            || !IsPartyActor(sourceActorId)
            || combatEvent.Amount <= 0)
        {
            return;
        }

        if (combatEvent.Type == CombatEventType.HealingApplied)
        {
            RegisterHealingThreat(sourceActorId, combatEvent.Amount);
            SuppressLegacyAutomaticThreat(combatEvent, sourceActorId);
            return;
        }

        if (combatEvent.TargetActorId is not { } targetActorId
            || !_enemyThreatTables.TryGetValue(targetActorId, out ThreatTable? threatTable))
        {
            return;
        }

        switch (combatEvent.Type)
        {
            case CombatEventType.DamageDealt:
                threatTable.AddThreat(
                    sourceActorId,
                    combatEvent.Amount,
                    ResolveThreatMultiplier(sourceActorId, combatEvent));
                threatTable.SuppressNextAutomaticAdd(sourceActorId);
                break;

            case CombatEventType.TauntApplied:
                RaiseTaunterToTopThreat(threatTable, sourceActorId);
                threatTable.SuppressNextAutomaticAdd(sourceActorId);
                break;

            default:
                // CriticalHit, EffectTicked, DamageBlocked, ShieldAbsorbed and other
                // event telemetry can describe the same action as DamageDealt. They
                // must not generate a second implicit copy of threat.
                threatTable.SuppressNextAutomaticAdd(sourceActorId);
                break;
        }
    }

    private void RegisterHealingThreat(Guid sourceActorId, decimal effectiveHealing)
    {
        decimal multiplier = sourceActorId == _player.Actor.ActorId
            ? GuardianThreatMultiplier
            : 1m;

        foreach (CombatParticipantDefinition enemy in _enemies.Where(item => !item.Actor.IsDead))
        {
            if (_enemyThreatTables.TryGetValue(enemy.Actor.ActorId, out ThreatTable? threatTable))
            {
                threatTable.AddThreat(
                    sourceActorId,
                    effectiveHealing,
                    HealingThreatCoefficient * multiplier);
            }
        }
    }

    private void SuppressLegacyAutomaticThreat(
        CombatEvent combatEvent,
        Guid sourceActorId)
    {
        if (combatEvent.TargetActorId is { } targetActorId
            && _enemyThreatTables.TryGetValue(targetActorId, out ThreatTable? threatTable))
        {
            threatTable.SuppressNextAutomaticAdd(sourceActorId);
        }
    }

    private decimal ResolveThreatMultiplier(
        Guid sourceActorId,
        CombatEvent combatEvent)
    {
        if (sourceActorId != _player.Actor.ActorId)
            return 1m;

        return string.Equals(
            combatEvent.DefinitionId,
            "AUTO_ATTACK",
            StringComparison.Ordinal)
                ? GuardianAutoAttackThreatMultiplier
                : GuardianThreatMultiplier;
    }

    private static void RaiseTaunterToTopThreat(
        ThreatTable threatTable,
        Guid sourceActorId)
    {
        decimal highestThreat = threatTable.Snapshot.Count == 0
            ? 0
            : threatTable.Snapshot.Values.Max();
        decimal currentThreat = threatTable.GetThreat(sourceActorId);
        decimal minimumThreat = highestThreat + 1m;
        if (currentThreat < minimumThreat)
            threatTable.AddThreat(sourceActorId, minimumThreat - currentThreat);
    }

    private CombatParticipantDefinition ResolveEnemyPrimaryTarget(
        CombatParticipantDefinition enemy,
        DateTimeOffset now)
    {
        CombatParticipantDefinition[] candidates = ActiveEnemyTargetCandidates();
        if (candidates.Length == 0)
            return _player;

        Guid? selected = TargetSelectionPolicy.SelectForcedOrThreatTarget(
            _enemyForcedTargets[enemy.Actor.ActorId],
            _enemyThreatTables[enemy.Actor.ActorId],
            candidates
                .Select(candidate => new CombatActor(
                    candidate.Actor.ActorId,
                    CombatActorSide.Friendly))
                .ToArray(),
            now);

        return candidates.FirstOrDefault(candidate =>
                   candidate.Actor.ActorId == selected)
               ?? candidates[0];
    }

    private Guid[] ResolveEnemyHostileActorIds(
        CombatParticipantDefinition enemy,
        DateTimeOffset now)
    {
        CombatParticipantDefinition[] candidates = ActiveEnemyTargetCandidates();
        if (candidates.Length == 0)
            return [];

        CombatParticipantDefinition primary = ResolveEnemyPrimaryTarget(enemy, now);
        ThreatTable threatTable = _enemyThreatTables[enemy.Actor.ActorId];

        return candidates
            .OrderByDescending(candidate =>
                candidate.Actor.ActorId == primary.Actor.ActorId)
            .ThenByDescending(candidate =>
                threatTable.GetThreat(candidate.Actor.ActorId))
            .ThenBy(candidate =>
                candidate.Kind == CombatActorKind.Player ? 0 : 1)
            .Select(candidate => candidate.Actor.ActorId)
            .ToArray();
    }

    private CombatParticipantDefinition[] ActiveEnemyTargetCandidates()
    {
        IEnumerable<CombatParticipantDefinition> candidates =
            _participantRoster.Participants
                .Where(item => item.Status == CombatParticipantStatus.Active)
                .Select(item => _playerStatesByActorId[item.ActorId].Definition)
                .Where(candidate => !candidate.Actor.IsDead);

        if (_companion is not null && !_companion.Actor.IsDead)
            candidates = candidates.Append(_companion);

        return candidates.ToArray();
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
