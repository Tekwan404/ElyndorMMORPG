using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Participants;

namespace Elyndor.Infrastructure.Combat;

public interface ICombatResultObserver
{
    Task ObserveAsync(
        Guid combatSessionId,
        IReadOnlyList<CombatParticipantSnapshot> participants,
        IReadOnlyList<CombatEvent> events,
        CancellationToken cancellationToken);
}
