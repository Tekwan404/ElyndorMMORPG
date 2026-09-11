using Elyndor.Core.Characters;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Economy;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Economy;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class CrystalWalletServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GrantAndReplayCreateOneLedgerEntryAndOneBalanceChange()
    {
        Guid accountId = await CreateAccountAsync();
        Guid operationId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        CrystalWalletService service = new(context, new FixedTimeProvider(Now));

        CrystalWalletOperationResult first = await service.GrantAsync(
            accountId, operationId, CrystalLedgerEntryType.AdminGrant, 150, "test-grant", CancellationToken.None);
        CrystalWalletOperationResult replay = await service.GrantAsync(
            accountId, operationId, CrystalLedgerEntryType.AdminGrant, 150, "test-grant", CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(150, first.Balance);
        Assert.Equal(first.Balance, replay.Balance);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(150, await verify.CrystalWallets.Where(wallet => wallet.AccountId == accountId)
            .Select(wallet => wallet.Balance).SingleAsync());
        Assert.Single(await verify.CrystalLedgerEntries.Where(entry => entry.AccountId == accountId).ToArrayAsync());
    }

    [Fact]
    public async Task SpendRejectsInsufficientBalanceWithoutCreatingLedgerEntry()
    {
        Guid accountId = await CreateAccountAsync();
        await using GameDbContext context = postgres.CreateDbContext();
        CrystalWalletService service = new(context, new FixedTimeProvider(Now));

        CrystalWalletOperationResult result = await service.SpendAsync(
            accountId, Guid.CreateVersion7(), CrystalLedgerEntryType.StorePurchase, 1, "test-purchase", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(CrystalWalletErrorCodes.InsufficientBalance, result.ErrorCode);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.CrystalLedgerEntries.ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentSpendsCannotDriveWalletBelowZero()
    {
        Guid accountId = await CreateAccountAsync();
        await using (GameDbContext seed = postgres.CreateDbContext())
        {
            CrystalWalletService service = new(seed, new FixedTimeProvider(Now));
            Assert.True((await service.GrantAsync(
                accountId, Guid.CreateVersion7(), CrystalLedgerEntryType.Payment, 100, "payment-1", CancellationToken.None)).Succeeded);
        }

        await using GameDbContext firstContext = postgres.CreateDbContext();
        await using GameDbContext secondContext = postgres.CreateDbContext();
        CrystalWalletService first = new(firstContext, new FixedTimeProvider(Now));
        CrystalWalletService second = new(secondContext, new FixedTimeProvider(Now));
        CrystalWalletOperationResult[] results = await Task.WhenAll(
            first.SpendAsync(accountId, Guid.CreateVersion7(), CrystalLedgerEntryType.StorePurchase, 75, "purchase-1", CancellationToken.None),
            second.SpendAsync(accountId, Guid.CreateVersion7(), CrystalLedgerEntryType.StorePurchase, 75, "purchase-2", CancellationToken.None));

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.ErrorCode == CrystalWalletErrorCodes.InsufficientBalance);
        await using GameDbContext verify = postgres.CreateDbContext();
        long balance = await verify.CrystalWallets.Where(wallet => wallet.AccountId == accountId)
            .Select(wallet => wallet.Balance).SingleAsync();
        long ledgerSum = await verify.CrystalLedgerEntries.Where(entry => entry.AccountId == accountId)
            .SumAsync(entry => entry.Delta);
        Assert.Equal(25, balance);
        Assert.Equal(balance, ledgerSum);
    }

    [Fact]
    public async Task DuplicateOperationWithDifferentPayloadIsRejected()
    {
        Guid accountId = await CreateAccountAsync();
        Guid operationId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        CrystalWalletService service = new(context, new FixedTimeProvider(Now));

        Assert.True((await service.GrantAsync(
            accountId, operationId, CrystalLedgerEntryType.Refund, 50, "refund-1", CancellationToken.None)).Succeeded);
        CrystalWalletOperationResult result = await service.GrantAsync(
            accountId, operationId, CrystalLedgerEntryType.Refund, 75, "refund-1", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(CrystalWalletErrorCodes.OperationConflict, result.ErrorCode);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(50, await verify.CrystalWallets.Where(wallet => wallet.AccountId == accountId)
            .Select(wallet => wallet.Balance).SingleAsync());
    }

    private async Task<Guid> CreateAccountAsync()
    {
        Guid accountId = Guid.CreateVersion7();
        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.Add(new Account(accountId, Random.Shared.NextInt64(1, long.MaxValue), Now));
        await context.SaveChangesAsync();
        return accountId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
