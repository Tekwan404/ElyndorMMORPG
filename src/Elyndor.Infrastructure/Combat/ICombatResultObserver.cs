using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Sessions;

namespace Elyndor.Infrastructure.Combat;

public interface ICombatResultObserver
{
    Task ObserveAsync(
        CombatSession session,
        IReadOnlyList<CombatParticipantSnapshot> participants,
        IReadOnlyList<CombatEvent> events,
        CancellationToken cancellationToken);
}
