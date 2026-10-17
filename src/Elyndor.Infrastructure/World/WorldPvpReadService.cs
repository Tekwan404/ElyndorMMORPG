using Elyndor.Core.World;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public sealed record WorldPvpRank(Guid CharacterId, string Name, string RaceId, long SeasonHonor,
    int Victories, int Assists, int Deaths, int KillStreak = 0)
{
    public WorldPvpHonorRank Rank => WorldPvpBalance.Rank(SeasonHonor);
    public WorldPvpHonorRank? NextRank => WorldPvpBalance.Ranks.FirstOrDefault(r => r.RequiredHonor > SeasonHonor);
}
public sealed record WorldPvpHistoryEntry(Guid SessionId, string TargetName, string RaceId,
    DateTimeOffset AtUtc, long Honor, long GoldEarned, long BloodyTokens, int TargetDefeatNumber, bool? Victory,
    bool Defeat = false);
public sealed record WorldPvpOverview(int Season, DateTimeOffset EndsAtUtc, WorldPvpRank? Self,
    long FieldHonor, long BloodyTokens, int RewardedToday, int DailyLimit, IReadOnlyList<WorldPvpRank> Leaders,
    WorldPvpRewardProfile? Rewards = null, IReadOnlyList<WorldPvpHonorRank>? Ranks = null,
    IReadOnlyList<WorldPvpHistoryEntry>? History = null);

public sealed class WorldPvpReadService(GameDbContext db, TimeProvider time, IContentSnapshotProvider? content = null)
{
    public async Task<WorldPvpOverview?> GetAsync(Guid accountId, string? raceId, CancellationToken ct)
    {
        var character = await db.Characters.AsNoTracking().SingleOrDefaultAsync(c => c.AccountId == accountId, ct);
        if (character is null) return null;
        var now = time.GetUtcNow();
        int season = WorldPvpBalance.Season(now);
        var rankings = from s in db.WorldPvpStandings.AsNoTracking()
                       join c in db.Characters.AsNoTracking() on s.CharacterId equals c.Id
                       where s.Season == season
                       select new { CharacterId = c.Id, c.Name, c.RaceId, s.SeasonHonor, s.Victories, s.Assists, s.Deaths, s.KillStreak };
        var self = await rankings.Where(r => r.CharacterId == character.Id)
            .Select(r => new WorldPvpRank(r.CharacterId, r.Name, r.RaceId, r.SeasonHonor, r.Victories, r.Assists, r.Deaths, r.KillStreak)).SingleOrDefaultAsync(ct)
            ?? new(character.Id, character.Name, character.RaceId, 0, 0, 0, 0);
        var leaders = await rankings.Where(r => r.SeasonHonor > 0 && (raceId == null || r.RaceId == raceId))
            .OrderByDescending(r => r.SeasonHonor).ThenBy(r => r.CharacterId).Take(50)
            .Select(r => new WorldPvpRank(r.CharacterId, r.Name, r.RaceId, r.SeasonHonor, r.Victories, r.Assists, r.Deaths, r.KillStreak)).ToArrayAsync(ct);
        var wallet = await db.WorldPvpWallets.AsNoTracking().SingleOrDefaultAsync(w => w.CharacterId == character.Id, ct);
        DateTimeOffset day = new(now.UtcDateTime.Date, TimeSpan.Zero);
        int today = await db.WorldPvpRewardReceipts.CountAsync(r => r.CharacterId == character.Id && r.AtUtc >= day
            && r.AtUtc < day.AddDays(1) && (r.GoldEarned > 0 || r.BloodyTokens > 0), ct);
        var history = await (from r in db.WorldPvpRewardReceipts.AsNoTracking()
                            join target in db.Characters.AsNoTracking() on r.VictimId equals target.Id
                            where r.CharacterId == character.Id && r.Season == season
                            orderby r.AtUtc descending, r.SessionId descending
                            select new WorldPvpHistoryEntry(r.SessionId, target.Name, target.RaceId, r.AtUtc,
                                r.FieldHonor, r.GoldEarned, r.BloodyTokens, r.TargetDefeatNumber, r.Victory, false))
                            .Take(20).ToArrayAsync(ct);
        var seasonStart = WorldPvpBalance.SeasonEnds(season - 1);
        var seasonEnd = WorldPvpBalance.SeasonEnds(season);
        var deaths = await db.WorldPvpDeathReceipts.AsNoTracking().Where(r => r.VictimId == character.Id
                && r.AtUtc >= seasonStart && r.AtUtc < seasonEnd)
            .OrderByDescending(r => r.AtUtc).ThenByDescending(r => r.SessionId).Take(20)
            .Select(r => new WorldPvpHistoryEntry(r.SessionId, "Гибель в открытом мире", "", r.AtUtc,
                0, 0, 0, 0, false, true)).ToArrayAsync(ct);
        var recent = history.Concat(deaths).OrderByDescending(r => r.AtUtc).ThenByDescending(r => r.SessionId).Take(20).ToArray();
        return new(season, WorldPvpBalance.SeasonEnds(season), self, wallet?.FieldHonor ?? 0,
            wallet?.BloodyTokens ?? 0, today, WorldPvpBalance.DailyRewardedOpponents, leaders,
            content?.GetCurrent().Package.WorldPvpRewards ?? new(), WorldPvpBalance.Ranks, recent);
    }
}
