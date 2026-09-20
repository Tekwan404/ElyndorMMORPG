using Elyndor.Core.Characters;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Economy;
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
        "SPATIAL_VOID_SEAL"
    ];

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CatalogListsSpatialArtifactsAndPurchaseGrantsArtifactOnce()
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
        Assert.Equal(800, purchase.CrystalBalance);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Single(verify.CharacterItems.Where(item =>
            item.ItemDefinitionId == "SPATIAL_EXPANDED_RING"));

        PremiumStoreSnapshot afterPurchase = await store.GetAsync(accountId, CancellationToken.None);
        PremiumStoreOfferSnapshot purchasedOffer = Assert.Single(
            afterPurchase.Offers,
            offer => offer.Offer.Sku == "SPATIAL_EXPANDED_RING");
        Assert.False(purchasedOffer.CanPurchase);
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
