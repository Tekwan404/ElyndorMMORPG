using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Items;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class LootContainerServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 3, 6, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OpeningWorldBossChestConsumesOneAndReplayDoesNotDuplicateLoot()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();
        Guid chestItemId = Guid.CreateVersion7();
        Guid mutationId = Guid.CreateVersion7();

        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(
                accountId,
                Random.Shared.NextInt64(1, long.MaxValue),
                Now));
            var character = new Character(
                characterId,
                accountId,
                Guid.CreateVersion7(),
                "ChestTester",
                $"CHEST{characterId:N}"[..16].ToUpperInvariant(),
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now);
            character.SetLevel(30);
            setup.Characters.Add(character);
            setup.CharacterItems.Add(new CharacterItem(
                chestItemId,
                characterId,
                "WORLD_BOSS_ASH_ARCHON_CHEST",
                2,
                Now));
            await setup.SaveChangesAsync();
        }

        LootContainerOpenResult first;
        LootContainerOpenResult replay;
        await using (GameDbContext db = postgres.CreateDbContext())
        {
            var service = new LootContainerService(
                db,
                new StaticContentSnapshotProvider(package),
                new FixedTimeProvider(Now.AddMinutes(1)));

            first = await service.OpenAsync(
                accountId,
                chestItemId,
                mutationId,
                CancellationToken.None);
            replay = await service.OpenAsync(
                accountId,
                chestItemId,
                mutationId,
                CancellationToken.None);
        }

        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.False(first.WasReplay);
        Assert.InRange(first.Gold, 250, 500);
        Assert.Single(first.Items);

        Assert.True(replay.Succeeded, replay.ErrorCode);
        Assert.True(replay.WasReplay);
        Assert.Equal(0, replay.Gold);
        Assert.Empty(replay.Items);

        await using GameDbContext verify = postgres.CreateDbContext();
        Character characterState = await verify.Characters.SingleAsync(
            candidate => candidate.Id == characterId);
        Assert.Equal(first.Gold, characterState.Gold);
        Assert.Equal(
            1,
            await verify.CharacterItems
                .Where(item => item.Id == chestItemId)
                .Select(item => item.Quantity)
                .SingleAsync());
        Assert.Single(await verify.CharacterMutations
            .Where(mutation => mutation.CharacterId == characterId)
            .ToArrayAsync());

        int grantedItems = await verify.CharacterItems.CountAsync(item =>
            item.CharacterId == characterId
            && item.Id != chestItemId);
        int pendingItems = await verify.PendingLootItems.CountAsync(item =>
            item.CharacterId == characterId);
        Assert.Equal(1, grantedItems + pendingItems);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
