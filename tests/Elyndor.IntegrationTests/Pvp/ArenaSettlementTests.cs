using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Pvp;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elyndor.IntegrationTests.Pvp;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ArenaSettlementTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task VictorySettlesBothRatingsAndHonorExactlyOnceOnReplay()
    {
        var (matchId, first, second) = await CompletedMatch(ArenaMatchOutcome.WinnerA);
        await using (var db = postgres.CreateDbContext())
        {
            var service = new ArenaSettlementService(db, TimeProvider.System);
            Assert.True((await service.SettleAsync(matchId, default)).Applied);
            Assert.False((await service.SettleAsync(matchId, default)).Applied);
        }

        await using var verify = postgres.CreateDbContext();
        Assert.Equal(10, (await verify.ArenaHonorWallets.SingleAsync(x => x.CharacterId == first)).Balance);
        Assert.Equal(1, await verify.ArenaHonorLedgerEntries.CountAsync(x => x.MatchId == matchId));
        Assert.Equal(1012, (await verify.ArenaStandings.SingleAsync(x => x.CharacterId == first)).Rating);
        Assert.Equal(988, (await verify.ArenaStandings.SingleAsync(x => x.CharacterId == second)).Rating);
    }

    [Fact]
    public async Task ConcurrentSettlementCannotAwardTwoVictoryGrants()
    {
        var (matchId, first, _) = await CompletedMatch(ArenaMatchOutcome.WinnerA);
        async Task<bool> Settle()
        {
            await using var db = postgres.CreateDbContext();
            return (await new ArenaSettlementService(db, TimeProvider.System).SettleAsync(matchId, default)).Applied;
        }

        bool[] outcomes = await Task.WhenAll(Settle(), Settle());
        Assert.Single(outcomes, x => x);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(10, (await verify.ArenaHonorWallets.SingleAsync(x => x.CharacterId == first)).Balance);
        Assert.Equal(1, await verify.ArenaHonorLedgerEntries.CountAsync(x => x.MatchId == matchId));
    }

    [Theory]
    [InlineData(ArenaMatchOutcome.Draw)]
    [InlineData(ArenaMatchOutcome.Cancelled)]
    public async Task DrawOrCancellationNeverAwardsHonorOrRating(ArenaMatchOutcome outcome)
    {
        var (matchId, first, second) = await CompletedMatch(outcome);
        await using (var db = postgres.CreateDbContext())
            await new ArenaSettlementService(db, TimeProvider.System).SettleAsync(matchId, default);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(0, await verify.ArenaHonorLedgerEntries.CountAsync(x => x.MatchId == matchId));
        Assert.Equal(1000, (await verify.ArenaStandings.SingleAsync(x => x.CharacterId == first)).Rating);
        Assert.Equal(1000, (await verify.ArenaStandings.SingleAsync(x => x.CharacterId == second)).Rating);
    }

    [Fact]
    public async Task LeaderboardAndWalletReadTheCommittedMatchResult()
    {
        var (matchId, first, second) = await CompletedMatch(ArenaMatchOutcome.WinnerA);
        await using (var db = postgres.CreateDbContext())
            await new ArenaSettlementService(db, TimeProvider.System).SettleAsync(matchId, default);

        await using var readDb = postgres.CreateDbContext();
        var reader = new ArenaReadService(readDb);
        var leaderboard = await reader.LeaderboardAsync(default);
        Assert.Equal([first, second], leaderboard.Select(x => x.CharacterId));
        Assert.Equal(1012, leaderboard[0].Rating);
        Assert.Equal(10, (await reader.StatusAsync(first, default)).Honor);
        Assert.Equal(0, (await reader.StatusAsync(second, default)).Honor);
    }

    [Fact]
    public async Task IneligibleMatchDoesNotCountAsRankedVictoryOrGrantHonor()
    {
        var (matchId, first, second) = await CompletedMatch(ArenaMatchOutcome.WinnerA,
            eligibleForProgression: false);
        await using (var db = postgres.CreateDbContext())
            await new ArenaSettlementService(db, TimeProvider.System).SettleAsync(matchId, default);

        await using var verify = postgres.CreateDbContext();
        Assert.Equal(0, await verify.ArenaHonorLedgerEntries.CountAsync(x => x.MatchId == matchId));
        var winner = await verify.ArenaStandings.SingleAsync(x => x.CharacterId == first);
        var loser = await verify.ArenaStandings.SingleAsync(x => x.CharacterId == second);
        Assert.Equal(1000, winner.Rating);
        Assert.Equal(0, winner.Wins);
        Assert.Equal(0, loser.Losses);
    }

    [Fact]
    public async Task CompleteAndSettleFinishesAnActiveMatchOnceAndIsReplaySafe()
    {
        Guid matchId, first;
        await using (var db = postgres.CreateDbContext())
        {
            first = AddPlayer(db, "First");
            Guid second = AddPlayer(db, "Second");
            var match = new ArenaMatch(Guid.NewGuid(), first, second, Now);
            db.ArenaMatches.Add(match);
            await db.SaveChangesAsync();
            matchId = match.Id;
        }

        await using (var db = postgres.CreateDbContext())
        {
            var service = new ArenaSettlementService(db, new FixedTime(Now.AddMinutes(3)));
            Assert.True((await service.CompleteAndSettleAsync(matchId, ArenaMatchOutcome.WinnerA, null, default)).Applied);
            Assert.False((await service.CompleteAndSettleAsync(matchId, ArenaMatchOutcome.WinnerB, null, default)).Applied);
        }

        await using var verify = postgres.CreateDbContext();
        ArenaMatch stored = await verify.ArenaMatches.SingleAsync(x => x.Id == matchId);
        Assert.Equal(ArenaMatchOutcome.WinnerA, stored.Outcome);
        Assert.NotNull(stored.SettledAtUtc);
        Assert.Equal(10, (await verify.ArenaHonorWallets.SingleAsync(x => x.CharacterId == first)).Balance);
    }

    [Fact]
    public async Task CancelledCompletionAwardsNothingEvenForRankedMatches()
    {
        Guid matchId, first;
        await using (var db = postgres.CreateDbContext())
        {
            first = AddPlayer(db, "First");
            Guid second = AddPlayer(db, "Second");
            var match = new ArenaMatch(Guid.NewGuid(), first, second, Now);
            db.ArenaMatches.Add(match);
            await db.SaveChangesAsync();
            matchId = match.Id;
        }

        await using (var db = postgres.CreateDbContext())
            await new ArenaSettlementService(db, new FixedTime(Now.AddMinutes(1)))
                .CompleteAndSettleAsync(matchId, ArenaMatchOutcome.Cancelled, true, default);

        await using var verify = postgres.CreateDbContext();
        Assert.False((await verify.ArenaMatches.SingleAsync(x => x.Id == matchId)).EligibleForProgression);
        Assert.Empty(await verify.ArenaHonorWallets.ToListAsync());
        Assert.Empty(await verify.ArenaHonorLedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task HonorIsCappedPerOpponentButRatingStillMoves()
    {
        Guid first, second;
        await using (var db = postgres.CreateDbContext())
        {
            first = AddPlayer(db, "First");
            second = AddPlayer(db, "Second");
            await db.SaveChangesAsync();
        }

        var options = Options.Create(new ArenaOptions { MaxHonorWinsPerOpponentInWindow = 2 });
        for (int i = 0; i < 3; i++)
        {
            Guid matchId;
            await using (var db = postgres.CreateDbContext())
            {
                var match = new ArenaMatch(Guid.NewGuid(), first, second, Now.AddMinutes(i));
                match.Complete(ArenaMatchOutcome.WinnerA, Now.AddMinutes(i + 1));
                db.ArenaMatches.Add(match);
                await db.SaveChangesAsync();
                matchId = match.Id;
            }

            await using var settle = postgres.CreateDbContext();
            await new ArenaSettlementService(settle, TimeProvider.System, options).SettleAsync(matchId, default);
        }

        await using var verify = postgres.CreateDbContext();
        Assert.Equal(20, (await verify.ArenaHonorWallets.SingleAsync(x => x.CharacterId == first)).Balance);
        Assert.Equal(2, await verify.ArenaHonorLedgerEntries.CountAsync(x => x.CharacterId == first));
        ArenaStanding winner = await verify.ArenaStandings.SingleAsync(x => x.CharacterId == first);
        Assert.Equal(3, winner.Wins);
        Assert.True(winner.Rating > 1012 + 10);
    }

    [Fact]
    public async Task RecentRematchIsSkippedByMatchmakingUntilCooldownPasses()
    {
        Guid first, second;
        await using (var db = postgres.CreateDbContext())
        {
            first = AddPlayer(db, "First");
            second = AddPlayer(db, "Second");
            var previous = new ArenaMatch(Guid.NewGuid(), first, second, Now.AddMinutes(1));
            previous.Complete(ArenaMatchOutcome.WinnerA, Now.AddMinutes(2));
            db.ArenaMatches.Add(previous);
            db.ArenaQueueEntries.Add(new ArenaQueueEntry(first, ArenaQueueMode.Ranked, 1, 1000, Now.AddMinutes(2)));
            db.ArenaQueueEntries.Add(new ArenaQueueEntry(second, ArenaQueueMode.Ranked, 1, 1000, Now.AddMinutes(2)));
            await db.SaveChangesAsync();
        }

        var time = new FixedTime(Now.AddMinutes(3));
        await using (var db = postgres.CreateDbContext())
        {
            var blocked = new ArenaMatchmakingService(db, time,
                Options.Create(new ArenaOptions { RematchCooldown = TimeSpan.FromMinutes(3) }));
            Assert.Null(await blocked.TryCreateMatchAsync(ArenaQueueMode.Ranked, default));
        }

        await using (var db = postgres.CreateDbContext())
        {
            var allowed = new ArenaMatchmakingService(db, time,
                Options.Create(new ArenaOptions { RematchCooldown = TimeSpan.Zero }));
            ArenaMatchCreated? created = await allowed.TryCreateMatchAsync(ArenaQueueMode.Ranked, default);
            Assert.NotNull(created);
            Assert.Equal(ArenaProgressionRules.FormulaVersion,
                (await db.ArenaMatches.AsNoTracking().SingleAsync(x => x.Id == created!.MatchId)).FormulaVersion);
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task<(Guid MatchId, Guid First, Guid Second)> CompletedMatch(ArenaMatchOutcome outcome,
        bool eligibleForProgression = true)
    {
        await using var db = postgres.CreateDbContext();
        Guid first = AddPlayer(db, "First");
        Guid second = AddPlayer(db, "Second");
        var match = new ArenaMatch(Guid.NewGuid(), first, second, Now);
        match.Complete(outcome, Now.AddMinutes(2), eligibleForProgression);
        db.ArenaMatches.Add(match);
        await db.SaveChangesAsync();
        return (match.Id, first, second);
    }

    private static Guid AddPlayer(GameDbContext db, string name)
    {
        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        db.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        db.Characters.Add(new Character(characterId, accountId, Guid.NewGuid(), name,
            $"{name}{characterId:N}"[..16].ToUpperInvariant(), "HUMAN", "MALE", "WARRIOR", Now));
        return characterId;
    }
}
