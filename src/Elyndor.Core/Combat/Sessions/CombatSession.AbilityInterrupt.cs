using Elyndor.Core.Combat;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    /// <summary>
    /// Executes a player ability without restarting the auto-attack cycle. Instant abilities
    /// leave scheduled swings untouched. Casted abilities are already handled by the combat
    /// scheduler, which holds a ready swing until the active cast resolves.
    /// </summary>
    public CombatCommandResult HandleAbilityInterruptingAutoAttack(
        Guid participantCharacterId,
        UseAbilityCommand command,
        DateTimeOffset now)
    {
        long before = Sequence;
        CombatCommandResult result = Handle(participantCharacterId, command, now);
        if (!result.Succeeded)
            return result;

        return new CombatCommandResult(
            true,
            null,
            Snapshot(participantCharacterId),
            GetEventsAfter(before));
    }
}
