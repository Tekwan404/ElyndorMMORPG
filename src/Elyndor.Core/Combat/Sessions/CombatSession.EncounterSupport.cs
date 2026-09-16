using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Monsters;

namespace Elyndor.Core.Combat.Sessions;

public sealed record EncounterEnemyProfile(
    MonsterDefinition Monster,
    MonsterAiProfile AiProfile);

public sealed partial class CombatSession
{
    private CombatParticipantDefinition ConfigurePrimaryEncounterResource(
        string resourceType,
        decimal maxResource,
        decimal currentResource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        if (_primaryEnemyActorId == Guid.Empty)
            throw new InvalidOperationException("Encounter has no primary enemy.");

        CombatParticipantDefinition current = _enemiesById[_primaryEnemyActorId];
        current.Actor.ConfigureResource(maxResource, currentResource);
        CombatParticipantDefinition updated = current with { ResourceType = resourceType };
        _enemiesById[_primaryEnemyActorId] = updated;
        int index = _enemies.FindIndex(item => item.Actor.ActorId == _primaryEnemyActorId);
        if (index < 0)
            throw new InvalidOperationException("Primary enemy is missing from the encounter roster.");
        _enemies[index] = updated;
        return updated;
    }

    private CombatParticipantDefinition SpawnEncounterEnemy(
        EncounterEnemyProfile profile,
        Guid sourceActorId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(profile.Monster);
        ArgumentNullException.ThrowIfNull(profile.AiProfile);

        CombatParticipantDefinition summoned = CreateSummonedParticipant(profile.Monster);
        CombatActorState[] existingEnemyActors = _enemies
            .Select(enemy => enemy.Actor)
            .ToArray();

        foreach (CombatPlayerRuntimeState playerState in _playerStatesByActorId.Values)
            playerState.Runtime.AddActor(summoned.Actor);
        _companionRuntime?.AddActor(summoned.Actor);
        foreach (CombatRuntimeState runtime in _enemyRuntimes.Values)
            runtime.AddActor(summoned.Actor);

        _enemies.Add(summoned);
        _enemiesById.Add(summoned.Actor.ActorId, summoned);
        _enemyRuntimes.Add(
            summoned.Actor.ActorId,
            CreateRuntime(
                summoned.Actor,
                _playerStatesByActorId.Values.Select(state => state.Definition.Actor)
                    .Concat(existingEnemyActors)
                    .Concat(_companion is null ? [] : new[] { _companion.Actor })));
        _enemyAiRuntimes.Add(
            summoned.Actor.ActorId,
            new EnemyAiRuntime(
                profile.AiProfile,
                now + summoned.AutoAttack.Interval));

        ThreatTable threat = new();
        foreach (CombatPlayerRuntimeState playerState in _playerStatesByActorId.Values)
            threat.AddThreat(playerState.Definition.Actor.ActorId, 1);
        if (_companion is not null)
            threat.AddThreat(_companion.Actor.ActorId, 1);
        _enemyThreatTables.Add(summoned.Actor.ActorId, threat);
        _enemyForcedTargets.Add(summoned.Actor.ActorId, new ForcedTargetState());

        Append(new CombatEvent(
            CombatEventType.ActorSummoned,
            now,
            summoned.Actor.ActorId,
            summoned.DefinitionId,
            SourceActorId: sourceActorId,
            TargetActorId: summoned.Actor.ActorId));
        return summoned;
    }

    private void DeactivateEncounterEnemy(
        Guid actorId,
        Guid sourceActorId,
        DateTimeOffset now,
        string definitionId,
        Guid? preferredNextTargetActorId = null)
    {
        if (!_enemiesById.TryGetValue(actorId, out CombatParticipantDefinition? enemy)
            || enemy.Actor.IsDead)
        {
            return;
        }

        enemy.Actor.SetCurrentHp(0);
        _deadActors.Add(actorId);
        if (_enemyAiRuntimes.TryGetValue(actorId, out EnemyAiRuntime? ai))
        {
            ai.State = MonsterAiState.Dead;
            ai.NextActionAtUtc = null;
        }
        if (_enemyRuntimes.TryGetValue(actorId, out CombatRuntimeState? runtime))
            runtime.ActiveCast = null;

        Append(new CombatEvent(
            CombatEventType.ActorDied,
            now,
            actorId,
            definitionId,
            SourceActorId: sourceActorId,
            TargetActorId: actorId));
        RetargetPlayersFromEncounterEnemy(actorId, now, preferredNextTargetActorId);
    }

    private void ReviveEncounterEnemy(
        Guid actorId,
        decimal hpPercent,
        Guid sourceActorId,
        DateTimeOffset now,
        string definitionId)
    {
        if (!_enemiesById.TryGetValue(actorId, out CombatParticipantDefinition? enemy))
            return;

        decimal previousHp = enemy.Actor.CurrentHp;
        decimal restoredHp = Math.Max(1, enemy.Actor.MaxHp * Math.Clamp(hpPercent, 0, 1));
        enemy.Actor.SetCurrentHp(restoredHp);
        _deadActors.Remove(actorId);
        if (_enemyRuntimes.TryGetValue(actorId, out CombatRuntimeState? runtime))
            runtime.ActiveCast = null;
        EnemyAiRuntime ai = _enemyAiRuntimes[actorId];
        ai.State = MonsterAiState.InCombat;
        ai.NextActionAtUtc = now + enemy.AutoAttack.Interval;

        Append(new CombatEvent(
            CombatEventType.HealingApplied,
            now,
            actorId,
            definitionId,
            enemy.Actor.CurrentHp - previousHp,
            SourceActorId: sourceActorId,
            TargetActorId: actorId));
    }

    private void HealEncounterActor(
        CombatActorState actor,
        decimal amount,
        Guid sourceActorId,
        DateTimeOffset now,
        string definitionId)
    {
        if (amount <= 0 || actor.IsDead)
            return;

        decimal before = actor.CurrentHp;
        actor.ApplyHealing(amount);
        decimal effective = actor.CurrentHp - before;
        if (effective <= 0)
            return;

        Append(new CombatEvent(
            CombatEventType.HealingApplied,
            now,
            actor.ActorId,
            definitionId,
            effective,
            SourceActorId: sourceActorId,
            TargetActorId: actor.ActorId));
    }

    private decimal ChangeEncounterResource(
        CombatActorState actor,
        decimal amount,
        Guid sourceActorId,
        DateTimeOffset now,
        string definitionId)
    {
        decimal actual = actor.AddResource(amount);
        if (actual == 0)
            return 0;

        Append(new CombatEvent(
            CombatEventType.ResourceChanged,
            now,
            actor.ActorId,
            definitionId,
            actual,
            SourceActorId: sourceActorId,
            TargetActorId: actor.ActorId));
        return actual;
    }

    private void SetEncounterEnemyAiPaused(Guid actorId, bool paused, DateTimeOffset now)
    {
        if (!_enemiesById.TryGetValue(actorId, out CombatParticipantDefinition? enemy)
            || !_enemyAiRuntimes.TryGetValue(actorId, out EnemyAiRuntime? ai))
        {
            return;
        }

        if (paused)
        {
            ai.NextActionAtUtc = null;
            return;
        }

        if (!enemy.Actor.IsDead)
        {
            ai.State = MonsterAiState.InCombat;
            ai.NextActionAtUtc = now + enemy.AutoAttack.Interval;
        }
    }

    private void RetargetPlayersFromEncounterEnemy(
        Guid removedActorId,
        DateTimeOffset now,
        Guid? preferredNextTargetActorId = null)
    {
        CombatParticipantDefinition? nextAlive = preferredNextTargetActorId is { } preferredId
            && _enemiesById.TryGetValue(preferredId, out CombatParticipantDefinition? preferred)
            && !preferred.Actor.IsDead
                ? preferred
                : _enemies.FirstOrDefault(enemy =>
                    !enemy.Actor.IsDead && enemy.Actor.ActorId != removedActorId);
        if (nextAlive is null)
            return;

        foreach (CombatPlayerRuntimeState state in _playerStatesByActorId.Values
                     .Where(state => state.SelectedTargetActorId == removedActorId))
        {
            state.SelectedTargetActorId = nextAlive.Actor.ActorId;
            Append(new CombatEvent(
                CombatEventType.TargetChanged,
                now,
                state.Definition.Actor.ActorId,
                nextAlive.DefinitionId,
                SourceActorId: state.Definition.Actor.ActorId,
                TargetActorId: nextAlive.Actor.ActorId));
        }
    }
}
