using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Identity;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Dungeons;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Dungeons;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class DungeonReconnectRecoveryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 6, 45, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FreshServiceAfterReconnectReturnsSameActiveDungeonRunId()
    {
        Guid accountId = Guid.Parse("88000000-0000-0000-0000-000000000001");
        Guid characterId = Guid.Parse("88111111-1111-1111-1111-111111111111");

        await using (GameDbContext seedContext = postgres.CreateDbContext())
        {
            seedContext.Accounts.Add(new Account(accountId, 8801, Now));
            Character character = new(
                characterId,
                accountId,
                Guid.NewGuid(),
                "ReconnectHero",
                "RECONNECTHERO",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now);
            character.SetLevel(15);
            seedContext.Characters.Add(character);
            seedContext.CharacterVitals.Add(
                new CharacterVitals(characterId, 200, 0, Now, Now));
            seedContext.CharacterLocations.Add(
                new CharacterLocation(characterId, "ANCIENT_MINE", 1, Now));
            await seedContext.SaveChangesAsync();
        }

        FixedTimeProvider time = new(Now);
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        StaticContentSnapshotProvider contentProvider = new(content);

        Guid runId;
        await using (GameDbContext firstRequestContext = postgres.CreateDbContext())
        {
            PartyService parties = new(firstRequestContext, time);
            Assert.True((await parties.CreateAsync(
                accountId,
                Guid.NewGuid(),
                CancellationToken.None)).IsSuccess);

            DungeonService dungeons = new(
                firstRequestContext,
                parties,
                contentProvider,
                time);
            DungeonOperationResult created = await dungeons.CreateAsync(
                accountId,
                "ANCIENT_MINE",
                Guid.NewGuid(),
                CancellationToken.None);

            Assert.True(created.Succeeded, created.ErrorCode);
            Assert.NotNull(created.Run);
            runId = created.Run!.RunId;
            Assert.Equal(DungeonRunState.Active, created.Run.State);
        }

        // A completely new DbContext/service graph simulates a reconnect/new API request.
        await using GameDbContext reconnectContext = postgres.CreateDbContext();
        PartyService reconnectParties = new(reconnectContext, new FixedTimeProvider(Now.AddSeconds(5)));
        DungeonService reconnectDungeons = new(
            reconnectContext,
            reconnectParties,
            contentProvider,
            new FixedTimeProvider(Now.AddSeconds(5)));

        DungeonRunView? recovered = await reconnectDungeons.GetCurrentAsync(
            accountId,
            CancellationToken.None);

        Assert.NotNull(recovered);
        Assert.Equal(runId, recovered!.RunId);
        Assert.Equal(DungeonRunState.Active, recovered.State);
        Assert.Equal("ANCIENT_MINE", recovered.DungeonId);
        Assert.Contains(recovered.Members, member => member.CharacterId == characterId && member.IsActive);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
