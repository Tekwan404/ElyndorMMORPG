using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Raids;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Raids;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class GroupMembershipConcurrencyTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 19, 4, 50, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConcurrentPartyAndRaidCreationAdmitsExactlyOneGroupContext()
    {
        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        await using (GameDbContext seed = postgres.CreateDbContext())
        {
            seed.Accounts.Add(new Account(accountId, 7999, Now));
            seed.Characters.Add(new Character(
                characterId,
                accountId,
                Guid.NewGuid(),
                "GroupRace",
                "GROUPRACE",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now));
            await seed.SaveChangesAsync();
        }

        await using GameDbContext partyContext = postgres.CreateDbContext();
        await using GameDbContext raidContext = postgres.CreateDbContext();
        PartyService partyService = new(partyContext, new FixedTimeProvider(Now));
        RaidService raidService = new(raidContext, new FixedTimeProvider(Now));

        Task<PartyOperationResult> partyTask = partyService.CreateAsync(
            accountId,
            Guid.NewGuid(),
            CancellationToken.None);
        Task<RaidOperationResult> raidTask = raidService.CreateAsync(
            accountId,
            Guid.NewGuid(),
            CancellationToken.None);
        await Task.WhenAll(partyTask, raidTask);
        PartyOperationResult partyResult = await partyTask;
        RaidOperationResult raidResult = await raidTask;

        Assert.NotEqual(partyResult.IsSuccess, raidResult.IsSuccess);
        await using GameDbContext verification = postgres.CreateDbContext();
        int memberships = await verification.PartyMembers.CountAsync(member => member.CharacterId == characterId)
            + await verification.RaidMembers.CountAsync(member => member.CharacterId == characterId);
        Assert.Equal(1, memberships);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
