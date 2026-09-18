using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    private void ProcessMonsterInterruptActionEvent(CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.AbilityCompleted
            || combatEvent.SourceActorId is not { } sourceActorId
            || !_enemyRuntimes.ContainsKey(sourceActorId)
            || combatEvent.TargetActorId is not { } targetActorId
            || ResolvePartyRuntime(targetActorId) is not { } targetRuntime
            || string.IsNullOrWhiteSpace(combatEvent.DefinitionId)
            || !_abilities.TryGetValue(combatEvent.DefinitionId, out AbilityDefinition? ability))
        {
            return;
        }

        AbilityActionDefinition? interruptAction = ability.Actions?
            .FirstOrDefault(action => action.Type == AbilityActionType.Interrupt);
        if (interruptAction is null)
            return;
        if (interruptAction.InterruptLockout is not { } lockout
            || lockout < TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"Interrupt ability '{ability.Id}' has an invalid lockout duration.");
        }

        AbilityExecutionResult interrupted = AbilityEngine.Interrupt(
            targetRuntime,
            combatEvent.OccurredAtUtc,
            lockout);
        if (!interrupted.Succeeded)
            return;

        ApplyKernelEvents(
            interrupted.Events,
            sourceActorId,
            targetActorId,
            ability.Id);
    }

    private CombatRuntimeState? ResolvePartyRuntime(Guid actorId)
    {
        if (_playerStatesByActorId.TryGetValue(
                actorId,
                out CombatPlayerRuntimeState? playerState))
        {
            return playerState.Runtime;
        }

        return _companion is not null
            && _companion.Actor.ActorId == actorId
            ? _companionRuntime
            : null;
    }
}
