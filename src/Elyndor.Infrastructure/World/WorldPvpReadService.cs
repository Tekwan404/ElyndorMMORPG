using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public sealed record WorldPvpRank(Guid CharacterId, string Name, string RaceId, int Rating,
    int Victories, int Assists, int Deaths);
public sealed record WorldPvpOverview(int Season, DateTimeOffset EndsAtUtc, WorldPvpRank? Self,
    long FieldHonor, long BloodyTokens, int RewardedToday, int DailyLimit, IReadOnlyList<WorldPvpRank> Leaders);

public sealed class WorldPvpReadService(GameDbContext db, TimeProvider time)
{
    public async Task<WorldPvpOverview?> GetAsync(Guid accountId, string? raceId, CancellationToken ct)
    {
        var character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(c => c.AccountId == accountId, ct);
        if (character is null) return null;
        int season = WorldPvpBalance.Season(time.GetUtcNow());
        var rankings = from s in db.WorldPvpStandings.AsNoTracking()
                       join c in db.Characters.AsNoTracking() on s.CharacterId equals c.Id
                       where s.Season == season
                       select new { CharacterId = c.Id, c.Name, c.RaceId, s.Rating, s.Victories, s.Assists, s.Deaths };
        var self = await rankings.Where(r => r.CharacterId == character.Id)
            .Select(r => new WorldPvpRank(r.CharacterId, r.Name, r.RaceId, r.Rating, r.Victories, r.Assists, r.Deaths)).SingleOrDefaultAsync(ct)
            ?? new(character.Id, character.Name, character.RaceId, 0, 0, 0, 0);
        var leaders = await rankings.Where(r => r.Rating > 0 && (raceId == null || r.RaceId == raceId))
            .OrderByDescending(r => r.Rating).ThenBy(r => r.CharacterId).Take(50)
            .Select(r => new WorldPvpRank(r.CharacterId, r.Name, r.RaceId, r.Rating, r.Victories, r.Assists, r.Deaths)).ToArrayAsync(ct);
        var wallet = await db.WorldPvpWallets.AsNoTracking().SingleOrDefaultAsync(w => w.CharacterId == character.Id, ct);
        DateTimeOffset day = new(time.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        int today = await db.WorldPvpRewardReceipts.CountAsync(r => r.CharacterId == character.Id && r.AtUtc >= day && r.AtUtc < day.AddDays(1), ct);
        return new(season, WorldPvpBalance.SeasonEnds(season), self, wallet?.FieldHonor ?? 0,
            wallet?.BloodyTokens ?? 0, today, WorldPvpBalance.DailyRewardedOpponents, leaders);
    }
}
