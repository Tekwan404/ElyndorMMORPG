using Elyndor.Core.Characters;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.World;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.World;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class DungeonTravelLockTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 11, 21, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveDungeonMembershipBlocksWorldTravelEvenAfterBossCompletion(bool completed)
    {
        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        Guid partyId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, 3901, Now));
            setup.Characters.Add(new Character(
                characterId,
                accountId,
                Guid.NewGuid(),
                "DungeonTraveler",
                "DUNGEONTRAVELER",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now));
            setup.CharacterLocations.Add(new CharacterLocation(
                characterId,
                "STARTER_TOWN",
                1,
                Now));

            Party party = Party.Create(
                partyId,
                Guid.NewGuid(),
                characterId,
                Now);
            setup.Parties.Add(party);
            setup.PartyMembers.AddRange(party.Members);

            DungeonRun run = DungeonRun.Create(
                runId,
                Guid.NewGuid(),
                partyId,
                "ECLIPSED_CITADEL",
                Now);
            run.AddMember(characterId, Now);
            if (completed)
                run.Complete(Now.AddMinutes(5));
            setup.DungeonRuns.Add(run);
            setup.DungeonRunMembers.AddRange(run.Members);
            await setup.SaveChangesAsync();
        }

        await using GameDbContext context = postgres.CreateDbContext();
        TravelService service = new(
            context,
            new WorldMap([
                new("STARTER_TOWN", "Starter Town", "SAFE", 1, ["WHISPERING_FOREST"]),
                new("WHISPERING_FOREST", "Whispering Forest", "ADVENTURE", 1, ["STARTER_TOWN"])
            ]),
            new FixedTimeProvider(Now.AddMinutes(10)));

        TravelResult result = await service.TravelAsync(
            accountId,
            Guid.NewGuid(),
            "WHISPERING_FOREST",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(TravelErrorCodes.DungeonRunActive, result.ErrorCode);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
