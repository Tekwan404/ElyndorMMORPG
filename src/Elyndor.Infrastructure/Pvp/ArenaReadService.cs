using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Pvp;

public sealed record ArenaLeaderboardEntry(Guid CharacterId, string Name, int Rating, int Wins, int Losses, int Draws);

public sealed record ArenaStatus(long Honor, int Rating, int Wins, int Losses, int Draws);

public sealed class ArenaReadService(GameDbContext db)
{
    public Task<List<ArenaLeaderboardEntry>> LeaderboardAsync(CancellationToken cancellationToken) =>
        (from standing in db.ArenaStandings.AsNoTracking()
         join character in db.Characters.AsNoTracking() on standing.CharacterId equals character.Id
         where standing.SeasonId == ArenaSeason.CurrentId
         orderby standing.Rating descending, standing.Wins descending, standing.CharacterId
         select new ArenaLeaderboardEntry(standing.CharacterId, character.Name, standing.Rating,
             standing.Wins, standing.Losses, standing.Draws))
        .Take(50).ToListAsync(cancellationToken);

    public async Task<Guid?> CharacterIdForAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        await db.Characters.AsNoTracking().Where(x => x.AccountId == accountId)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);

    public async Task<ArenaStatus> StatusAsync(Guid characterId, CancellationToken cancellationToken)
    {
        if (!await db.Characters.AsNoTracking().AnyAsync(x => x.Id == characterId, cancellationToken))
            throw new InvalidOperationException("Arena character was not found.");

        var standing = await db.ArenaStandings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CharacterId == characterId && x.SeasonId == ArenaSeason.CurrentId,
                cancellationToken);
        long honor = await db.ArenaHonorWallets.AsNoTracking()
            .Where(x => x.CharacterId == characterId).Select(x => (long?)x.Balance)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;
        return new ArenaStatus(honor, standing?.Rating ?? ArenaProgressionRules.InitialRating,
            standing?.Wins ?? 0, standing?.Losses ?? 0, standing?.Draws ?? 0);
    }
}
