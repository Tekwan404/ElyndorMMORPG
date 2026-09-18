using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private IReadOnlyList<CombatEvent> ResolveExpiredEffectActions(
        ActiveEffect effect,
        CombatActorState effectTarget,
        DateTimeOffset now)
    {
        IReadOnlyList<EffectExpirationActionDefinition>? actions =
            effect.Definition.OnExpireActions;
        if (actions is not { Count: > 0 } || effectTarget.IsDead)
            return [];

        CombatActorState? source = ResolveCombatActor(effect.SourceId);
        if (source is null || source.IsDead)
            return [];

        List<CombatEvent> events = [];
        foreach (EffectExpirationActionDefinition action in actions)
        {
            CombatActorState[] targets = ResolveEffectExpirationTargets(
                effectTarget,
                action.TargetScope);
            foreach (CombatActorState target in targets)
            {
                if (target.IsDead)
                    continue;

                switch (action.Type)
                {
                    case EffectExpirationActionType.Damage:
                        DamageResult damage = DamagePipeline.Resolve(
                            new DamageRequest(
                                source,
                                target,
                                action.Amount,
                                action.DamageType,
                                CanMiss: false,
                                CanDodge: false,
                                CanCrit: false,
                                MinimumDamage: 0),
                            _random,
                            now);
                        events.AddRange(damage.Events.Select(item => item with
                        {
                            DefinitionId = item.DefinitionId ?? effect.Definition.Id,
                            SourceActorId = item.SourceActorId ?? source.ActorId,
                            TargetActorId = item.TargetActorId ?? target.ActorId
                        }));
                        break;
                    case EffectExpirationActionType.ApplyEffect:
                        if (action.Effect is null)
                        {
                            throw new InvalidOperationException(
                                "Expiration ApplyEffect action requires an effect definition.");
                        }
                        events.AddRange(EffectEngine.Apply(
                            target,
                            source.ActorId,
                            action.Effect,
                            now));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(action),
                            action.Type,
                            "Unknown effect expiration action type.");
                }
            }
        }

        return events;
    }

    private CombatActorState[] ResolveEffectExpirationTargets(
        CombatActorState effectTarget,
        EffectExpirationTargetScope scope)
    {
        if (scope == EffectExpirationTargetScope.EffectTarget)
            return effectTarget.IsDead ? [] : [effectTarget];

        IEnumerable<CombatActorState> allies = _enemiesById.ContainsKey(effectTarget.ActorId)
            ? _enemies
                .Where(enemy => !enemy.Actor.IsDead)
                .Select(enemy => enemy.Actor)
            : ActiveEnemyTargetCandidates()
                .Where(participant => !participant.Actor.IsDead)
                .Select(participant => participant.Actor);
        CombatActorState[] otherAllies = allies
            .Where(actor => actor.ActorId != effectTarget.ActorId)
            .ToArray();

        return scope switch
        {
            EffectExpirationTargetScope.EffectTargetAllies => otherAllies,
            EffectExpirationTargetScope.EffectTargetAndAllies =>
                [effectTarget, .. otherAllies],
            _ => throw new ArgumentOutOfRangeException(
                nameof(scope),
                scope,
                "Unknown effect expiration target scope.")
        };
    }
}
