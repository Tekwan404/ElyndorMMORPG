using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Pvp;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

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
