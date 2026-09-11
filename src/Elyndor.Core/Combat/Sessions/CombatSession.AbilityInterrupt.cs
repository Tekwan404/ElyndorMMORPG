using Elyndor.Core.Combat;

namespace Elyndor.Core.Combat.Sessions;

public sealed partial class CombatSession
{
    /// <summary>
    /// Executes a player ability without resetting the auto-attack cycle. Instant abilities
    /// leave already scheduled swings untouched. A real cast only delays a swing that would
    /// otherwise become ready before the cast resolves.
    /// </summary>
    public CombatCommandResult HandleAbilityInterruptingAutoAttack(
        Guid participantCharacterId,
        UseAbilityCommand command,
        DateTimeOffset now)
    {
        ActivatePlayer(participantCharacterId);
        DateTimeOffset? mainHandScheduledAtUtc = _nextPlayerMainHandAutoAttackAtUtc;
        DateTimeOffset? offHandScheduledAtUtc = _nextPlayerOffHandAutoAttackAtUtc;

        long before = Sequence;
        CombatCommandResult result = Handle(participantCharacterId, command, now);
        if (!result.Succeeded)
            return result;

        ActivatePlayer(participantCharacterId);
        if (_playerAutoAttackEnabled)
        {
            DateTimeOffset? castResolvesAtUtc = _playerRuntime.ActiveCast?.ResolvesAtUtc;
            _nextPlayerMainHandAutoAttackAtUtc = ResumeSwingAfterAbility(
                mainHandScheduledAtUtc,
                castResolvesAtUtc);
            _nextPlayerOffHandAutoAttackAtUtc = _player.OffHandAutoAttack is null
                ? null
                : ResumeSwingAfterAbility(
                    offHandScheduledAtUtc,
                    castResolvesAtUtc);
        }

        return new CombatCommandResult(
            true,
            null,
            Snapshot(participantCharacterId),
            GetEventsAfter(before));
    }

    private static DateTimeOffset? ResumeSwingAfterAbility(
        DateTimeOffset? scheduledAtUtc,
        DateTimeOffset? castResolvesAtUtc)
    {
        if (scheduledAtUtc is null || castResolvesAtUtc is null)
            return scheduledAtUtc;

        return scheduledAtUtc < castResolvesAtUtc
            ? castResolvesAtUtc
            : scheduledAtUtc;
    }
}
