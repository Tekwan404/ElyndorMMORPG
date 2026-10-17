using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.World;

public sealed class WorldPvpSettlementService(GameDbContext db, TimeProvider time,
    ILogger<WorldPvpSettlementService>? logger = null)
{
    private static readonly Action<ILogger, Guid, Guid, DateTimeOffset, DateTimeOffset, Exception?> FutureDefeatTime =
        LoggerMessage.Define<Guid, Guid, DateTimeOffset, DateTimeOffset>(LogLevel.Warning,
            new EventId(4201, "WORLD_PVP_FUTURE_DEFEAT_TIME"),
            "World PvP future defeat timestamp clamped for session {SessionId}, victim {VictimId}; event {EventAt}, server {Now}.");
    public async Task ApplyAsync(CombatSessionSnapshot snapshot, CancellationToken ct, WorldPvpRewardProfile? rewards = null)
    {
        rewards ??= new();
        if (!rewards.IsValid()) throw new ArgumentOutOfRangeException(nameof(rewards));
        if (!snapshot.OpenWorldPvp || snapshot.Status == CombatSessionStatus.Active
            || snapshot.WorldPvpDefeats is not { Count: > 0 } defeats) return;
        var settled = await db.WorldPvpDeathReceipts.AsNoTracking().Where(r => r.SessionId == snapshot.SessionId)
            .Select(r => r.VictimId).ToArrayAsync(ct);
        // The immutable terminal journal supplies every captured defeat, including departed players.
        foreach (var defeat in defeats.Where(d => !settled.Contains(d.VictimId)).OrderBy(d => d.AtUtc))
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                Guid[] ids = defeat.Contributions.Select(c => c.CharacterId).Append(defeat.VictimId)
                    .Append(defeat.KillerId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray();
                var characters = await db.Characters.FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"Id\" = ANY({ids}) ORDER BY \"Id\" FOR UPDATE").ToDictionaryAsync(c => c.Id, ct);
                if (!characters.TryGetValue(defeat.VictimId, out var victim)
                    || await db.WorldPvpDeathReceipts.AnyAsync(r => r.SessionId == snapshot.SessionId && r.VictimId == defeat.VictimId, ct))
                { await tx.CommitAsync(ct); return; }
                DateTimeOffset at = defeat.AtUtc;
                DateTimeOffset now = time.GetUtcNow();
                if (at > now)
                {
                    if (logger is not null)
                        FutureDefeatTime(logger, snapshot.SessionId, defeat.VictimId, at, now, null);
                    at = now;
                }
                int season = WorldPvpBalance.Season(at);
                var victimStanding = await StandingAsync(victim.Id, season, ct);
                victimStanding.RecordDeath();
                db.WorldPvpDeathReceipts.Add(new(snapshot.SessionId, victim.Id, at));
                bool killRecorded = false;
                if (defeat.KillerId is { } killerId && characters.TryGetValue(killerId, out var killer)
                    && killer.Id != victim.Id && killer.AccountId != victim.AccountId
                    && killer.RaceId != victim.RaceId && killer.Level >= OpenWorldPvpRules.MinimumLevel
                    && victim.Level >= OpenWorldPvpRules.MinimumLevel)
                {
                    var killerStanding = await StandingAsync(killer.Id, season, ct);
                    killerStanding.RecordKill();
                    killRecorded = true;
                }
                var credits = WorldPvpBalance.Credits(defeat);
                int creditedCount = credits.Count;
                foreach (var credit in credits)
                {
                    if (!characters.TryGetValue(credit.CharacterId, out var player) || player.AccountId == victim.AccountId
                        || player.RaceId == victim.RaceId || player.Level < OpenWorldPvpRules.MinimumLevel
                        || victim.Level < OpenWorldPvpRules.MinimumLevel) continue;
                    DateTimeOffset day = new(at.UtcDateTime.Date, TimeSpan.Zero);
                    int previous = await db.WorldPvpRewardReceipts.CountAsync(r => r.CharacterId == player.Id
                        && r.VictimId == victim.Id && r.AtUtc >= day && r.AtUtc < day.AddDays(1), ct);
                    int today = await db.WorldPvpRewardReceipts.CountAsync(r => r.CharacterId == player.Id
                        && r.AtUtc >= day && r.AtUtc < day.AddDays(1)
                        && (r.GoldEarned > 0 || r.BloodyTokens > 0), ct);
                    var standing = await StandingAsync(player.Id, season, ct);
                    long honor = WorldPvpBalance.RepeatHonor(WorldPvpBalance.GroupAward(
                        WorldPvpBalance.FieldHonorPerCredit, creditedCount, credit.Victory), previous);
                    bool economicReward = previous == 0 && today < WorldPvpBalance.DailyRewardedOpponents;
                    long tokens = economicReward && credit.Victory ? 1 : 0;
                    standing.Credit(credit.Victory, honor, countVictory: !killRecorded);
                    var wallet = await db.WorldPvpWallets.SingleOrDefaultAsync(w => w.CharacterId == player.Id, ct);
                    if (wallet is null) { wallet = new(player.Id); db.WorldPvpWallets.Add(wallet); }
                    wallet.Grant(honor, tokens);
                    long gold = economicReward ? (credit.Victory ? rewards.GoldPerKill : rewards.GoldPerAssist) : 0;
                    player.AddGold(gold);
                    db.WorldPvpRewardReceipts.Add(new(snapshot.SessionId, victim.Id, player.Id, at, season,
                        0, honor, tokens, gold, checked(previous + 1), credit.Victory));
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
