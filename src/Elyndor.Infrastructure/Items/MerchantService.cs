using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

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
    public const string InvalidMutationId = "merchant_mutation_id_invalid";
    public const string MutationConflict = "merchant_mutation_conflict";
    public const string NotEnoughGold = "merchant_not_enough_gold";
    public const string Conflict = "merchant_conflict";
}

public sealed record MerchantCatalogItem(ItemDefinition Definition, int SellPriceGold);

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
    private const string BuyOperation = "MERCHANT_BUY";
    private const string SellMaterialOperation = "MERCHANT_SELL_MATERIAL";

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
        Guid mutationId,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            accountId,
            merchantId,
            mutationId,
            BuyOperation,
            Fingerprint(
                BuyOperation,
                merchantId,
                itemDefinitionId,
                quantity.ToString(CultureInfo.InvariantCulture)),
            async (character, merchant) =>
            {
                if (quantity < 1 || quantity > 20) return MerchantErrorCodes.InvalidQuantity;
                if (!merchant.ItemIds.Contains(itemDefinitionId, StringComparer.Ordinal))
                    return MerchantErrorCodes.ItemNotSold;

                ItemDefinition? definition = FindItem(itemDefinitionId);
                if (definition is null || definition.BuyPriceGold <= 0)
                    return MerchantErrorCodes.ItemNotSold;

                long totalPrice = checked((long)definition.BuyPriceGold * quantity);
                int affected = await dbContext.Characters
                    .Where(candidate => candidate.Id == character.Id && candidate.Gold >= totalPrice)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            candidate => candidate.Gold,
                            candidate => candidate.Gold - totalPrice),
                        cancellationToken);
                if (affected == 0) return MerchantErrorCodes.NotEnoughGold;

                await AddItemAsync(character.Id, definition, quantity, cancellationToken);
                return null;
            },
            cancellationToken);

    public Task<MerchantOperationResult> SellMaterialAsync(
        Guid accountId,
        string merchantId,
        Guid characterItemId,
        int quantity,
        Guid mutationId,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            accountId,
            merchantId,
            mutationId,
            SellMaterialOperation,
            Fingerprint(
                SellMaterialOperation,
                merchantId,
                characterItemId.ToString("N"),
                quantity.ToString(CultureInfo.InvariantCulture)),
            async (character, _) =>
            {
                if (quantity < 1 || quantity > 99) return MerchantErrorCodes.InvalidQuantity;

                CharacterItem? preview = await dbContext.CharacterItems
                    .AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.Id == characterItemId, cancellationToken);
                if (preview is null || preview.CharacterId != character.Id)
                    return MerchantErrorCodes.ItemNotOwned;

                ItemDefinition? definition = FindItem(preview.ItemDefinitionId);
                if (definition is null || definition.Type != ItemType.Material)
                    return MerchantErrorCodes.ItemNotSellable;
                if (quantity > preview.Quantity) return MerchantErrorCodes.InvalidQuantity;

                int unitPrice = ResolveSellPrice(definition);
                if (unitPrice <= 0) return MerchantErrorCodes.ItemNotSellable;
                long totalPrice = checked((long)unitPrice * quantity);

                int credited = await dbContext.Characters
                    .Where(candidate => candidate.Id == character.Id)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            candidate => candidate.Gold,
                            candidate => candidate.Gold + totalPrice),
                        cancellationToken);
                if (credited == 0) return MerchantErrorCodes.CharacterNotFound;

                CharacterItem? item = await dbContext.CharacterItems
                    .SingleOrDefaultAsync(candidate => candidate.Id == characterItemId, cancellationToken);
                if (item is null || item.CharacterId != character.Id)
                    return MerchantErrorCodes.ItemNotOwned;
                if (!string.Equals(item.ItemDefinitionId, definition.Id, StringComparison.Ordinal))
                    return MerchantErrorCodes.Conflict;
                if (quantity > item.Quantity) return MerchantErrorCodes.InvalidQuantity;

                item.RemoveQuantity(quantity);
                if (item.Quantity == 0) dbContext.CharacterItems.Remove(item);
                return null;
            },
            cancellationToken);

    private async Task<MerchantOperationResult> ExecuteMutationAsync(
        Guid accountId,
        string merchantId,
        Guid mutationId,
        string operationType,
        string requestFingerprint,
        Func<Character, MerchantDefinition, Task<string?>> mutation,
        CancellationToken cancellationToken)
    {
        if (mutationId == Guid.Empty)
            return MerchantOperationResult.Failure(MerchantErrorCodes.InvalidMutationId);

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                Character? character = await dbContext.Characters
                    .AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
                if (character is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return MerchantOperationResult.Failure(MerchantErrorCodes.CharacterNotFound);
                }

                CharacterMutation? existing = await dbContext.CharacterMutations
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        candidate => candidate.CharacterId == character.Id
                            && candidate.MutationId == mutationId,
                        cancellationToken);
                if (existing is not null)
                {
                    MerchantOperationResult replay = await ReplayAsync(
                        character, merchantId, existing, operationType, requestFingerprint, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return replay;
                }

                MerchantDefinition? merchant = FindMerchant(merchantId);
                if (merchant is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return MerchantOperationResult.Failure(MerchantErrorCodes.MerchantNotFound);
                }
                if (!await IsAtMerchantLocationAsync(character.Id, merchant, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return MerchantOperationResult.Failure(MerchantErrorCodes.InvalidLocation);
                }

                dbContext.CharacterMutations.Add(new CharacterMutation(
                    character.Id, mutationId, operationType, requestFingerprint, timeProvider.GetUtcNow()));
                await dbContext.SaveChangesAsync(cancellationToken);

                string? error = await mutation(character, merchant);
                if (error is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return MerchantOperationResult.Failure(error);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                long gold = await CurrentGoldAsync(character.Id, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return MerchantOperationResult.Success(ToSnapshot(merchant, gold));
            }
            catch (DbUpdateException exception) when (IsMutationConstraintViolation(exception))
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return await ResolveReplayAsync(
                    accountId, merchantId, mutationId, operationType, requestFingerprint, cancellationToken);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return MerchantOperationResult.Failure(MerchantErrorCodes.Conflict);
            }
        });
    }

    private async Task<MerchantOperationResult> ResolveReplayAsync(
        Guid accountId,
        string merchantId,
        Guid mutationId,
        string operationType,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null) return MerchantOperationResult.Failure(MerchantErrorCodes.CharacterNotFound);

        CharacterMutation? existing = await dbContext.CharacterMutations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CharacterId == character.Id && candidate.MutationId == mutationId,
                cancellationToken);
        if (existing is null) return MerchantOperationResult.Failure(MerchantErrorCodes.Conflict);

        return await ReplayAsync(
            character, merchantId, existing, operationType, requestFingerprint, cancellationToken);
    }

    private async Task<MerchantOperationResult> ReplayAsync(
        Character character,
        string merchantId,
        CharacterMutation existing,
        string operationType,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existing.OperationType, operationType, StringComparison.Ordinal)
            || !string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
        {
            return MerchantOperationResult.Failure(MerchantErrorCodes.MutationConflict);
        }

        MerchantDefinition? merchant = FindMerchant(merchantId);
        if (merchant is null) return MerchantOperationResult.Failure(MerchantErrorCodes.MerchantNotFound);

        long gold = await CurrentGoldAsync(character.Id, cancellationToken);
        return MerchantOperationResult.Success(ToSnapshot(merchant, gold));
    }

    private Task<long> CurrentGoldAsync(Guid characterId, CancellationToken cancellationToken) =>
        dbContext.Characters.AsNoTracking()
            .Where(candidate => candidate.Id == characterId)
            .Select(candidate => candidate.Gold)
            .SingleAsync(cancellationToken);

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
            merchant.ItemIds.Select(id => FindItem(id))
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

    private static string Fingerprint(params string[] parts)
    {
        string canonical = string.Join(
            "\u001F",
            parts.Select(part => $"{part.Length.ToString(CultureInfo.InvariantCulture)}:{part}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static bool IsMutationConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "pk_character_mutations"
        };
}
