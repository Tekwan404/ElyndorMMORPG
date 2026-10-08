using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Combat.Abilities;

public static partial class AbilityEngine
{
    private static AbilityExecutionResult AdvanceChannel(CombatRuntimeState runtime, ActiveCast cast,
        DateTimeOffset now, IGameRandom? random)
    {
        if (now < cast.NextResolutionAtUtc)
            return AbilityExecutionResult.Failure(AbilityErrorCode.CastNotReady);
        EnsureExecutable(cast.Ability, random);
        List<CombatEvent> events = [];
        while (cast.NextResolutionAtUtc <= now && cast.NextResolutionAtUtc <= cast.ResolvesAtUtc)
        {
            DateTimeOffset tick = cast.NextResolutionAtUtc;
            Guid[] livingTargets = (cast.TargetIds ?? [cast.TargetId])
                .Where(id => runtime.Actors.TryGetValue(id, out var target) && !target.IsDead && target.IsTargetable(tick))
                .ToArray();
            if (runtime.Actor.IsDead || livingTargets.Length == 0
                || runtime.Actor.ActiveEffects.Any(effect => effect.AppliedAtUtc < tick && effect.ExpiresAtUtc > tick
                    && (effect.Definition.Kind is EffectKind.Stun or EffectKind.Fear
                        || cast.Ability.IsSpell && effect.Definition.Kind == EffectKind.Silence)))
            {
                runtime.ActiveCast = null;
                runtime.Version++;
                events.Add(new(CombatEventType.AbilityInterrupted, tick, runtime.Actor.ActorId, cast.Ability.Id));
                return new(true, AbilityErrorCode.None, events);
            }
            events.AddRange(ResolveActions(runtime, cast.Ability, livingTargets, cast.TargetModifiers, tick, random));
            cast = cast with { CompletedChannelTicks = cast.CompletedChannelTicks + 1 };
            if (tick == cast.ResolvesAtUtc)
            {
                runtime.ActiveCast = null;
                runtime.Version++;
                events.Add(new(CombatEventType.AbilityCompleted, tick, runtime.Actor.ActorId, cast.Ability.Id));
                return new(true, AbilityErrorCode.None, events);
            }
            runtime.ActiveCast = cast;
        }
        runtime.Version++;
        return new(true, AbilityErrorCode.None, events);
    }

    internal static bool IsValidChannel(AbilityDefinition ability) => ability.Type == AbilityType.Channelled
        ? ability.CastTime > TimeSpan.Zero && ability.ChannelTickInterval is { } interval
            && interval > TimeSpan.Zero && interval <= ability.CastTime
            && ability.CastTime.Ticks % interval.Ticks == 0
            && ability.Actions?.Any(action => action.Delay is { } delay && delay > TimeSpan.Zero) != true
        : ability.ChannelTickInterval is null;
}
