using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    public const string InterruptTargetCastParameter = "interruptTargetCast";
    public const string InterruptLockoutSecondsParameter = "interruptLockoutSeconds";

    private void ProcessEnemyInterruptUtilityEvent(CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.AbilityCompleted
            || combatEvent.SourceActorId is not { } sourceActorId
            || !_playerStatesByActorId.ContainsKey(sourceActorId)
            || combatEvent.TargetActorId is not { } targetActorId
            || !_enemyRuntimes.TryGetValue(targetActorId, out CombatRuntimeState? targetRuntime)
            || string.IsNullOrWhiteSpace(combatEvent.DefinitionId)
            || !_abilities.TryGetValue(combatEvent.DefinitionId, out AbilityDefinition? ability)
            || ability.RuntimeParameters is null
            || !ability.RuntimeParameters.TryGetValue(
                InterruptTargetCastParameter,
                out decimal interruptEnabled)
            || interruptEnabled <= 0)
        {
            return;
        }

        decimal lockoutSeconds = ability.RuntimeParameters.GetValueOrDefault(
            InterruptLockoutSecondsParameter);
        TimeSpan lockout = TimeSpan.FromSeconds(
            (double)Math.Max(0, lockoutSeconds));
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
            combatEvent.DefinitionId);
    }
}
