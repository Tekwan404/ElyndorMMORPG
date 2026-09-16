using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    public const string InterruptTargetCastParameter = "interruptTargetCast";
    public const string InterruptLockoutSecondsParameter = "interruptLockoutSeconds";
    private const decimal BuiltInControlInterruptLockoutSeconds = 2m;

    private void ProcessEnemyInterruptUtilityEvent(CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.AbilityCompleted
            || combatEvent.SourceActorId is not { } sourceActorId
            || !_playerStatesByActorId.ContainsKey(sourceActorId)
            || combatEvent.TargetActorId is not { } targetActorId
            || !_enemyRuntimes.TryGetValue(targetActorId, out CombatRuntimeState? targetRuntime)
            || string.IsNullOrWhiteSpace(combatEvent.DefinitionId)
            || !_abilities.TryGetValue(combatEvent.DefinitionId, out AbilityDefinition? ability))
        {
            return;
        }

        bool configuredInterrupt = ability.RuntimeParameters?.TryGetValue(
                InterruptTargetCastParameter,
                out decimal interruptEnabled) == true
            && interruptEnabled > 0;
        bool builtInControlInterrupt = combatEvent.DefinitionId is
            "CONCUSSION_BLOW" or "HAMMER_OF_JUSTICE";
        if (!configuredInterrupt && !builtInControlInterrupt)
            return;

        decimal lockoutSeconds = configuredInterrupt
            ? ability.RuntimeParameters!.GetValueOrDefault(InterruptLockoutSecondsParameter)
            : BuiltInControlInterruptLockoutSeconds;
        AbilityExecutionResult interrupted = AbilityEngine.Interrupt(
            targetRuntime,
            combatEvent.OccurredAtUtc,
            TimeSpan.FromSeconds((double)Math.Max(0, lockoutSeconds)));
        if (!interrupted.Succeeded)
            return;

        ApplyKernelEvents(
            interrupted.Events,
            sourceActorId,
            targetActorId,
            combatEvent.DefinitionId);
    }
}
