using Elyndor.Core.Combat;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    /// <summary>
    /// Executes a player ability and, after a successful command, restarts the current
    /// auto-attack cycle from the ability resolution point. The player's auto-attack toggle
    /// stays enabled; stale main-hand/off-hand timers cannot fire during the cast.
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

        ActivatePlayer(participantCharacterId);
        if (_playerAutoAttackEnabled)
        {
            DateTimeOffset restartAtUtc = _playerRuntime.ActiveCast?.ResolvesAtUtc ?? now;
            _nextPlayerMainHandAutoAttackAtUtc =
                restartAtUtc + EffectivePlayerAutoAttackInterval(_player.AutoAttack, restartAtUtc);
            _nextPlayerOffHandAutoAttackAtUtc = _player.OffHandAutoAttack is null
                ? null
                : restartAtUtc + EffectivePlayerAutoAttackInterval(
                    _player.OffHandAutoAttack,
                    restartAtUtc);
        }

        return new CombatCommandResult(
            true,
            null,
            Snapshot(participantCharacterId),
            GetEventsAfter(before));
    }
}
