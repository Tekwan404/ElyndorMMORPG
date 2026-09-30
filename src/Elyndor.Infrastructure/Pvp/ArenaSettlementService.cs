using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elyndor.Infrastructure.Pvp;

public sealed record ArenaSettlementResult(bool Applied);

public sealed class ArenaSettlementService(GameDbContext db, TimeProvider time,
    IOptions<ArenaOptions>? options = null)
{
    private readonly ArenaOptions _options = options?.Value ?? new ArenaOptions();

    public Task<ArenaSettlementResult> SettleAsync(Guid matchId, CancellationToken cancellationToken) =>
        RunAsync(matchId, null, null, cancellationToken);

    /// <summary>
    /// Records the terminal outcome and settles rating/Honor in one transaction. Safe to retry:
    /// an already completed or settled match is left untouched.
    /// </summary>
    public Task<ArenaSettlementResult> CompleteAndSettleAsync(Guid matchId, ArenaMatchOutcome outcome,
        bool? eligibleForProgression, CancellationToken cancellationToken) =>
        RunAsync(matchId, outcome, eligibleForProgression, cancellationToken);

    private Task<ArenaSettlementResult> RunAsync(Guid matchId, ArenaMatchOutcome? completeWith,
        bool? eligible, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var match = await db.ArenaMatches.FromSqlInterpolated(
                    $"SELECT * FROM game.arena_matches WHERE \"Id\" = {matchId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Arena match was not found.");
            if (match.Outcome == ArenaMatchOutcome.Active)
            {
                if (completeWith is null)
                    throw new InvalidOperationException("Active arena matches cannot be settled.");
                match.Complete(completeWith.Value, time.GetUtcNow(),
                    completeWith == ArenaMatchOutcome.Cancelled ? false : eligible);
            }
            if (match.SettledAtUtc is not null) return new ArenaSettlementResult(false);

            Guid[] characterIds = [match.CharacterAId, match.CharacterBId];
            int lockedCharacters = (await db.Characters.FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"Id\" = ANY({characterIds}) ORDER BY \"Id\" FOR UPDATE")
                .ToArrayAsync(cancellationToken)).Length;
            if (lockedCharacters != 2) throw new InvalidOperationException("Arena participant is missing.");

            var standings = await db.ArenaStandings
                .Where(x => characterIds.Contains(x.CharacterId) && x.SeasonId == match.SeasonId)
                .ToDictionaryAsync(x => x.CharacterId, cancellationToken);
            var first = standings.GetValueOrDefault(match.CharacterAId)
                ?? new ArenaStanding(match.CharacterAId, match.SeasonId);
            var second = standings.GetValueOrDefault(match.CharacterBId)
                ?? new ArenaStanding(match.CharacterBId, match.SeasonId);
            if (!standings.ContainsKey(first.CharacterId)) db.ArenaStandings.Add(first);
            if (!standings.ContainsKey(second.CharacterId)) db.ArenaStandings.Add(second);

            ArenaProgression progression = ArenaProgressionRules.Calculate(
                first.Rating, second.Rating, match.Outcome, match.EligibleForProgression,
                _options.RatingChangeFactor, _options.VictoryHonor);
            if (match.EligibleForProgression)
            {
                first.Record(match.Outcome, progression.RatingA, isFirst: true);
                second.Record(match.Outcome, progression.RatingB, isFirst: false);
            }

            Guid? winner = match.Outcome switch
            {
                ArenaMatchOutcome.WinnerA => match.CharacterAId,
                ArenaMatchOutcome.WinnerB => match.CharacterBId,
                _ => null
            };
            long honor = match.Outcome == ArenaMatchOutcome.WinnerA
                ? progression.HonorA : progression.HonorB;
            if (winner is not null && honor > 0
                && await IsHonorFarmingAsync(match, winner.Value, cancellationToken))
                honor = 0;
            if (winner is not null && honor > 0)
            {
                var wallet = await db.ArenaHonorWallets.SingleOrDefaultAsync(
                    x => x.CharacterId == winner.Value, cancellationToken)
                    ?? new ArenaHonorWallet(winner.Value);
                if (db.Entry(wallet).State == EntityState.Detached) db.ArenaHonorWallets.Add(wallet);
                wallet.Grant(honor);
                db.ArenaHonorLedgerEntries.Add(new ArenaHonorLedgerEntry(match.Id, winner.Value,
                    honor, wallet.Balance, time.GetUtcNow()));
            }

            match.MarkSettled(time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(CancellationToken.None);
            return new ArenaSettlementResult(true);
        });

    /// <summary>Honor is capped per winner/opponent pair inside a sliding window; rating still applies.</summary>
    private async Task<bool> IsHonorFarmingAsync(ArenaMatch match, Guid winner, CancellationToken cancellationToken)
    {
        Guid loser = winner == match.CharacterAId ? match.CharacterBId : match.CharacterAId;
        DateTimeOffset since = time.GetUtcNow() - _options.HonorFarmWindow;
        int priorWins = await db.ArenaMatches.AsNoTracking().CountAsync(x =>
            x.Id != match.Id && x.SeasonId == match.SeasonId && x.EligibleForProgression
            && x.SettledAtUtc != null && x.SettledAtUtc >= since
            && (x.CharacterAId == winner && x.CharacterBId == loser && x.Outcome == ArenaMatchOutcome.WinnerA
                || x.CharacterAId == loser && x.CharacterBId == winner && x.Outcome == ArenaMatchOutcome.WinnerB),
            cancellationToken);
        return priorWins >= _options.MaxHonorWinsPerOpponentInWindow;
    }
}
