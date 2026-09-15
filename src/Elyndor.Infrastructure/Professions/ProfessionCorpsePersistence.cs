using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Professions;

public static class ProfessionCorpsePersistence
{
    public static async Task<int> DiscardPendingAsync(
        GameDbContext dbContext,
        Guid characterId,
        CancellationToken cancellationToken)
    {
        if (characterId == Guid.Empty)
            return 0;

        var corpses = await dbContext.SkinnableCorpses
            .Where(corpse => corpse.CharacterId == characterId
                && corpse.SkinnedAtUtc == null)
            .ToArrayAsync(cancellationToken);
        if (corpses.Length == 0)
            return 0;

        dbContext.SkinnableCorpses.RemoveRange(corpses);
        await dbContext.SaveChangesAsync(cancellationToken);
        return corpses.Length;
    }

    public static async Task<int> DiscardSupersededAsync(
        GameDbContext dbContext,
        Guid characterId,
        Guid currentCombatSessionId,
        DateTimeOffset currentServerTimeUtc,
        CancellationToken cancellationToken)
    {
        if (characterId == Guid.Empty || currentCombatSessionId == Guid.Empty)
            return 0;

        var corpses = await dbContext.SkinnableCorpses
            .Where(corpse => corpse.CharacterId == characterId
                && corpse.SkinnedAtUtc == null
                && corpse.CombatSessionId != currentCombatSessionId
                && corpse.CreatedAtUtc <= currentServerTimeUtc)
            .ToArrayAsync(cancellationToken);
        if (corpses.Length == 0)
            return 0;

        dbContext.SkinnableCorpses.RemoveRange(corpses);
        await dbContext.SaveChangesAsync(cancellationToken);
        return corpses.Length;
    }
}
