using Elyndor.Core.Combat;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    /// <summary>
    /// Executes a player ability without resetting the independent weapon swing clocks.
    /// Instant abilities and the global cooldown do not delay an already scheduled swing.
    /// Real casts are handled by the combat scheduler, which delays a due swing while
    /// <see cref="CombatRuntimeState.ActiveCast"/> is active.
    /// </summary>
    public CombatCommandResult HandleAbilityInterruptingAutoAttack(
        Guid participantCharacterId,
        UseAbilityCommand command,
        DateTimeOffset now) =>
        Handle(participantCharacterId, command, now);
}
