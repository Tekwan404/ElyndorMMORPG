using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Raids;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Raids;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class RaidServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 15, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateReplayReturnsOriginalRaidWithoutSecondRow()
    {
        Guid accountId = await SeedCharacterAsync();
        Guid requestId = Guid.NewGuid();

        await using GameDbContext context = postgres.CreateDbContext();
        RaidService service = new(context, new FixedTimeProvider(Now));

        RaidOperationResult first = await service.CreateAsync(accountId, requestId, CancellationToken.None);
        RaidOperationResult replay = await service.CreateAsync(accountId, requestId, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Snapshot!.RaidId, replay.Snapshot!.RaidId);
        Assert.Equal(1, await context.RaidGroups.CountAsync());
        Assert.Single(replay.Snapshot.Members);
    }

    [Fact]
    public async Task LeaderCanInviteAndTargetCanAccept()
    {
        (Guid leaderAccountId, Guid targetAccountId, Guid targetCharacterId) =
            await SeedPairAsync();

        await using GameDbContext context = postgres.CreateDbContext();
        RaidService service = new(context, new FixedTimeProvider(Now));
        Assert.True((await service.CreateAsync(
            leaderAccountId,
            Guid.NewGuid(),
            CancellationToken.None)).IsSuccess);

        RaidOperationResult invited = await service.InviteAsync(
            leaderAccountId,
            Guid.NewGuid(),
            targetCharacterId,
            CancellationToken.None);
        RaidOperationResult accepted = await service.AcceptInviteAsync(
            targetAccountId,
            invited.Invite!.Id,
            CancellationToken.None);

        Assert.True(invited.IsSuccess);
        Assert.True(accepted.IsSuccess);
        Assert.Equal(2, accepted.Snapshot!.Members.Count);
    }

    private async Task<Guid> SeedCharacterAsync()
    {
        Guid accountId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        Guid characterId = Guid.Parse("55555555-5555-5555-5555-555555555551");

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, 5001, Now));
        context.Characters.Add(new Character(
            characterId,
            accountId,
            Guid.NewGuid(),
            "RaidLeader",
            "RAIDLEADER",
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        await context.SaveChangesAsync();
        return accountId;
    }

    private async Task<(Guid LeaderAccountId, Guid TargetAccountId, Guid TargetCharacterId)> SeedPairAsync()
    {
        Guid leaderAccountId = Guid.Parse("50000000-0000-0000-0000-000000000010");
        Guid targetAccountId = Guid.Parse("50000000-0000-0000-0000-000000000011");
        Guid leaderCharacterId = Guid.Parse("55555555-5555-5555-5555-555555555510");
        Guid targetCharacterId = Guid.Parse("55555555-5555-5555-5555-555555555511");

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.AddRange(
            new Account(leaderAccountId, 5010, Now),
            new Account(targetAccountId, 5011, Now));
        context.Characters.AddRange(
            new Character(
                leaderCharacterId,
                leaderAccountId,
                Guid.NewGuid(),
                "RaidLeaderTwo",
                "RAIDLEADERTWO",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now),
            new Character(
                targetCharacterId,
                targetAccountId,
                Guid.NewGuid(),
                "RaidTarget",
                "RAIDTARGET",
                "HUMAN",
                "FEMALE",
                "MAGE",
                Now));
        await context.SaveChangesAsync();
        return (leaderAccountId, targetAccountId, targetCharacterId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
