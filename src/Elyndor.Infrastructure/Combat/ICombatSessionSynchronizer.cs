using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Combat;

public interface ICombatSessionSynchronizer
{
    Task<bool> SynchronizeAsync(
        CombatSession session,
        GameContentSnapshot? contentSnapshot,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
