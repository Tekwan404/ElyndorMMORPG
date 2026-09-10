using Elyndor.Core.Combat;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    /// <summary>
    /// Executes a player ability and, only after a successful command, cancels any running
    /// auto-attack cycle. Abilities never implicitly restart auto attack; the player must
    /// issue StartAutoAttack again.
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
            _playerAutoAttackEnabled = false;
            _nextPlayerMainHandAutoAttackAtUtc = null;
            _nextPlayerOffHandAutoAttackAtUtc = null;
            Append(new CombatEvent(
                CombatEventType.AutoAttackStopped,
                now,
                _player.Actor.ActorId,
                SourceActorId: _player.Actor.ActorId,
                TargetActorId: _enemy.Actor.ActorId));
        }

        return new CombatCommandResult(
            true,
            null,
            Snapshot(participantCharacterId),
            GetEventsAfter(before));
    }
}
