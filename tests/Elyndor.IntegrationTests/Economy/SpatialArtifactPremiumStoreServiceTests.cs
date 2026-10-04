using Elyndor.Core.Characters;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Economy;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Economy;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class SpatialArtifactPremiumStoreServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 19, 30, 0, TimeSpan.Zero);
    private static readonly string[] SpatialArtifactSkus =
    [
        "SPATIAL_CRACKED_RING",
        "SPATIAL_MINOR_RING",
        "SPATIAL_EXPANDED_RING",
        "SPATIAL_SEAL",
        "SPATIAL_BOTTOMLESS_RING",
        "SPATIAL_POCKET_SHARD",
        "SPATIAL_VOID_SEAL",
        "SPATIAL_ASTRAL_RING",
        "SPATIAL_DIMENSION_CORE",
        "SPATIAL_ETERNITY_SEAL"
    ];

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("SPATIAL_ASTRAL_RING", 100, 2250)]
    [InlineData("SPATIAL_DIMENSION_CORE", 150, 3250)]
    [InlineData("SPATIAL_ETERNITY_SEAL", 250, 5000)]
    public async Task NewArtifactCanBePurchasedAndEquipped(string sku, int bonus, long price)
    {
        Guid accountId = await CreateAccountAsync();
        var provider = new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json")));
        await using (var context = postgres.CreateDbContext())
        {
            await new CrystalWalletService(context, new FixedTimeProvider(Now)).GrantAsync(accountId,
                Guid.NewGuid(), CrystalLedgerEntryType.AdminGrant, price, "artifact", CancellationToken.None);
            Assert.True((await new PremiumStoreService(context, provider, new FixedTimeProvider(Now))
                .PurchaseAsync(accountId, sku, Guid.NewGuid(), CancellationToken.None)).Succeeded);
        }
        await using var spatialContext = postgres.CreateDbContext();
        Guid itemId = spatialContext.CharacterItems.Single(item => item.ItemDefinitionId == sku).Id;
        var equipped = await new SpatialInventoryService(spatialContext, provider).EquipAsync(accountId, itemId, CancellationToken.None);
        Assert.True(equipped.IsSuccess, equipped.ErrorCode);
        Assert.Equal(InventoryCapacity.DefaultCapacity + bonus, equipped.Snapshot!.Capacity.Capacity);
    }

    [Fact]
    public async Task CatalogListsSpatialArtifactsAndPurchaseRemainsRepeatable()
    {
        Guid accountId = await CreateAccountAsync();
        await using GameDbContext context = postgres.CreateDbContext();
        CrystalWalletService wallet = new(context, new FixedTimeProvider(Now));
        await wallet.GrantAsync(
            accountId,
            Guid.CreateVersion7(),
            CrystalLedgerEntryType.AdminGrant,
            1000,
            "spatial-store-test",
            CancellationToken.None);

        PremiumStoreService store = new(
            context,
            new StaticContentSnapshotProvider(
                await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json"))),
            new FixedTimeProvider(Now));

        PremiumStoreSnapshot catalog = await store.GetAsync(accountId, CancellationToken.None);
        Assert.All(
            SpatialArtifactSkus,
            sku => Assert.Contains(catalog.Offers, offer => offer.Offer.Sku == sku && offer.CanPurchase));

        PremiumStorePurchaseResult purchase = await store.PurchaseAsync(
            accountId,
            "SPATIAL_EXPANDED_RING",
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(purchase.Succeeded);
        Assert.Equal(650, purchase.CrystalBalance);

        PremiumStoreSnapshot afterPurchase = await store.GetAsync(accountId, CancellationToken.None);
        PremiumStoreOfferSnapshot purchasedOffer = Assert.Single(
            afterPurchase.Offers,
            offer => offer.Offer.Sku == "SPATIAL_EXPANDED_RING");
        Assert.True(purchasedOffer.CanPurchase);

        PremiumStorePurchaseResult secondPurchase = await store.PurchaseAsync(
            accountId,
            "SPATIAL_EXPANDED_RING",
            Guid.CreateVersion7(),
            CancellationToken.None);

        Assert.True(secondPurchase.Succeeded);
        Assert.Equal(300, secondPurchase.CrystalBalance);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(2, verify.CharacterItems.Count(item =>
            item.ItemDefinitionId == "SPATIAL_EXPANDED_RING"));
    }

    private async Task<Guid> CreateAccountAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(
            accountId,
            Random.Shared.NextInt64(1, long.MaxValue),
            Now));
        context.Characters.Add(new Character(
            Guid.CreateVersion7(),
            accountId,
            Guid.CreateVersion7(),
            "SpatialBuyer",
            "SPATIALBUYER0001",
            "HUMAN",
            "MALE",
            "WARRIOR",
            Now));
        await context.SaveChangesAsync();
        return accountId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
