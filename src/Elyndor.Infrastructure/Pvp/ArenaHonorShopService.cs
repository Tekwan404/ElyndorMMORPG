using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Pvp;

public static class ArenaHonorShopErrorCodes
{
    public const string CharacterNotFound = "arena_shop_character_not_found";
    public const string ItemNotFound = "arena_shop_item_not_found";
    public const string ItemNotForSale = "arena_shop_item_not_for_sale";
    public const string ClassRestricted = "arena_shop_class_restricted";
    public const string RequiredLevel = "arena_shop_required_level";
    public const string NotEnoughHonor = "arena_shop_not_enough_honor";
    public const string InventoryFull = "arena_shop_inventory_full";
    public const string InvalidMutationId = "arena_shop_mutation_id_invalid";
    public const string MutationConflict = "arena_shop_mutation_conflict";
}

public sealed record ArenaHonorShopSnapshot(
    long Honor,
    IReadOnlyList<ItemDefinition> Items);

public sealed record ArenaHonorShopOperationResult(
    bool Succeeded,
    string? ErrorCode,
    ArenaHonorShopSnapshot? Snapshot)
{
    public static ArenaHonorShopOperationResult Success(ArenaHonorShopSnapshot snapshot) =>
        new(true, null, snapshot);

    public static ArenaHonorShopOperationResult Failure(string errorCode) =>
        new(false, errorCode, null);
}

public sealed class ArenaHonorShopService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    private const string BuyOperation = "ARENA_HONOR_SHOP_BUY";

    public async Task<ArenaHonorShopOperationResult> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null)
            return ArenaHonorShopOperationResult.Failure(ArenaHonorShopErrorCodes.CharacterNotFound);

        return ArenaHonorShopOperationResult.Success(
            await BuildSnapshotAsync(character, cancellationToken));
    }

    public async Task<ArenaHonorShopOperationResult> BuyAsync(
        Guid accountId,
        string itemDefinitionId,
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        if (mutationId == Guid.Empty)
            return ArenaHonorShopOperationResult.Failure(ArenaHonorShopErrorCodes.InvalidMutationId);

        string fingerprint = Fingerprint(BuyOperation, itemDefinitionId);
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);

            Character? character = await dbContext.Characters
                .FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (character is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ArenaHonorShopOperationResult.Failure(ArenaHonorShopErrorCodes.CharacterNotFound);
            }

            CharacterMutation? existing = await dbContext.CharacterMutations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.CharacterId == character.Id
                        && candidate.MutationId == mutationId,
                    cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.OperationType, BuyOperation, StringComparison.Ordinal)
                    || !string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ArenaHonorShopOperationResult.Failure(ArenaHonorShopErrorCodes.MutationConflict);
                }

                ArenaHonorShopSnapshot replay = await BuildSnapshotAsync(character, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ArenaHonorShopOperationResult.Success(replay);
            }

            GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
            if (!contentSnapshot.Indexes.ItemsById.TryGetValue(itemDefinitionId, out ItemDefinition? definition))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ArenaHonorShopOperationResult.Failure(ArenaHonorShopErrorCodes.ItemNotFound);
            }
            if (definition.Type != ItemType.Equipment || definition.HonorPrice <= 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ArenaHonorShopOperationResult.Failure(ArenaHonorShopErrorCodes.ItemNotForSale);
            }
            if (definition.AllowedClassIds is { Count: > 0 }
                && !definition.AllowedClassIds.Contains(character.ClassId, StringComparer.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ArenaHonorShopOperationResult.Failure(ArenaHonorShopErrorCodes.ClassRestricted);
            }
            if (character.Level < definition.RequiredLevel)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ArenaHonorShopOperationResult.Failure(ArenaHonorShopErrorCodes.RequiredLevel);
            }
            if (!await InventoryCapacity.CanAddAsync(
                    dbContext,
                    character.Id,
                    definition,
                    1,
                    contentSnapshot,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ArenaHonorShopOperationResult.Failure(ArenaHonorShopErrorCodes.InventoryFull);
            }

            ArenaHonorWallet? wallet = await dbContext.ArenaHonorWallets
                .FromSqlInterpolated(
                    $"SELECT * FROM game.arena_honor_wallets WHERE \"CharacterId\" = {character.Id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (wallet is null || !wallet.TrySpend(definition.HonorPrice))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ArenaHonorShopOperationResult.Failure(ArenaHonorShopErrorCodes.NotEnoughHonor);
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            CharacterItem item = ItemInstancePersistenceFactory.CreateCharacterItem(
                character.Id,
                definition,
                mutationId,
                "ARENA_HONOR_SHOP",
                definition.Id,
                0,
                now,
                contentSnapshot.Package);
            dbContext.CharacterItems.Add(item);
            dbContext.CharacterMutations.Add(new CharacterMutation(
                character.Id,
                mutationId,
                BuyOperation,
                fingerprint,
                now));

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(CancellationToken.None);

            return ArenaHonorShopOperationResult.Success(
                await BuildSnapshotAsync(character, cancellationToken));
        });
    }

    private async Task<ArenaHonorShopSnapshot> BuildSnapshotAsync(
        Character character,
        CancellationToken cancellationToken)
    {
        long honor = await dbContext.ArenaHonorWallets
            .AsNoTracking()
            .Where(wallet => wallet.CharacterId == character.Id)
            .Select(wallet => (long?)wallet.Balance)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;

        ItemDefinition[] items = contentProvider.GetCurrent().Package.Items!
            .Where(item => item.Type == ItemType.Equipment
                && item.HonorPrice > 0
                && (item.AllowedClassIds is not { Count: > 0 }
                    || item.AllowedClassIds.Contains(character.ClassId, StringComparer.Ordinal)))
            .OrderBy(item => item.RequiredLevel)
            .ThenBy(item => item.HonorPrice)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();

        return new ArenaHonorShopSnapshot(honor, items);
    }

    private static string Fingerprint(params string[] parts) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('|', parts))));
}
