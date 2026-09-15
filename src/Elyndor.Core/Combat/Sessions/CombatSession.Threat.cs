using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private const decimal HealingThreatCoefficient = 0.5m;

    private static void InitializeThreatTables()
    {
    }

    private static void EnsureThreatTable(Guid enemyActorId)
    {
        _ = enemyActorId;
    }

    private void RegisterThreat(CombatEvent combatEvent)
    {
        ProcessMirrorEncounterEvent(combatEvent);
        ProcessPaladinKernelEvent(combatEvent);

        if (combatEvent.SourceActorId is not { } sourceActorId
            || !IsPartyActor(sourceActorId)
            || combatEvent.Amount <= 0)
        {
            return;
        }

        if (combatEvent.Type == CombatEventType.HealingApplied)
        {
            RegisterHealingThreat(
                sourceActorId,
                combatEvent.Amount,
                combatEvent.OccurredAtUtc);
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
                threatTable.SuppressNextAutomaticAdd(sourceActorId);
                break;
        }
    }

    private void RegisterHealingThreat(
        Guid sourceActorId,
        decimal effectiveHealing,
        DateTimeOffset now)
    {
        foreach (CombatParticipantDefinition enemy in _enemies.Where(item => !item.Actor.IsDead))
        {
            if (!_enemyThreatTables.TryGetValue(enemy.Actor.ActorId, out ThreatTable? threatTable))
                continue;

            decimal multiplier = sourceActorId == _player.Actor.ActorId
                ? ResolvePlayerThreatMultiplier(enemy.Actor.ActorId, now)
                : 1m;
            threatTable.AddThreat(
                sourceActorId,
                effectiveHealing,
                HealingThreatCoefficient * multiplier);
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

        decimal multiplier = ResolvePlayerThreatMultiplier(
            combatEvent.TargetActorId,
            combatEvent.OccurredAtUtc);

        if (IsMage)
            multiplier *= ResolveMageThreatMultiplier(combatEvent);

        return Math.Max(0, multiplier);
    }

    private decimal ResolveMageThreatMultiplier(CombatEvent combatEvent)
    {
        if (combatEvent.DamageType != DamageType.Magical)
            return 1m;

        decimal multiplier = 1m;
        if (TryGetMageHook("A-1-4", out ResolvedTalentEventHook subtlety))
            multiplier *= Math.Max(0, 1 - subtlety.Value / 100m);

        string? school = ResolveMageDamageSchool(combatEvent.DefinitionId);
        if (string.Equals(school, "FIRE", StringComparison.Ordinal)
            && TryGetMageHook("F-1-3", out ResolvedTalentEventHook burningSoul))
        {
            multiplier *= Math.Max(0, 1 - burningSoul.Value / 100m);
        }
        else if (string.Equals(school, "FROST", StringComparison.Ordinal)
            && TryGetMageHook("I-3-4", out ResolvedTalentEventHook focusedIce))
        {
            multiplier *= Math.Max(0, 1 - focusedIce.SecondaryValue / 100m);
        }

        return multiplier;
    }

    private string? ResolveMageDamageSchool(string? definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
            return null;

        if (_abilities.TryGetValue(definitionId, out AbilityDefinition? ability)
            && ability.IsSpell)
        {
            return ability.School;
        }

        if (IsFireDamageDefinition(definitionId)
            || string.Equals(definitionId, "MAGE_PYROBLAST_BURN", StringComparison.Ordinal))
        {
            return "FIRE";
        }

        if (string.Equals(definitionId, ArcaneEchoEffectId, StringComparison.Ordinal))
            return "ARCANE";

        if (string.Equals(definitionId, "MAGE_DEEP_FREEZE_HIT", StringComparison.Ordinal))
            return "FROST";

        return null;
    }

    private decimal ResolvePlayerThreatMultiplier(
        Guid? targetActorId,
        DateTimeOffset now)
    {
        decimal multiplier = targetActorId is { } targetId
            ? GuardianThreatMultiplierForTarget(targetId, now)
            : 1m;

        if (IsActivePaladin
            && TryGetPaladinHook("P-1-3", out ResolvedTalentEventHook righteousFury))
        {
            multiplier *= 1m + 0.35m * righteousFury.Rank;
        }

        return multiplier;
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
