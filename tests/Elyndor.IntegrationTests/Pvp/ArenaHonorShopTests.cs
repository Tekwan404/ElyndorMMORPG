using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Pvp;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Pvp;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ArenaHonorShopTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 6, 21, 50, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PurchaseDebitsHonorAndGrantsExactlyOneItemOnReplay()
    {
        var (accountId, characterId) = await CreateCharacterAsync(level: 60, honor: 200);
        GameContentPackage content = await ContentWithOfferAsync(
            "TEST_ARENA_HONOR_CHEST",
            100);
        Guid mutationId = Guid.CreateVersion7();

        await using (GameDbContext db = postgres.CreateDbContext())
        {
            var service = new ArenaHonorShopService(
                db,
                new StaticContentSnapshotProvider(content),
                new FixedTime(Now));

            ArenaHonorShopOperationResult first = await service.BuyAsync(
                accountId,
                "TEST_ARENA_HONOR_CHEST",
                mutationId,
                default);
            ArenaHonorShopOperationResult replay = await service.BuyAsync(
                accountId,
                "TEST_ARENA_HONOR_CHEST",
                mutationId,
                default);

            Assert.True(first.Succeeded);
            Assert.True(replay.Succeeded);
            Assert.Equal(100, first.Snapshot!.Honor);
            Assert.Equal(100, replay.Snapshot!.Honor);
        }

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            100,
            (await verify.ArenaHonorWallets.SingleAsync(
                wallet => wallet.CharacterId == characterId)).Balance);
        Assert.Equal(
            1,
            await verify.CharacterItems.CountAsync(
                item => item.CharacterId == characterId
                    && item.ItemDefinitionId == "TEST_ARENA_HONOR_CHEST"));
        Assert.Equal(
            1,
            await verify.CharacterMutations.CountAsync(
                mutation => mutation.CharacterId == characterId
                    && mutation.MutationId == mutationId));
    }

    [Fact]
    public async Task PurchaseWithInsufficientHonorChangesNothing()
    {
        var (accountId, characterId) = await CreateCharacterAsync(level: 60, honor: 50);
        GameContentPackage content = await ContentWithOfferAsync(
            "TEST_ARENA_HONOR_CHEST",
            100);

        await using (GameDbContext db = postgres.CreateDbContext())
        {
            var service = new ArenaHonorShopService(
                db,
                new StaticContentSnapshotProvider(content),
                new FixedTime(Now));

            ArenaHonorShopOperationResult result = await service.BuyAsync(
                accountId,
                "TEST_ARENA_HONOR_CHEST",
                Guid.CreateVersion7(),
                default);

            Assert.False(result.Succeeded);
            Assert.Equal(ArenaHonorShopErrorCodes.NotEnoughHonor, result.ErrorCode);
        }

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(
            50,
            (await verify.ArenaHonorWallets.SingleAsync(
                wallet => wallet.CharacterId == characterId)).Balance);
        Assert.DoesNotContain(
            await verify.CharacterItems
                .Where(item => item.CharacterId == characterId)
                .ToArrayAsync(),
            item => item.ItemDefinitionId == "TEST_ARENA_HONOR_CHEST");
        Assert.Empty(await verify.CharacterMutations
            .Where(mutation => mutation.CharacterId == characterId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task ShopListsOnlyHonorOffersForTheCharactersClass()
    {
        var (accountId, _) = await CreateCharacterAsync(level: 60, honor: 200);
        GameContentPackage content = await ContentWithOfferAsync(
            "TEST_ARENA_HONOR_CHEST",
            100);

        ItemDefinition source = content.Items!
            .Single(item => item.Id == "TEST_ARENA_HONOR_CHEST");
        ItemDefinition mageOffer = source with
        {
            Id = "TEST_ARENA_MAGE_HONOR_CHEST",
            Name = "Mage Honor Chest",
            AllowedClassIds = ["MAGE"]
        };
        content = content with
        {
            Items = content.Items!.Concat([mageOffer]).ToArray()
        };

        await using GameDbContext db = postgres.CreateDbContext();
        var service = new ArenaHonorShopService(
            db,
            new StaticContentSnapshotProvider(content),
            new FixedTime(Now));

        ArenaHonorShopOperationResult result = await service.GetAsync(accountId, default);

        Assert.True(result.Succeeded);
        Assert.Equal(200, result.Snapshot!.Honor);
        ItemDefinition offer = Assert.Single(result.Snapshot.Items);
        Assert.Equal("TEST_ARENA_HONOR_CHEST", offer.Id);
        Assert.Equal(100, offer.HonorPrice);
    }

    private async Task<(Guid AccountId, Guid CharacterId)> CreateCharacterAsync(
        int level,
        long honor)
    {
        Guid accountId = Guid.CreateVersion7();
        Guid characterId = Guid.CreateVersion7();

        await using GameDbContext db = postgres.CreateDbContext();
        db.Accounts.Add(new Account(
            accountId,
            Random.Shared.NextInt64(1, long.MaxValue),
            Now));
        var character = new Character(
            characterId,
            accountId,
            Guid.CreateVersion7(),
            "HonorShop",
            $"HON{characterId:N}"[..16].ToUpperInvariant(),
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now);
        character.SetLevel(level);
        db.Characters.Add(character);

        var wallet = new ArenaHonorWallet(characterId);
        if (honor > 0)
            wallet.Grant(honor);
        db.ArenaHonorWallets.Add(wallet);

        await db.SaveChangesAsync();
        return (accountId, characterId);
    }

    private static async Task<GameContentPackage> ContentWithOfferAsync(
        string itemId,
        long honorPrice)
    {
        GameContentPackage content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        ItemDefinition source = content.Items!
            .Single(item => item.Id == "WARRIOR_COMMON_BORDER_STEEL_CHEST");
        ItemDefinition offer = source with
        {
            Id = itemId,
            Name = "Arena Honor Chest",
            HonorPrice = honorPrice,
            AllowedClassIds = ["WARRIOR"]
        };

        return content with
        {
            Items = content.Items!.Concat([offer]).ToArray()
        };
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
