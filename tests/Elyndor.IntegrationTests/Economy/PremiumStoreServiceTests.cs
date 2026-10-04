using Elyndor.Core.Economy;
using Elyndor.Core.Content;
using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Economy;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Economy;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class PremiumStoreServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 13, 0, 0, TimeSpan.Zero);
    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BulkPurchaseTotalsAndReplayAreAtomicAndQuantitySpecific()
    {
        Guid accountId = await CreateAccountAsync();
        await using var context = postgres.CreateDbContext();
        await new CrystalWalletService(context, new FixedTimeProvider(Now)).GrantAsync(accountId,
            Guid.NewGuid(), CrystalLedgerEntryType.AdminGrant, 2000, "bulk", CancellationToken.None);
        var store = new PremiumStoreService(context, new StaticContentSnapshotProvider(
            await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"))), new FixedTimeProvider(Now));
        Guid operation = Guid.NewGuid();
        var first = await store.PurchaseAsync(accountId, "REFORGE_STONES_SMALL", operation, CancellationToken.None, 50);
        var replay = await store.PurchaseAsync(accountId, "REFORGE_STONES_SMALL", operation, CancellationToken.None, 50);
        var conflict = await store.PurchaseAsync(accountId, "REFORGE_STONES_SMALL", operation, CancellationToken.None, 51);
        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.True(replay.Succeeded);
        Assert.Equal(750, first.CrystalBalance);
        Assert.Equal(PremiumStoreErrorCodes.OperationConflict, conflict.ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(500, verify.CharacterItems.Where(item => item.ItemDefinitionId == "REFORGE_STONE").Sum(item => item.Quantity));
        Assert.Equal(1250, Assert.Single(verify.PremiumStorePurchases.Where(purchase => purchase.AccountId == accountId)).CrystalPrice);
        Assert.Single(verify.CrystalLedgerEntries.Where(entry => entry.EntryType == CrystalLedgerEntryType.StorePurchase));
    }

    [Theory]
    [InlineData(51, 2000, false, null)]
    [InlineData(51, 100, false, "premium_store_insufficient_crystals")]
    [InlineData(50, 2000, true, "premium_store_inventory_full")]
    public async Task BulkPurchaseValidatesTotalBeforeMutation(int packs, long balance, bool full, string? error)
    {
        Guid accountId = await CreateAccountAsync();
        await using var context = postgres.CreateDbContext();
        await new CrystalWalletService(context, new FixedTimeProvider(Now)).GrantAsync(accountId,
            Guid.NewGuid(), CrystalLedgerEntryType.AdminGrant, balance, "bulk", CancellationToken.None);
        Guid characterId = context.Characters.Single(character => character.AccountId == accountId).Id;
        if (full)
        {
            for (int index = 0; index < InventoryCapacity.DefaultCapacity; index++)
                context.CharacterItems.Add(new CharacterItem(Guid.CreateVersion7(), characterId, "RECRUIT_IRON_SWORD", 1, Now.AddTicks(index)));
            await context.SaveChangesAsync();
        }
        var package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var store = new PremiumStoreService(context, new StaticContentSnapshotProvider(package), new FixedTimeProvider(Now));
        var result = await store.PurchaseAsync(accountId, "REFORGE_STONES_SMALL", Guid.NewGuid(), CancellationToken.None, packs);
        Assert.Equal(error, result.ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(error is null ? balance - packs * 25 : balance, verify.CrystalWallets.Single().Balance);
        if (error is not null)
        {
            Assert.Empty(verify.PremiumStorePurchases.Where(purchase => purchase.AccountId == accountId));
            Assert.Empty(verify.CrystalLedgerEntries.Where(entry => entry.EntryType == CrystalLedgerEntryType.StorePurchase));
        }
        else
        {
            Assert.Equal(packs * 10, verify.CharacterItems.Sum(item => item.Quantity));
            Assert.All(verify.CharacterItems, item => Assert.InRange(item.Quantity, 1, package.Items!.Single(definition => definition.Id == item.ItemDefinitionId).MaxStack));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task InvalidPackCountNeverSpendsCurrency(int count)
    {
        Guid accountId = await CreateAccountAsync();
        await using var context = postgres.CreateDbContext();
        var store = new PremiumStoreService(context, new StaticContentSnapshotProvider(
            await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"))), new FixedTimeProvider(Now));
        Assert.Equal("premium_store_invalid_quantity", (await store.PurchaseAsync(accountId,
            "REFORGE_STONES_SMALL", Guid.NewGuid(), CancellationToken.None, count)).ErrorCode);
        Assert.Empty(context.PremiumStorePurchases.Where(purchase => purchase.AccountId == accountId));
    }

    [Fact]
    public async Task LimitsCountPacksAndConcurrentReplayCommitsOnce()
    {
        Guid accountId = await CreateAccountAsync();
        var package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        package = package with { PremiumStoreOffers = package.PremiumStoreOffers!.Select(offer =>
            offer.Sku == "REFORGE_STONES_SMALL" ? offer with { PerAccountLimit = 51 } : offer).ToArray() };
        await using (var setup = postgres.CreateDbContext())
            await new CrystalWalletService(setup, new FixedTimeProvider(Now)).GrantAsync(accountId,
                Guid.NewGuid(), CrystalLedgerEntryType.AdminGrant, 2000, "concurrent", CancellationToken.None);
        Guid mutation = Guid.NewGuid();
        async Task<PremiumStorePurchaseResult> BuyAsync()
        {
            await using var context = postgres.CreateDbContext();
            return await new PremiumStoreService(context, new StaticContentSnapshotProvider(package), new FixedTimeProvider(Now))
                .PurchaseAsync(accountId, "REFORGE_STONES_SMALL", mutation, CancellationToken.None, 50);
        }
        var results = await Task.WhenAll(BuyAsync(), BuyAsync());
        Assert.All(results, result => Assert.True(result.Succeeded, result.ErrorCode));
        await using var verify = postgres.CreateDbContext();
        Assert.Single(verify.PremiumStorePurchases.Where(purchase => purchase.AccountId == accountId));
        var store = new PremiumStoreService(verify, new StaticContentSnapshotProvider(package), new FixedTimeProvider(Now));
        Assert.Equal(1, (await store.GetAsync(accountId, CancellationToken.None)).Offers.Single(offer => offer.Offer.Sku == "REFORGE_STONES_SMALL").MaxPackCount);
        Assert.Equal(PremiumStoreErrorCodes.LimitReached, (await store.PurchaseAsync(accountId,
            "REFORGE_STONES_SMALL", Guid.NewGuid(), CancellationToken.None, 2)).ErrorCode);
    }

    [Fact]
    public async Task BulkPurchaseFillsExistingStackBeforeCreatingBoundedStacks()
    {
        Guid accountId = await CreateAccountAsync();
        var package = await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"));
        var definition = package.Items!.Single(item => item.Id == "REFORGE_STONE");
        await using var context = postgres.CreateDbContext();
        Guid characterId = context.Characters.Single(character => character.AccountId == accountId).Id;
        Guid existingId = Guid.NewGuid();
        context.CharacterItems.Add(new CharacterItem(existingId, characterId, definition.Id,
            definition.MaxStack - 10, Now, definition.Version));
        await context.SaveChangesAsync();
        await new CrystalWalletService(context, new FixedTimeProvider(Now)).GrantAsync(accountId,
            Guid.NewGuid(), CrystalLedgerEntryType.AdminGrant, 2000, "stacking", CancellationToken.None);
        var result = await new PremiumStoreService(context, new StaticContentSnapshotProvider(package), new FixedTimeProvider(Now))
            .PurchaseAsync(accountId, "REFORGE_STONES_SMALL", Guid.NewGuid(), CancellationToken.None, 50);
        Assert.True(result.Succeeded, result.ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(definition.MaxStack, verify.CharacterItems.Single(item => item.Id == existingId).Quantity);
        Assert.Equal(definition.MaxStack - 10 + 500, verify.CharacterItems.Sum(item => item.Quantity));
        Assert.All(verify.CharacterItems, item => Assert.InRange(item.Quantity, 1, definition.MaxStack));
    }

    [Fact]
    public async Task PurchaseUsesServerSkuPriceAndReplayGrantsItemsOnlyOnce()
    {
        Guid accountId = await CreateAccountAsync();
        await using GameDbContext context = postgres.CreateDbContext();
        CrystalWalletService wallet = new(context, new FixedTimeProvider(Now));
        await wallet.GrantAsync(accountId, Guid.CreateVersion7(), CrystalLedgerEntryType.AdminGrant, 100, "test", CancellationToken.None);
        PremiumStoreService store = new(context, new StaticContentSnapshotProvider(
            await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"))), new FixedTimeProvider(Now));
        Guid mutationId = Guid.CreateVersion7();

        PremiumStorePurchaseResult first = await store.PurchaseAsync(accountId, "REFORGE_STONES_SMALL", mutationId, CancellationToken.None);
        PremiumStorePurchaseResult replay = await store.PurchaseAsync(accountId, "REFORGE_STONES_SMALL", mutationId, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(75, first.CrystalBalance);
        Assert.Equal(first.CrystalBalance, replay.CrystalBalance);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(10, verify.CharacterItems.Where(item => item.ItemDefinitionId == "REFORGE_STONE").Sum(item => item.Quantity));
    }

    [Fact]
    public async Task EnhancementOreOfferUsesServerPriceAndReplayGrantsOreOnlyOnce()
    {
        Guid accountId = await CreateAccountAsync();
        await using GameDbContext context = postgres.CreateDbContext();
        CrystalWalletService wallet = new(context, new FixedTimeProvider(Now));
        await wallet.GrantAsync(accountId, Guid.CreateVersion7(), CrystalLedgerEntryType.AdminGrant, 100, "test", CancellationToken.None);
        PremiumStoreService store = new(context, new StaticContentSnapshotProvider(
            await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"))), new FixedTimeProvider(Now));
        Guid mutationId = Guid.CreateVersion7();

        PremiumStorePurchaseResult first = await store.PurchaseAsync(accountId, "ENHANCEMENT_ORE_SMALL", mutationId, CancellationToken.None);
        PremiumStorePurchaseResult replay = await store.PurchaseAsync(accountId, "ENHANCEMENT_ORE_SMALL", mutationId, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(70, first.CrystalBalance);
        Assert.Equal(first.CrystalBalance, replay.CrystalBalance);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(20, verify.CharacterItems.Where(item => item.ItemDefinitionId == "ENHANCEMENT_ORE").Sum(item => item.Quantity));
    }

    [Fact]
    public async Task TestStoreOffersRemainRepeatableBeyondFormerLifetimeLimit()
    {
        Guid accountId = await CreateAccountAsync();
        await using GameDbContext context = postgres.CreateDbContext();
        CrystalWalletService wallet = new(context, new FixedTimeProvider(Now));
        await wallet.GrantAsync(
            accountId,
            Guid.CreateVersion7(),
            CrystalLedgerEntryType.AdminGrant,
            2_000,
            "test-store-unlimited",
            CancellationToken.None);
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        var content = new StaticContentSnapshotProvider(package);
        PremiumStoreService store = new(context, content, new FixedTimeProvider(Now));

        for (int purchase = 0; purchase < 25; purchase++)
        {
            PremiumStorePurchaseResult result = await store.PurchaseAsync(
                accountId,
                "REFORGE_STONES_SMALL",
                Guid.CreateVersion7(),
                CancellationToken.None);
            Assert.True(result.Succeeded, result.ErrorCode);
        }

        PremiumStoreSnapshot snapshot = await store.GetAsync(accountId, CancellationToken.None);
        PremiumStoreOfferSnapshot offer = Assert.Single(
            snapshot.Offers,
            item => item.Offer.Sku == "REFORGE_STONES_SMALL");
        Assert.True(offer.CanPurchase);
        Assert.Null(offer.Offer.PerAccountLimit);
        Assert.All(package.PremiumStoreOffers!, item => Assert.Null(item.PerAccountLimit));
    }

    private async Task<Guid> CreateAccountAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        context.Characters.Add(new Character(Guid.CreateVersion7(), accountId, Guid.CreateVersion7(), "Buyer", "BUYER00000000001", "HUMAN", "MALE", "WARRIOR", Now));
        await context.SaveChangesAsync();
        return accountId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
