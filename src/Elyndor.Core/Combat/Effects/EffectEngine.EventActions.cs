using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Combat.Effects;

public static partial class EffectEngine
{
    public static IReadOnlyList<CombatEvent> ResolveEventActions(
        CombatRuntimeState runtime, CombatEvent input, IGameRandom random, ProcGuard guard,
        Func<AbilityDefinition, CombatActorState, AbilityTargetModifier>? resolveTargetModifier = null)
    {
        if (!ProcGuard.IsEligible(input) || input.SourceActorId != runtime.Actor.ActorId
            || input.TargetActorId is not { } targetId || runtime.Actor.IsDead)
            return [];
        List<CombatEvent> events = [];
        foreach (ActiveEffect effect in runtime.Actor.ActiveEffects
                     .Where(e => e.ExpiresAtUtc > input.OccurredAtUtc && e.Definition.EventActions is { Count: > 0 }).ToArray())
        {
            int index = 0;
            foreach (EffectEventActionDefinition rule in effect.Definition.EventActions!)
            {
                string key = $"effect:{effect.Definition.Id}:{index++}";
                if (rule.Trigger != input.Type || rule.AbilityId is not null && rule.AbilityId != input.DefinitionId
                    || !guard.TryObserve(runtime.Actor.ActorId, key, input.ProcDispatchToken, input.Sequence)
                    || !guard.IsReady(runtime.Actor.ActorId, key, input.OccurredAtUtc)
                    || rule.ChancePercent < 100 && random.NextUnit() >= rule.ChancePercent / 100m)
                    continue;
                decimal amount = rule.UseBaseDamage ? input.BaseDamage : input.Amount;
                var action = rule.Action with { Amount = rule.Action.Amount + amount * rule.EventAmountPercent / 100m };
                var ability = new AbilityDefinition(effect.Definition.Id, AbilityType.Instant, AbilityTargetType.SingleEnemy,
                    0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, false, "PHYSICAL", Actions: [action]);
                AbilityTargetModifier? modifier = runtime.Actors.TryGetValue(targetId, out CombatActorState? target)
                    ? resolveTargetModifier?.Invoke(ability, target) : null;
                var generated = AbilityEngine.ResolveTriggeredAction(runtime, ability, action, targetId, input.OccurredAtUtc, random, modifier);
                if (generated.Count == 0) continue;
                guard.StartCooldown(runtime.Actor.ActorId, key, input.OccurredAtUtc, rule.InternalCooldown);
                events.AddRange(generated.Select(e => e with
                {
                    DefinitionId = effect.Definition.Id,
                    SourceActorId = e.SourceActorId ?? runtime.Actor.ActorId,
                    TargetActorId = e.TargetActorId ?? targetId,
                    IsProc = true, ProcDepth = input.ProcDepth + 1, ProcOriginId = effect.Definition.Id
                }));
            }
        }
        return events;
    }
}
