using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Items;

public static class MerchantErrorCodes
{
    public const string CharacterNotFound = "merchant_character_not_found";
    public const string MerchantNotFound = "merchant_not_found";
    public const string InvalidLocation = "merchant_invalid_location";
    public const string ItemNotSold = "merchant_item_not_sold";
    public const string ItemNotOwned = "merchant_item_not_owned";
    public const string ItemNotSellable = "merchant_item_not_sellable";
    public const string InvalidQuantity = "merchant_invalid_quantity";
    public const string NotEnoughGold = "merchant_not_enough_gold";
    public const string Conflict = "merchant_conflict";
}

public sealed record MerchantCatalogItem(
    ItemDefinition Definition,
    int SellPriceGold);

public sealed record MerchantSnapshot(
    MerchantDefinition Merchant,
    long Gold,
    IReadOnlyList<MerchantCatalogItem> Items);

public sealed record MerchantOperationResult(
    bool IsSuccess,
    string? ErrorCode,
    MerchantSnapshot? Snapshot)
{
    public static MerchantOperationResult Success(MerchantSnapshot snapshot) => new(true, null, snapshot);
    public static MerchantOperationResult Failure(string errorCode) => new(false, errorCode, null);
}

public sealed class MerchantService(
    GameDbContext dbContext,
    GameContentPackage content,
    TimeProvider timeProvider)
{
    public async Task<MerchantOperationResult> GetAsync(
        Guid accountId,
        string merchantId,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null) return MerchantOperationResult.Failure(MerchantErrorCodes.CharacterNotFound);

        MerchantDefinition? merchant = FindMerchant(merchantId);
        if (merchant is null) return MerchantOperationResult.Failure(MerchantErrorCodes.MerchantNotFound);
        if (!await IsAtMerchantLocationAsync(character.Id, merchant, cancellationToken))
            return MerchantOperationResult.Failure(MerchantErrorCodes.InvalidLocation);

        return MerchantOperationResult.Success(ToSnapshot(merchant, character.Gold));
    }

    public Task<MerchantOperationResult> BuyAsync(
        Guid accountId,
        string merchantId,
        string itemDefinitionId,
        int quantity,
        CancellationToken cancellationToken) => ExecuteAsync(
            accountId,
            merchantId,
            async (character, merchant) =>
            {
                if (quantity < 1 || quantity > 20)
                    return MerchantErrorCodes.InvalidQuantity;

                if (!merchant.ItemIds.Contains(itemDefinitionId, StringComparer.Ordinal))
                    return MerchantErrorCodes.ItemNotSold;

                ItemDefinition? definition = FindItem(itemDefinitionId);
                if (definition is null || definition.BuyPriceGold <= 0)
                    return MerchantErrorCodes.ItemNotSold;

                long totalPrice = checked((long)definition.BuyPriceGold * quantity);
                if (!character.TrySpendGold(totalPrice))
                    return MerchantErrorCodes.NotEnoughGold;

                await AddItemAsync(character.Id, definition, quantity, cancellationToken);
                return null;
            },
            cancellationToken);

    public Task<MerchantOperationResult> SellMaterialAsync(
        Guid accountId,
        string merchantId,
        Guid characterItemId,
        int quantity,
        CancellationToken cancellationToken) => ExecuteAsync(
            accountId,
            merchantId,
            async (character, _) =>
            {
                if (quantity < 1 || quantity > 99)
                    return MerchantErrorCodes.InvalidQuantity;

                CharacterItem? item = await dbContext.CharacterItems
                    .SingleOrDefaultAsync(candidate => candidate.Id == characterItemId, cancellationToken);
                if (item is null || item.CharacterId != character.Id)
                    return MerchantErrorCodes.ItemNotOwned;

                ItemDefinition? definition = FindItem(item.ItemDefinitionId);
                if (definition is null || definition.Type != ItemType.Material)
                    return MerchantErrorCodes.ItemNotSellable;
                if (quantity > item.Quantity)
                    return MerchantErrorCodes.InvalidQuantity;

                int unitPrice = ResolveSellPrice(definition);
                if (unitPrice <= 0) return MerchantErrorCodes.ItemNotSellable;

                item.RemoveQuantity(quantity);
                if (item.Quantity == 0) dbContext.CharacterItems.Remove(item);
                character.AddGold(checked((long)unitPrice * quantity));
                return null;
            },
            cancellationToken);

    private async Task<MerchantOperationResult> ExecuteAsync(
        Guid accountId,
        string merchantId,
        Func<Character, MerchantDefinition, Task<string?>> mutation,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                Character? character = await dbContext.Characters
                    .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
                if (character is null)
                    return MerchantOperationResult.Failure(MerchantErrorCodes.CharacterNotFound);

                MerchantDefinition? merchant = FindMerchant(merchantId);
                if (merchant is null)
                    return MerchantOperationResult.Failure(MerchantErrorCodes.MerchantNotFound);
                if (!await IsAtMerchantLocationAsync(character.Id, merchant, cancellationToken))
                    return MerchantOperationResult.Failure(MerchantErrorCodes.InvalidLocation);

                string? error = await mutation(character, merchant);
                if (error is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return MerchantOperationResult.Failure(error);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return MerchantOperationResult.Success(ToSnapshot(merchant, character.Gold));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MerchantOperationResult.Failure(MerchantErrorCodes.Conflict);
            }
        });
    }

    private async Task AddItemAsync(
        Guid characterId,
        ItemDefinition definition,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (!definition.Stackable)
        {
            for (var index = 0; index < quantity; index++)
                dbContext.CharacterItems.Add(new CharacterItem(
                    Guid.NewGuid(), characterId, definition.Id, 1, timeProvider.GetUtcNow()));
            return;
        }

        int remaining = quantity;
        CharacterItem[] stacks = await dbContext.CharacterItems
            .Where(item => item.CharacterId == characterId
                && item.ItemDefinitionId == definition.Id
                && item.Quantity < definition.MaxStack)
            .OrderBy(item => item.AcquiredAtUtc)
            .ToArrayAsync(cancellationToken);

        foreach (CharacterItem stack in stacks)
        {
            if (remaining <= 0) break;
            int toAdd = Math.Min(definition.MaxStack - stack.Quantity, remaining);
            if (toAdd <= 0) continue;
            stack.AddQuantity(toAdd, definition.MaxStack);
            remaining -= toAdd;
        }

        while (remaining > 0)
        {
            int stackSize = Math.Min(definition.MaxStack, remaining);
            dbContext.CharacterItems.Add(new CharacterItem(
                Guid.NewGuid(), characterId, definition.Id, stackSize, timeProvider.GetUtcNow()));
            remaining -= stackSize;
        }
    }

    private async Task<bool> IsAtMerchantLocationAsync(
        Guid characterId,
        MerchantDefinition merchant,
        CancellationToken cancellationToken) =>
        await dbContext.CharacterLocations.AsNoTracking().AnyAsync(
            location => location.CharacterId == characterId && location.LocationId == merchant.LocationId,
            cancellationToken);

    private MerchantDefinition? FindMerchant(string merchantId) =>
        (content.Merchants ?? []).SingleOrDefault(candidate =>
            string.Equals(candidate.Id, merchantId, StringComparison.Ordinal));

    private ItemDefinition? FindItem(string definitionId) =>
        (content.Items ?? []).SingleOrDefault(candidate =>
            string.Equals(candidate.Id, definitionId, StringComparison.Ordinal));

    private MerchantSnapshot ToSnapshot(MerchantDefinition merchant, long gold) =>
        new(
            merchant,
            gold,
            merchant.ItemIds
                .Select(id => FindItem(id))
                .Where(item => item is not null)
                .Select(item => new MerchantCatalogItem(item!, ResolveSellPrice(item!)))
                .ToArray());

    public static int ResolveSellPrice(ItemDefinition definition)
    {
        if (definition.SellPriceGold > 0) return definition.SellPriceGold;
        if (definition.Type != ItemType.Material) return 0;
        return definition.Rarity switch
        {
            ItemRarity.Common => 2,
            ItemRarity.Uncommon => 4,
            ItemRarity.Rare => 8,
            _ => 0
        };
    }
}
