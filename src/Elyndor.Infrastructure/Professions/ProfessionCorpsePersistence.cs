using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Professions;

public static class ProfessionCorpsePersistence
{
    public static Task<int> DiscardPendingAsync(
        GameDbContext dbContext,
        Guid characterId,
        CancellationToken cancellationToken)
    {
        if (characterId == Guid.Empty)
            return Task.FromResult(0);

        return dbContext.SkinnableCorpses
            .Where(corpse => corpse.CharacterId == characterId
                && corpse.SkinnedAtUtc == null)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public static Task<int> DiscardSupersededAsync(
        GameDbContext dbContext,
        Guid characterId,
        Guid currentCombatSessionId,
        DateTimeOffset currentServerTimeUtc,
        CancellationToken cancellationToken)
    {
        if (characterId == Guid.Empty || currentCombatSessionId == Guid.Empty)
            return Task.FromResult(0);

        return dbContext.SkinnableCorpses
            .Where(corpse => corpse.CharacterId == characterId
                && corpse.SkinnedAtUtc == null
                && corpse.CombatSessionId != currentCombatSessionId
                && corpse.CreatedAtUtc <= currentServerTimeUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
