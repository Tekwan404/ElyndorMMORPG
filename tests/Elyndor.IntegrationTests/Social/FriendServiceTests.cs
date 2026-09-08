using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Social;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Social;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class FriendServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SearchesByNameTelegramUsernameAndPublicCode()
    {
        (Guid requesterAccountId, _, Guid targetCharacterId) = await SeedPairAsync();

        await using GameDbContext context = postgres.CreateDbContext();
        FriendService service = new(context, new FixedTimeProvider(Now));

        PlayerSearchResult[] byName = (await service.SearchAsync(
            requesterAccountId,
            "Mage",
            CancellationToken.None)).ToArray();
        PlayerSearchResult[] byTelegram = (await service.SearchAsync(
            requesterAccountId,
            "@mage_one",
            CancellationToken.None)).ToArray();
        PlayerSearchResult[] byCode = (await service.SearchAsync(
            requesterAccountId,
            "ELY-2222222222",
            CancellationToken.None)).ToArray();

        Assert.Equal(targetCharacterId, Assert.Single(byName).CharacterId);
        Assert.Equal(targetCharacterId, Assert.Single(byTelegram).CharacterId);
        Assert.Equal(targetCharacterId, Assert.Single(byCode).CharacterId);
    }

    [Fact]
    public async Task FriendRequestAcceptCreatesOneMutualFriendshipAndReplayIsSafe()
    {
        (Guid requesterAccountId, Guid targetAccountId, Guid targetCharacterId) =
            await SeedPairAsync();
        Guid requestId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        await using (GameDbContext senderContext = postgres.CreateDbContext())
        {
            FriendService service = new(senderContext, new FixedTimeProvider(Now));
            FriendMutationResult first = await service.SendRequestAsync(
                requesterAccountId,
                targetCharacterId,
                requestId,
                CancellationToken.None);
            FriendMutationResult replay = await service.SendRequestAsync(
                requesterAccountId,
                targetCharacterId,
                requestId,
                CancellationToken.None);

            Assert.True(first.IsSuccess);
            Assert.True(replay.IsSuccess);
        }

        await using (GameDbContext targetContext = postgres.CreateDbContext())
        {
            FriendService service = new(targetContext, new FixedTimeProvider(Now.AddMinutes(1)));
            FriendMutationResult accepted = await service.AcceptRequestAsync(
                targetAccountId,
                requestId,
                CancellationToken.None);
            Assert.True(accepted.IsSuccess);
        }

        await using GameDbContext verificationContext = postgres.CreateDbContext();
        Assert.Equal(1, await verificationContext.Friendships.CountAsync());
        FriendSnapshot snapshot = (await new FriendService(
            verificationContext,
            new FixedTimeProvider(Now)).GetSnapshotAsync(
                requesterAccountId,
                CancellationToken.None))!;
        Assert.Equal(targetCharacterId, Assert.Single(snapshot.Friends).CharacterId);
    }

    [Fact]
    public async Task OppositeFriendRequestsForOnePairAreSerializedWithoutConstraintFailure()
    {
        (Guid requesterAccountId, Guid targetAccountId, Guid targetCharacterId) =
            await SeedPairAsync();
        Guid requesterCharacterId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        FriendService first = new(firstContext, new FixedTimeProvider(Now));
        FriendService second = new(secondContext, new FixedTimeProvider(Now));

        FriendMutationResult[] results = await Task.WhenAll(
            first.SendRequestAsync(
                requesterAccountId,
                targetCharacterId,
                Guid.NewGuid(),
                CancellationToken.None),
            second.SendRequestAsync(
                targetAccountId,
                requesterCharacterId,
                Guid.NewGuid(),
                CancellationToken.None));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.Equal(1, results.Count(result => result.ErrorCode == FriendErrorCodes.RequestPending));
    }

    private async Task<(Guid RequesterAccountId, Guid TargetAccountId, Guid TargetCharacterId)> SeedPairAsync()
    {
        Guid requesterAccountId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid targetAccountId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        Guid requesterCharacterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid targetCharacterId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await using GameDbContext context = postgres.CreateDbContext();
        Account requesterAccount = new(requesterAccountId, 1001, Now);
        Account targetAccount = new(targetAccountId, 1002, Now);
        targetAccount.SetTelegramUsername("@Mage_One");
        context.Accounts.AddRange(requesterAccount, targetAccount);
        context.Characters.AddRange(
            new Character(
                requesterCharacterId,
                requesterAccountId,
                Guid.NewGuid(),
                "Warrior",
                "WARRIOR",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now),
            new Character(
                targetCharacterId,
                targetAccountId,
                Guid.NewGuid(),
                "Mage One",
                "MAGE ONE",
                "HUMAN",
                "MALE",
                "MAGE",
                Now));
        await context.SaveChangesAsync();
        return (requesterAccountId, targetAccountId, targetCharacterId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
