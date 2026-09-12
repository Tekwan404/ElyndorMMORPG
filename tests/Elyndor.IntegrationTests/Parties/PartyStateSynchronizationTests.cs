using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Parties;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class PartyStateSynchronizationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 6, 15, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MemberLeavingBeforeLeaderLoadsProducesAuthoritativeSnapshotWithoutGhostMember()
    {
        Guid leaderAccountId = Guid.Parse("87000000-0000-0000-0000-000000000001");
        Guid memberAccountId = Guid.Parse("87000000-0000-0000-0000-000000000002");
        Guid leaderCharacterId = Guid.Parse("87111111-1111-1111-1111-111111111111");
        Guid memberCharacterId = Guid.Parse("87222222-2222-2222-2222-222222222222");
        Guid inviteId = Guid.Parse("87333333-3333-3333-3333-333333333333");

        await SeedPairAsync(
            leaderAccountId,
            memberAccountId,
            leaderCharacterId,
            memberCharacterId);

        await using (GameDbContext setupContext = postgres.CreateDbContext())
        {
            PartyService setup = new(setupContext, new FixedTimeProvider(Now));
            PartyOperationResult created = await setup.CreateAsync(
                leaderAccountId,
                Guid.NewGuid(),
                CancellationToken.None);
            Assert.True(created.IsSuccess, created.ErrorCode);
            Assert.True((await setup.InviteAsync(
                leaderAccountId,
                inviteId,
                memberCharacterId,
                PartyInviteMode.Direct,
                CancellationToken.None)).IsSuccess);
            PartyOperationResult accepted = await setup.AcceptInviteAsync(
                memberAccountId,
                inviteId,
                CancellationToken.None);
            Assert.True(accepted.IsSuccess, accepted.ErrorCode);
            Assert.Equal(2, accepted.Snapshot!.Members.Count);
        }

        // The member leaves before the leader has loaded a fresh party snapshot.
        await using (GameDbContext memberContext = postgres.CreateDbContext())
        {
            PartyService memberService = new(memberContext, new FixedTimeProvider(Now.AddSeconds(1)));
            PartyOperationResult left = await memberService.LeaveAsync(
                memberAccountId,
                CancellationToken.None);
            Assert.True(left.IsSuccess, left.ErrorCode);
            Assert.DoesNotContain(
                left.Snapshot!.Members,
                member => member.CharacterId == memberCharacterId);
        }

        await using (GameDbContext leaderContext = postgres.CreateDbContext())
        {
            PartyService leaderService = new(leaderContext, new FixedTimeProvider(Now.AddSeconds(2)));
            PartySnapshot? leaderSnapshot = await leaderService.GetAsync(
                leaderAccountId,
                CancellationToken.None);

            Assert.NotNull(leaderSnapshot);
            PartyMemberView onlyMember = Assert.Single(leaderSnapshot!.Members);
            Assert.Equal(leaderCharacterId, onlyMember.CharacterId);
            Assert.True(onlyMember.IsLeader);
            Assert.DoesNotContain(
                leaderSnapshot.Members,
                member => member.CharacterId == memberCharacterId);
        }

        await using (GameDbContext departedContext = postgres.CreateDbContext())
        {
            PartyService departedService = new(departedContext, new FixedTimeProvider(Now.AddSeconds(2)));
            Assert.Null(await departedService.GetAsync(
                memberAccountId,
                CancellationToken.None));
        }

        await using GameDbContext verificationContext = postgres.CreateDbContext();
        PartyMember persistedMember = await verificationContext.PartyMembers.SingleAsync();
        Assert.Equal(leaderCharacterId, persistedMember.CharacterId);
        Assert.False(await verificationContext.PartyMembers.AnyAsync(
            member => member.CharacterId == memberCharacterId));
    }

    private async Task SeedPairAsync(
        Guid leaderAccountId,
        Guid memberAccountId,
        Guid leaderCharacterId,
        Guid memberCharacterId)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.AddRange(
            new Account(leaderAccountId, 8701, Now),
            new Account(memberAccountId, 8702, Now));
        context.Characters.AddRange(
            new Character(
                leaderCharacterId,
                leaderAccountId,
                Guid.NewGuid(),
                "SyncLeader",
                "SYNCLEADER",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now),
            new Character(
                memberCharacterId,
                memberAccountId,
                Guid.NewGuid(),
                "SyncMember",
                "SYNCMEMBER",
                "HUMAN",
                "FEMALE",
                "MAGE",
                Now));
        await context.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
