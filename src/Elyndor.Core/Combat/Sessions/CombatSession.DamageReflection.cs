using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private void ProcessGenericDamageReflection(CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.DamageDealt
            || combatEvent.Amount <= 0
            || combatEvent.IsPeriodic
            || combatEvent.IsReflected
            || combatEvent.SourceActorId is not { } attackerActorId
            || combatEvent.TargetActorId is not { } defenderActorId
            || attackerActorId == defenderActorId
            || !AreOpposingCombatActors(attackerActorId, defenderActorId))
        {
            return;
        }

        CombatActorState? attacker = ResolveCombatActor(attackerActorId);
        CombatActorState? defender = ResolveCombatActor(defenderActorId);
        if (attacker is null || defender is null || attacker.IsDead)
            return;

        ActiveEffect[] reflections = defender.ActiveEffects
            .Where(effect =>
                effect.ExpiresAtUtc > combatEvent.OccurredAtUtc
                && effect.Definition.Kind == EffectKind.DamageReflection)
            .OrderByDescending(effect => effect.Definition.ApplicationPriority)
            .ThenBy(effect => effect.Sequence)
            .ToArray();

        foreach (ActiveEffect reflection in reflections)
        {
            if (attacker.IsDead || Status != CombatSessionStatus.Active)
                return;

            decimal reflectedAmount = combatEvent.Amount
                * Math.Max(0, reflection.Definition.Magnitude)
                * Math.Max(1, reflection.Stacks);
            if (reflection.Definition.ReflectedDamageCap is { } cap)
                reflectedAmount = Math.Min(reflectedAmount, cap);
            if (reflectedAmount <= 0)
                continue;

            DamageResult reflected = DamagePipeline.Resolve(
                new DamageRequest(
                    defender,
                    attacker,
                    reflectedAmount,
                    DamageType.True,
                    CanMiss: false,
                    CanDodge: false,
                    CanCrit: false,
                    MinimumDamage: 0,
                    SkipDefenseMitigation: true,
                    CanBlock: false),
                _random,
                combatEvent.OccurredAtUtc);
            CombatEvent[] reflectedEvents = reflected.Events
                .Select(item => item with { IsReflected = true })
                .ToArray();
            ApplyKernelEvents(
                reflectedEvents,
                defender.ActorId,
                attacker.ActorId,
                reflection.Definition.Id);
        }
    }

    private CombatActorState? ResolveCombatActor(Guid actorId)
    {
        if (_playerStatesByActorId.TryGetValue(
                actorId,
                out CombatPlayerRuntimeState? playerState))
        {
            return playerState.Definition.Actor;
        }
        if (_companion is not null && _companion.Actor.ActorId == actorId)
            return _companion.Actor;
        return _enemiesById.TryGetValue(
            actorId,
            out CombatParticipantDefinition? enemy)
            ? enemy.Actor
            : null;
    }

    private bool AreOpposingCombatActors(Guid firstActorId, Guid secondActorId)
    {
        bool firstIsEnemy = _enemiesById.ContainsKey(firstActorId);
        bool secondIsEnemy = _enemiesById.ContainsKey(secondActorId);
        bool firstIsParty = _playerStatesByActorId.ContainsKey(firstActorId)
            || _companion is not null && _companion.Actor.ActorId == firstActorId;
        bool secondIsParty = _playerStatesByActorId.ContainsKey(secondActorId)
            || _companion is not null && _companion.Actor.ActorId == secondActorId;
        return firstIsEnemy && secondIsParty || secondIsEnemy && firstIsParty;
    }
}
