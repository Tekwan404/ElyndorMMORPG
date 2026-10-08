using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private void InterruptChannelOnControl(CombatEvent input)
    {
        if (input.Type != CombatEventType.EffectApplied) return;
        CombatRuntimeState? runtime = _playerStatesByActorId.TryGetValue(input.ActorId, out var player)
            ? player.Runtime
            : _enemyRuntimes.GetValueOrDefault(input.ActorId);
        if (runtime is null && _companionRuntime?.Actor.ActorId == input.ActorId)
            runtime = _companionRuntime;
        if (runtime?.ActiveCast is not { Ability.Type: AbilityType.Channelled } cast) return;
        if (!runtime.Actor.ActiveEffects.Any(effect => effect.Definition.Id == input.DefinitionId
                && effect.AppliedAtUtc <= input.OccurredAtUtc && effect.ExpiresAtUtc > input.OccurredAtUtc
                && (effect.Definition.Kind is EffectKind.Stun or EffectKind.Fear
                    || cast.Ability.IsSpell && effect.Definition.Kind == EffectKind.Silence))) return;
        ApplyKernelEvents(AbilityEngine.Interrupt(runtime, input.OccurredAtUtc, TimeSpan.Zero).Events,
            input.SourceActorId ?? input.ActorId, input.ActorId, cast.Ability.Id);
    }
}
