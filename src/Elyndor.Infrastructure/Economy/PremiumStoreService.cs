using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Economy;

public static class PremiumStoreErrorCodes
{
    public const string OfferNotFound = "premium_store_offer_not_found";
    public const string OfferDisabled = "premium_store_offer_disabled";
    public const string LimitReached = "premium_store_limit_reached";
    public const string CharacterNotFound = "premium_store_character_not_found";
    public const string InsufficientCrystals = "premium_store_insufficient_crystals";
    public const string InvalidOperation = "premium_store_invalid_operation";
    public const string OperationConflict = "premium_store_operation_conflict";
    public const string InventoryFull = "premium_store_inventory_full";
}

public sealed record PremiumStorePurchaseResult(bool Succeeded, string? ErrorCode, long CrystalBalance)
{
    public static PremiumStorePurchaseResult Fail(string code, long balance = 0) => new(false, code, balance);
}
public sealed record PremiumStoreOfferSnapshot(PremiumStoreOfferDefinition Offer, ItemDefinition Item, bool CanPurchase);
public sealed record PremiumStoreSnapshot(long CrystalBalance, IReadOnlyList<PremiumStoreOfferSnapshot> Offers);

public sealed class PremiumStoreService(GameDbContext dbContext, IContentSnapshotProvider contentProvider, TimeProvider timeProvider)
{
    public async Task<PremiumStoreSnapshot> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        GameContentSnapshot content = contentProvider.GetCurrent();
        long balance = await dbContext.CrystalWallets.AsNoTracking().Where(wallet => wallet.AccountId == accountId)
            .Select(wallet => (long?)wallet.Balance).SingleOrDefaultAsync(cancellationToken) ?? 0;
        PremiumStorePurchase[] purchases = await dbContext.PremiumStorePurchases.AsNoTracking()
            .Where(purchase => purchase.AccountId == accountId).ToArrayAsync(cancellationToken);
        PremiumStoreOfferSnapshot[] offers = (content.Package.PremiumStoreOffers ?? [])
            .Where(offer => offer.Enabled && content.Indexes.ItemsById.TryGetValue(offer.ItemDefinitionId, out ItemDefinition? item) && item.PremiumEligible)
            .Select(offer => new PremiumStoreOfferSnapshot(offer, content.Indexes.ItemsById[offer.ItemDefinitionId],
                offer.PerAccountLimit is not int limit || purchases.Count(purchase => purchase.Sku == offer.Sku) < limit))
            .ToArray();
        return new PremiumStoreSnapshot(balance, offers);
    }
    public Task<PremiumStorePurchaseResult> PurchaseAsync(Guid accountId, string sku, Guid operationId, CancellationToken cancellationToken) =>
        operationId == Guid.Empty || string.IsNullOrWhiteSpace(sku)
            ? Task.FromResult(PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.InvalidOperation))
            : dbContext.Database.CreateExecutionStrategy().ExecuteAsync(() => PurchaseCoreAsync(accountId, sku, operationId, cancellationToken));

    private async Task<PremiumStorePurchaseResult> PurchaseCoreAsync(Guid accountId, string sku, Guid operationId, CancellationToken cancellationToken)
    {
        PremiumStoreOfferDefinition? offer = contentProvider.GetCurrent().Indexes.PremiumStoreOffersBySku.GetValueOrDefault(sku);
        if (offer is null) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.OfferNotFound);
        if (!offer.Enabled) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.OfferDisabled);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Character? character = await dbContext.Characters.FromSqlInterpolated($"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (character is null) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.CharacterNotFound);
        PremiumStorePurchase? replay = await dbContext.PremiumStorePurchases.AsNoTracking().SingleOrDefaultAsync(item => item.OperationId == operationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.AccountId != accountId || !string.Equals(replay.Sku, sku, StringComparison.Ordinal))
                return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.OperationConflict);
            await transaction.CommitAsync(cancellationToken);
            long balance = await dbContext.CrystalWallets.Where(wallet => wallet.AccountId == accountId).Select(wallet => wallet.Balance).SingleAsync(cancellationToken);
            return new(true, null, balance);
        }
        if (offer.PerAccountLimit is int limit && await dbContext.PremiumStorePurchases.CountAsync(item => item.AccountId == accountId && item.Sku == sku, cancellationToken) >= limit) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.LimitReached);
        CrystalWallet? wallet = await dbContext.CrystalWallets.SingleOrDefaultAsync(item => item.AccountId == accountId, cancellationToken);
        if (wallet is null || !wallet.TryDebit(offer.CrystalPrice)) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.InsufficientCrystals, wallet?.Balance ?? 0);
        ItemDefinition definition = contentProvider.GetCurrent().Indexes.ItemsById[offer.ItemDefinitionId];
        if (!definition.PremiumEligible) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.OfferNotFound);
        if (!await InventoryCapacity.CanAddAsync(dbContext, character.Id, definition, offer.Quantity, contentProvider.GetCurrent(), cancellationToken))
            return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.InventoryFull, wallet.Balance);
        dbContext.CharacterItems.Add(new CharacterItem(Guid.CreateVersion7(), character.Id, definition.Id, offer.Quantity, timeProvider.GetUtcNow(), definition.Version));
        dbContext.PremiumStorePurchases.Add(new PremiumStorePurchase(operationId, accountId, character.Id, sku, definition.Id, offer.Quantity, offer.CrystalPrice, timeProvider.GetUtcNow()));
        dbContext.CrystalLedgerEntries.Add(new CrystalLedgerEntry(Guid.CreateVersion7(), accountId, operationId, CrystalLedgerEntryType.StorePurchase, -offer.CrystalPrice, wallet.Balance, sku, Fingerprint(sku), timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, null, wallet.Balance);
    }
    private static string Fingerprint(string sku) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sku)));
}
