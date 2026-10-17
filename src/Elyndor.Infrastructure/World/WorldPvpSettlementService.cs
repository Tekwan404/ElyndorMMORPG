using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public sealed class WorldPvpSettlementService(GameDbContext db, TimeProvider time)
{
    public async Task ApplyAsync(CombatSessionSnapshot snapshot, CancellationToken ct)
    {
        if (!snapshot.OpenWorldPvp || snapshot.Status == CombatSessionStatus.Active
            || snapshot.WorldPvpDefeats is not { Count: > 0 } defeats) return;
        var settled = await db.WorldPvpDeathReceipts.AsNoTracking().Where(r => r.SessionId == snapshot.SessionId)
            .Select(r => r.VictimId).ToArrayAsync(ct);
        // The immutable terminal journal supplies every captured defeat, including departed players.
        foreach (var defeat in defeats.Where(d => !settled.Contains(d.VictimId)).OrderBy(d => d.AtUtc).ThenBy(d => d.VictimId))
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                Guid[] ids = defeat.Contributions.Select(c => c.CharacterId).Append(defeat.VictimId).Distinct().ToArray();
                var characters = await db.Characters.FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR UPDATE").ToDictionaryAsync(c => c.Id, ct);
                if (!characters.TryGetValue(defeat.VictimId, out var victim)
                    || await db.WorldPvpDeathReceipts.AnyAsync(r => r.SessionId == snapshot.SessionId && r.VictimId == defeat.VictimId, ct))
                { await tx.CommitAsync(ct); return; }
                var at = defeat.AtUtc;
                if (at > time.GetUtcNow()) throw new InvalidDataException("World PvP defeat is in the future.");
                int season = WorldPvpBalance.Season(at);
                var victimStanding = await StandingAsync(victim.Id, season, ct);
                int victimRating = victimStanding.Rating;
                victimStanding.RecordDeath();
                db.WorldPvpDeathReceipts.Add(new(snapshot.SessionId, victim.Id, at));
                foreach (var credit in WorldPvpBalance.Credits(defeat))
                {
                    if (!characters.TryGetValue(credit.CharacterId, out var player) || player.AccountId == victim.AccountId
                        || player.RaceId == victim.RaceId || player.Level < 60 || victim.Level < 60) continue;
                    var cutoff = at - WorldPvpBalance.PairCooldown;
                    bool repeated = await db.WorldPvpRewardReceipts.AnyAsync(r => r.AtUtc >= cutoff
                        && ((r.CharacterId == player.Id && r.VictimId == victim.Id)
                            || (r.CharacterId == victim.Id && r.VictimId == player.Id)), ct);
                    DateTimeOffset day = new(at.UtcDateTime.Date, TimeSpan.Zero);
                    int today = await db.WorldPvpRewardReceipts.CountAsync(r => r.CharacterId == player.Id
                        && r.AtUtc >= day && r.AtUtc < day.AddDays(1), ct);
                    if (repeated || today >= WorldPvpBalance.DailyRewardedOpponents) continue;
                    var standing = await StandingAsync(player.Id, season, ct);
                    int rating = WorldPvpBalance.RatingAward(standing.Rating, victimRating);
                    long tokens = victimRating >= standing.Rating && credit.Victory ? 1 : 0;
                    standing.Credit(credit.Victory, rating);
                    var wallet = await db.WorldPvpWallets.SingleOrDefaultAsync(w => w.CharacterId == player.Id, ct);
                    if (wallet is null) { wallet = new(player.Id); db.WorldPvpWallets.Add(wallet); }
                    wallet.Grant(WorldPvpBalance.FieldHonorPerCredit, tokens);
                    db.WorldPvpRewardReceipts.Add(new(snapshot.SessionId, victim.Id, player.Id, at, season,
                        rating, WorldPvpBalance.FieldHonorPerCredit, tokens));
                }
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
    }

    private async Task<WorldPvpStanding> StandingAsync(Guid id, int season, CancellationToken ct)
    {
        var standing = db.WorldPvpStandings.Local.SingleOrDefault(s => s.CharacterId == id && s.Season == season)
            ?? await db.WorldPvpStandings.SingleOrDefaultAsync(s => s.CharacterId == id && s.Season == season, ct);
        if (standing is not null) return standing;
        standing = new(id, season);
        db.WorldPvpStandings.Add(standing);
        return standing;
    }
}
