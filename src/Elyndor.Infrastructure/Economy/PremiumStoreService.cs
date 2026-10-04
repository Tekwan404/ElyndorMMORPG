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
    public const string InvalidQuantity = "premium_store_invalid_quantity";
}

public sealed record PremiumStorePurchaseResult(bool Succeeded, string? ErrorCode, long CrystalBalance)
{
    public static PremiumStorePurchaseResult Fail(string code, long balance = 0) => new(false, code, balance);
}
public sealed record PremiumStoreOfferSnapshot(PremiumStoreOfferDefinition Offer, ItemDefinition Item, bool CanPurchase, int MaxPackCount = 10000);
public sealed record PremiumStoreSnapshot(long CrystalBalance, IReadOnlyList<PremiumStoreOfferSnapshot> Offers);

public sealed class PremiumStoreService(GameDbContext dbContext, IContentSnapshotProvider contentProvider, TimeProvider timeProvider)
{
    public const int MaximumPackCount = 10000;
    private const int MaximumItemQuantity = 1000000;
    public async Task<PremiumStoreSnapshot> GetAsync(Guid accountId, CancellationToken cancellationToken)
    {
        GameContentSnapshot content = contentProvider.GetCurrent();
        long balance = await dbContext.CrystalWallets.AsNoTracking().Where(wallet => wallet.AccountId == accountId)
            .Select(wallet => (long?)wallet.Balance).SingleOrDefaultAsync(cancellationToken) ?? 0;
        var purchases = await dbContext.PremiumStorePurchases.AsNoTracking()
            .Where(purchase => purchase.AccountId == accountId).GroupBy(purchase => purchase.Sku)
            .Select(group => new { Sku = group.Key, Count = group.Sum(purchase => (long)purchase.PackCount) })
            .ToDictionaryAsync(group => group.Sku, group => group.Count, cancellationToken);
        PremiumStoreOfferSnapshot[] offers = (content.Package.PremiumStoreOffers ?? [])
            .Where(offer => offer.Enabled && content.Indexes.ItemsById.TryGetValue(offer.ItemDefinitionId, out ItemDefinition? item) && item.PremiumEligible)
            .Select(offer => new PremiumStoreOfferSnapshot(offer, content.Indexes.ItemsById[offer.ItemDefinitionId],
                offer.PerAccountLimit is not int limit || purchases.GetValueOrDefault(offer.Sku) < limit,
                (int)Math.Max(0, Math.Min(Math.Min(MaximumPackCount, MaximumItemQuantity / offer.Quantity),
                    offer.PerAccountLimit is int cap ? cap - purchases.GetValueOrDefault(offer.Sku) : MaximumPackCount))))
            .ToArray();
        return new PremiumStoreSnapshot(balance, offers);
    }
    public Task<PremiumStorePurchaseResult> PurchaseAsync(Guid accountId, string sku, Guid operationId, CancellationToken cancellationToken, int packCount = 1) =>
        operationId == Guid.Empty || string.IsNullOrWhiteSpace(sku)
            ? Task.FromResult(PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.InvalidOperation))
            : packCount is < 1 or > MaximumPackCount
                ? Task.FromResult(PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.InvalidQuantity))
                : dbContext.Database.CreateExecutionStrategy().ExecuteAsync(() => PurchaseCoreAsync(accountId, sku, operationId, packCount, cancellationToken));

    private async Task<PremiumStorePurchaseResult> PurchaseCoreAsync(Guid accountId, string sku, Guid operationId, int packCount, CancellationToken cancellationToken)
    {
        GameContentSnapshot content = contentProvider.GetCurrent();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Character? character = await dbContext.Characters.FromSqlInterpolated($"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (character is null) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.CharacterNotFound);
        PremiumStorePurchase? replay = await dbContext.PremiumStorePurchases.AsNoTracking().SingleOrDefaultAsync(item => item.OperationId == operationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.AccountId != accountId || replay.PackCount != packCount || !string.Equals(replay.Sku, sku, StringComparison.Ordinal))
                return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.OperationConflict);
            await transaction.CommitAsync(cancellationToken);
            long balance = await dbContext.CrystalWallets.Where(wallet => wallet.AccountId == accountId).Select(wallet => wallet.Balance).SingleAsync(cancellationToken);
            return new(true, null, balance);
        }
        PremiumStoreOfferDefinition? offer = content.Indexes.PremiumStoreOffersBySku.GetValueOrDefault(sku);
        if (offer is null) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.OfferNotFound);
        if (!offer.Enabled) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.OfferDisabled);
        int quantity;
        long price;
        try { quantity = checked(offer.Quantity * packCount); price = checked(offer.CrystalPrice * packCount); }
        catch (OverflowException) { return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.InvalidQuantity); }
        if (quantity > MaximumItemQuantity) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.InvalidQuantity);
        if (offer.PerAccountLimit is int limit && packCount + await dbContext.PremiumStorePurchases
            .Where(item => item.AccountId == accountId && item.Sku == sku)
            .SumAsync(item => (long)item.PackCount, cancellationToken) > limit)
            return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.LimitReached);
        CrystalWallet? wallet = await dbContext.CrystalWallets.FromSqlInterpolated(
            $"SELECT * FROM game.crystal_wallets WHERE \"AccountId\" = {accountId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (wallet is null || wallet.Balance < price) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.InsufficientCrystals, wallet?.Balance ?? 0);
        ItemDefinition definition = content.Indexes.ItemsById[offer.ItemDefinitionId];
        if (!definition.PremiumEligible) return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.OfferNotFound);
        if (!await InventoryCapacity.CanAddAsync(dbContext, character.Id, definition, quantity, content, cancellationToken))
            return PremiumStorePurchaseResult.Fail(PremiumStoreErrorCodes.InventoryFull, wallet.Balance);
        wallet.TryDebit(price);
        await AddItemAsync(character.Id, definition, quantity, cancellationToken);
        dbContext.PremiumStorePurchases.Add(new PremiumStorePurchase(operationId, accountId, character.Id, sku, definition.Id, quantity, price, timeProvider.GetUtcNow(), packCount));
        dbContext.CrystalLedgerEntries.Add(new CrystalLedgerEntry(Guid.CreateVersion7(), accountId, operationId, CrystalLedgerEntryType.StorePurchase, -price, wallet.Balance, sku, Fingerprint($"{sku}:{packCount}"), timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, null, wallet.Balance);
    }

    private async Task AddItemAsync(
        Guid characterId,
        ItemDefinition definition,
        int quantity,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!definition.Stackable)
        {
            for (var ordinal = 0; ordinal < quantity; ordinal++)
            {
                dbContext.CharacterItems.Add(new CharacterItem(
                    Guid.CreateVersion7(),
                    characterId,
                    definition.Id,
                    1,
                    now.AddTicks(ordinal),
                    definition.Version));
            }
            return;
        }

        int remaining = quantity;
        CharacterItem[] stacks = await dbContext.CharacterItems
            .Where(item => item.CharacterId == characterId
                && item.ItemDefinitionId == definition.Id
                && item.DefinitionVersion == definition.Version
                && item.Quantity < definition.MaxStack && item.TransactionLockId == null)
            .Where(item => !dbContext.CharacterEquipment.Any(equipment =>
                equipment.CharacterId == characterId
                && equipment.CharacterItemId == item.Id))
            .OrderBy(item => item.AcquiredAtUtc)
            .ToArrayAsync(cancellationToken);

        foreach (CharacterItem stack in stacks)
        {
            if (remaining <= 0) break;
            int added = Math.Min(definition.MaxStack - stack.Quantity, remaining);
            if (added <= 0) continue;
            stack.AddQuantity(added, definition.MaxStack);
            remaining -= added;
        }

        var ordinalIndex = 0;
        while (remaining > 0)
        {
            int stackSize = Math.Min(definition.MaxStack, remaining);
            dbContext.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                definition.Id,
                stackSize,
                now.AddTicks(ordinalIndex++),
                definition.Version));
            remaining -= stackSize;
        }
    }

    private static string Fingerprint(string sku) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sku)));
}
