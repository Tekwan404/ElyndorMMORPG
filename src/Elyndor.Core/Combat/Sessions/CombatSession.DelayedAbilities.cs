using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private DateTimeOffset? NextDelayedAbilityActionAtUtc
    {
        get
        {
            DateTimeOffset? next = null;
            foreach (CombatPlayerRuntimeState state in _playerStatesByActorId.Values)
                next = Min(next, state.Runtime.NextPendingActionAtUtc);
            next = Min(next, _companionRuntime?.NextPendingActionAtUtc);
            foreach (CombatRuntimeState runtime in _enemyRuntimes.Values)
                next = Min(next, runtime.NextPendingActionAtUtc);
            return next;
        }
    }

    private void ProcessDelayedAbilityActions(DateTimeOffset now)
    {
        Guid activePlayerActorId = _activePlayerState.Definition.Actor.ActorId;
        try
        {
            foreach (CombatPlayerRuntimeState state in _playerStatesByActorId.Values
                         .OrderBy(item => item.Definition.Actor.ActorId))
            {
                ResolveDelayedAbilityActions(
                    state.Runtime,
                    state.Definition.Actor.ActorId,
                    now);
                if (Status != CombatSessionStatus.Active)
                    return;
            }

            if (_companionRuntime is not null && _companion is not null)
            {
                ResolveDelayedAbilityActions(
                    _companionRuntime,
                    _companion.Actor.ActorId,
                    now);
                if (Status != CombatSessionStatus.Active)
                    return;
            }

            foreach (CombatParticipantDefinition enemy in _enemies)
            {
                ResolveDelayedAbilityActions(
                    _enemyRuntimes[enemy.Actor.ActorId],
                    enemy.Actor.ActorId,
                    now);
                if (Status != CombatSessionStatus.Active)
                    return;
            }
        }
        finally
        {
            if (_playerStatesByActorId.ContainsKey(activePlayerActorId))
                ActivatePlayer(activePlayerActorId);
        }
    }

    private void ResolveDelayedAbilityActions(
        CombatRuntimeState runtime,
        Guid sourceActorId,
        DateTimeOffset now)
    {
        IReadOnlyList<CombatEvent> events = AbilityEngine.ResolvePendingActions(
            runtime,
            now,
            _random);
        if (events.Count == 0)
            return;

        ApplyKernelEvents(
            events,
            sourceActorId,
            sourceActorId,
            definitionId: null);
    }
}
