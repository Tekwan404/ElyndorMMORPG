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
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Dungeons;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class DungeonTemporaryExitReconnectTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 13, 17, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReconnectFromTownAfterTemporaryExitRecoversSameActiveRun()
    {
        Guid accountId = Guid.Parse("84000000-0000-0000-0000-000000000001");
        Guid characterId = Guid.Parse("84111111-1111-1111-1111-111111111111");
        Guid runId;

        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        StaticContentSnapshotProvider contentProvider = new(content);

        await using (GameDbContext firstContext = postgres.CreateDbContext())
        {
            firstContext.Accounts.Add(new Account(accountId, 8401, Now));
            Character character = new(
                characterId,
                accountId,
                Guid.NewGuid(),
                "TownReconnect",
                "TOWNRECONNECT",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now);
            character.SetLevel(15);
            firstContext.Characters.Add(character);
            firstContext.CharacterVitals.Add(new CharacterVitals(characterId, 200, 0, Now, Now));
            firstContext.CharacterLocations.Add(new CharacterLocation(characterId, "ANCIENT_MINE", 1, Now));
            await firstContext.SaveChangesAsync();

            FixedTimeProvider time = new(Now);
            PartyService parties = new(firstContext, time);
            Assert.True((await parties.CreateAsync(
                accountId,
                Guid.NewGuid(),
                CancellationToken.None)).IsSuccess);

            DungeonService dungeons = new(firstContext, parties, contentProvider, time);
            DungeonOperationResult created = await dungeons.CreateAsync(
                accountId,
                "ANCIENT_MINE",
                Guid.NewGuid(),
                CancellationToken.None);
            Assert.True(created.Succeeded, created.ErrorCode);
            runId = created.Run!.RunId;

            DungeonNavigationService navigation = new(
                firstContext,
                new FixedTimeProvider(Now.AddMinutes(1)));
            DungeonNavigationResult exit = await navigation.ExitToCityAsync(
                accountId,
                runId,
                CancellationToken.None);
            Assert.True(exit.Succeeded, exit.ErrorCode);
            Assert.Equal(WorldLocationIds.StarterTown, exit.LocationId);
        }

        await using GameDbContext reconnectContext = postgres.CreateDbContext();
        FixedTimeProvider reconnectTime = new(Now.AddMinutes(2));
        PartyService reconnectParties = new(reconnectContext, reconnectTime);
        DungeonService reconnectDungeons = new(
            reconnectContext,
            reconnectParties,
            contentProvider,
            reconnectTime);

        DungeonRunView? recovered = await reconnectDungeons.GetCurrentAsync(
            accountId,
            CancellationToken.None);

        Assert.NotNull(recovered);
        Assert.Equal(runId, recovered!.RunId);
        Assert.Equal(DungeonRunState.Active, recovered.State);
        Assert.Contains(recovered.Members, member =>
            member.CharacterId == characterId && member.State == DungeonRunMemberState.Active);
        Assert.Equal(
            WorldLocationIds.StarterTown,
            await reconnectContext.CharacterLocations
                .Where(location => location.CharacterId == characterId)
                .Select(location => location.LocationId)
                .SingleAsync());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
