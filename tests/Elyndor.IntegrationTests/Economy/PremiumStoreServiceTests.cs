using Elyndor.Core.Economy;
using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
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
