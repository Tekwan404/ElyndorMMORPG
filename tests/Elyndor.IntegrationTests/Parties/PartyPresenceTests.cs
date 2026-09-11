using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Parties;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class PartyPresenceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 11, 20, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OpenWorldCombatRosterOnlyContainsMembersInTheSameLocation()
    {
        Guid leaderAccountId = Guid.Parse("31000000-0000-0000-0000-000000000001");
        Guid remoteAccountId = Guid.Parse("31000000-0000-0000-0000-000000000002");
        Guid leaderCharacterId = Guid.Parse("31111111-1111-1111-1111-111111111111");
        Guid remoteCharacterId = Guid.Parse("32222222-2222-2222-2222-222222222222");

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.AddRange(
            new Account(leaderAccountId, 3101, Now),
            new Account(remoteAccountId, 3102, Now));
        context.Characters.AddRange(
            new Character(
                leaderCharacterId,
                leaderAccountId,
                Guid.NewGuid(),
                "LeaderPresence",
                "LEADERPRESENCE",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now),
            new Character(
                remoteCharacterId,
                remoteAccountId,
                Guid.NewGuid(),
                "RemotePresence",
                "REMOTEPRESENCE",
                "HUMAN",
                "FEMALE",
                "MAGE",
                Now));
        context.CharacterLocations.AddRange(
            new CharacterLocation(leaderCharacterId, "STARTER_TOWN", 1, Now),
            new CharacterLocation(remoteCharacterId, "DEEP_FOREST", 1, Now));
        await context.SaveChangesAsync();

        PartyService service = new(context, new FixedTimeProvider(Now));
        PartyOperationResult created = await service.CreateAsync(
            leaderAccountId,
            Guid.NewGuid(),
            CancellationToken.None);
        Assert.True(created.IsSuccess);

        Guid inviteId = Guid.NewGuid();
        Assert.True((await service.InviteAsync(
            leaderAccountId,
            inviteId,
            remoteCharacterId,
            PartyInviteMode.Direct,
            CancellationToken.None)).IsSuccess);
        Assert.True((await service.AcceptInviteAsync(
            remoteAccountId,
            inviteId,
            CancellationToken.None)).IsSuccess);

        IReadOnlyList<PartyCombatMember> separated = await service.GetCombatMembersAsync(
            leaderAccountId,
            CancellationToken.None);

        PartyCombatMember onlyLeader = Assert.Single(separated);
        Assert.Equal(leaderCharacterId, onlyLeader.CharacterId);
        Assert.True(onlyLeader.IsLeader);

        CharacterLocation remoteLocation = await context.CharacterLocations
            .SingleAsync(location => location.CharacterId == remoteCharacterId);
        remoteLocation.Relocate("STARTER_TOWN", Now.AddSeconds(1));
        await context.SaveChangesAsync();

        IReadOnlyList<PartyCombatMember> together = await service.GetCombatMembersAsync(
            leaderAccountId,
            CancellationToken.None);
        Assert.Equal(2, together.Count);

        PartySnapshot? snapshot = await service.GetAsync(leaderAccountId, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal("STARTER_TOWN", snapshot.Members.Single(member => member.CharacterId == leaderCharacterId).LocationId);
        Assert.Equal("STARTER_TOWN", snapshot.Members.Single(member => member.CharacterId == remoteCharacterId).LocationId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
