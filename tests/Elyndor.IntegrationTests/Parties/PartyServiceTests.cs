using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Parties;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class PartyServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 8, 14, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LeaderCanCreateDirectInviteAndTargetCanAccept()
    {
        (Guid leaderAccountId, Guid targetAccountId, Guid targetCharacterId) =
            await SeedPairAsync();
        Guid createRequestId = Guid.NewGuid();
        Guid inviteId = Guid.NewGuid();

        await using GameDbContext context = postgres.CreateDbContext();
        PartyService service = new(context, new FixedTimeProvider(Now));
        PartyOperationResult created = await service.CreateAsync(
            leaderAccountId,
            createRequestId,
            CancellationToken.None);
        Assert.True(created.IsSuccess);
        Assert.NotNull(created.Snapshot);

        PartyOperationResult invited = await service.InviteAsync(
            leaderAccountId,
            inviteId,
            targetCharacterId,
            Elyndor.Core.Parties.PartyInviteMode.Direct,
            CancellationToken.None);
        Assert.True(invited.IsSuccess);

        PartyOperationResult accepted = await service.AcceptInviteAsync(
            targetAccountId,
            inviteId,
            CancellationToken.None);
        Assert.True(accepted.IsSuccess);
        Assert.Equal(2, accepted.Snapshot!.Members.Count);
    }

    [Fact]
    public async Task CreateReplayDoesNotCreateSecondParty()
    {
        (Guid leaderAccountId, _, _) = await SeedPairAsync();
        Guid requestId = Guid.NewGuid();

        await using GameDbContext context = postgres.CreateDbContext();
        PartyService service = new(context, new FixedTimeProvider(Now));
        PartyOperationResult first = await service.CreateAsync(
            leaderAccountId,
            requestId,
            CancellationToken.None);
        PartyOperationResult replay = await service.CreateAsync(
            leaderAccountId,
            requestId,
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Snapshot!.PartyId, replay.Snapshot!.PartyId);
    }

    [Fact]
    public async Task ConcurrentPartyCreationForOneCharacterReturnsStableResult()
    {
        (Guid leaderAccountId, _, _) = await SeedPairAsync();
        Guid firstRequestId = Guid.NewGuid();
        Guid secondRequestId = Guid.NewGuid();

        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        PartyService firstService = new(firstContext, new FixedTimeProvider(Now));
        PartyService secondService = new(secondContext, new FixedTimeProvider(Now));

        Task<PartyOperationResult> firstTask = firstService.CreateAsync(
            leaderAccountId,
            firstRequestId,
            CancellationToken.None);
        Task<PartyOperationResult> secondTask = secondService.CreateAsync(
            leaderAccountId,
            secondRequestId,
            CancellationToken.None);

        PartyOperationResult[] results = await Task.WhenAll(firstTask, secondTask);

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.ErrorCode == PartyErrorCodes.AlreadyInParty);
    }

    [Fact]
    public async Task ConcurrentAcceptAndDeclineOfInviteProduceOneStableOutcome()
    {
        (Guid leaderAccountId, Guid targetAccountId, Guid targetCharacterId) =
            await SeedPairAsync();
        Guid inviteId = Guid.NewGuid();

        await using (GameDbContext setupContext = postgres.CreateDbContext())
        {
            PartyService setupService = new(setupContext, new FixedTimeProvider(Now));
            PartyOperationResult created = await setupService.CreateAsync(
                leaderAccountId,
                Guid.NewGuid(),
                CancellationToken.None);
            Assert.True(created.IsSuccess);
            PartyOperationResult invited = await setupService.InviteAsync(
                leaderAccountId,
                inviteId,
                targetCharacterId,
                Elyndor.Core.Parties.PartyInviteMode.Direct,
                CancellationToken.None);
            Assert.True(invited.IsSuccess);
        }

        await using GameDbContext acceptContext = postgres.CreateDbContext();
        await using GameDbContext declineContext = postgres.CreateDbContext();
        PartyService acceptService = new(acceptContext, new FixedTimeProvider(Now));
        PartyService declineService = new(declineContext, new FixedTimeProvider(Now));
        Task<PartyOperationResult> acceptTask = acceptService.AcceptInviteAsync(
            targetAccountId,
            inviteId,
            CancellationToken.None);
        Task<PartyOperationResult> declineTask = declineService.DeclineInviteAsync(
            targetAccountId,
            inviteId,
            CancellationToken.None);

        PartyOperationResult[] results = await Task.WhenAll(acceptTask, declineTask);

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.ErrorCode == PartyErrorCodes.InvalidState);
    }

    [Fact]
    public async Task ConcurrentAcceptsFromDifferentPartiesCannotAddOneCharacterTwice()
    {
        (Guid targetAccountId, Guid targetCharacterId, Guid firstLeaderAccountId, Guid secondLeaderAccountId) =
            await SeedCompetingInvitesAsync();

        await using (GameDbContext setupContext = postgres.CreateDbContext())
        {
            PartyService setupService = new(setupContext, new FixedTimeProvider(Now));
            PartyOperationResult firstParty = await setupService.CreateAsync(
                firstLeaderAccountId,
                Guid.NewGuid(),
                CancellationToken.None);
            PartyOperationResult secondParty = await setupService.CreateAsync(
                secondLeaderAccountId,
                Guid.NewGuid(),
                CancellationToken.None);
            Assert.True(firstParty.IsSuccess);
            Assert.True(secondParty.IsSuccess);
            Assert.True((await setupService.InviteAsync(
                firstLeaderAccountId,
                Guid.NewGuid(),
                targetCharacterId,
                PartyInviteMode.Direct,
                CancellationToken.None)).IsSuccess);
            Assert.True((await setupService.InviteAsync(
                secondLeaderAccountId,
                Guid.NewGuid(),
                targetCharacterId,
                PartyInviteMode.Direct,
                CancellationToken.None)).IsSuccess);
        }

        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        PartyInvite[] invites = await firstContext.PartyInvites
            .AsNoTracking()
            .Where(invite => invite.TargetCharacterId == targetCharacterId)
            .OrderBy(invite => invite.CreatedAtUtc)
            .ToArrayAsync();
        Assert.Equal(2, invites.Length);
        PartyService firstService = new(firstContext, new FixedTimeProvider(Now));
        PartyService secondService = new(secondContext, new FixedTimeProvider(Now));

        PartyOperationResult[] results = await Task.WhenAll(
            firstService.AcceptInviteAsync(
                targetAccountId,
                invites[0].Id,
                CancellationToken.None),
            secondService.AcceptInviteAsync(
                targetAccountId,
                invites[1].Id,
                CancellationToken.None));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.ErrorCode == PartyErrorCodes.AlreadyInParty);
        await using GameDbContext verifyContext = postgres.CreateDbContext();
        Assert.Equal(
            1,
            await verifyContext.PartyMembers.CountAsync(member => member.CharacterId == targetCharacterId));
    }

    private async Task<(Guid LeaderAccountId, Guid TargetAccountId, Guid TargetCharacterId)> SeedPairAsync()
    {
        Guid leaderAccountId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        Guid targetAccountId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        Guid leaderCharacterId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        Guid targetCharacterId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.AddRange(
            new Account(leaderAccountId, 3001, Now),
            new Account(targetAccountId, 3002, Now));
        context.Characters.AddRange(
            new Character(
                leaderCharacterId,
                leaderAccountId,
                Guid.NewGuid(),
                "Leader",
                "LEADER",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now),
            new Character(
                targetCharacterId,
                targetAccountId,
                Guid.NewGuid(),
                "Target",
                "TARGET",
                "HUMAN",
                "FEMALE",
                "MAGE",
                Now));
        await context.SaveChangesAsync();
        return (leaderAccountId, targetAccountId, targetCharacterId);
    }

    private async Task<(
        Guid TargetAccountId,
        Guid TargetCharacterId,
        Guid FirstLeaderAccountId,
        Guid SecondLeaderAccountId)> SeedCompetingInvitesAsync()
    {
        Guid targetAccountId = Guid.Parse("30000000-0000-0000-0000-000000000012");
        Guid firstLeaderAccountId = Guid.Parse("30000000-0000-0000-0000-000000000013");
        Guid secondLeaderAccountId = Guid.Parse("30000000-0000-0000-0000-000000000014");
        Guid targetCharacterId = Guid.Parse("44444444-4444-4444-4444-444444444412");

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.AddRange(
            new Account(targetAccountId, 3012, Now),
            new Account(firstLeaderAccountId, 3013, Now),
            new Account(secondLeaderAccountId, 3014, Now));
        context.Characters.AddRange(
            new Character(
                targetCharacterId,
                targetAccountId,
                Guid.NewGuid(),
                "TargetTwo",
                "TARGETTWO",
                "HUMAN",
                "FEMALE",
                "MAGE",
                Now),
            new Character(
                Guid.Parse("33333333-3333-3333-3333-333333333413"),
                firstLeaderAccountId,
                Guid.NewGuid(),
                "LeaderOne",
                "LEADERONE",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now),
            new Character(
                Guid.Parse("33333333-3333-3333-3333-333333333414"),
                secondLeaderAccountId,
                Guid.NewGuid(),
                "LeaderTwo",
                "LEADERTWO",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now));
        await context.SaveChangesAsync();
        return (targetAccountId, targetCharacterId, firstLeaderAccountId, secondLeaderAccountId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
