using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Elyndor.Infrastructure.Items;

public static class ItemSalvageErrorCodes
{
    public const string CharacterNotFound = "salvage_character_not_found";
    public const string ItemNotFound = "salvage_item_not_found";
    public const string ItemLocked = "salvage_item_locked";
    public const string ItemEquipped = "salvage_item_equipped";
    public const string ItemTransactionLocked = "salvage_item_transaction_locked";
    public const string ItemNotEquipment = "salvage_item_not_equipment";
    public const string ConfirmationRequired = "salvage_confirmation_required";
    public const string ProfileMissing = "salvage_profile_missing";
    public const string InventoryFull = "salvage_inventory_full";
    public const string InvalidMutationId = "salvage_mutation_id_invalid";
    public const string MutationConflict = "salvage_mutation_conflict";
    public const string Conflict = "salvage_conflict";
}

public sealed record ItemSalvageOperationResult(
    bool Succeeded,
    string? ErrorCode,
    ItemSalvageYield? Reward)
{
    public static ItemSalvageOperationResult Failure(string errorCode) => new(false, errorCode, null);
}

public sealed record ItemSalvagePreviewResult(
    bool Succeeded,
    string? ErrorCode,
    ItemSalvageYield? Reward,
    bool RequiresConfirmation)
{
    public static ItemSalvagePreviewResult Failure(string errorCode) => new(false, errorCode, null, false);
}

public sealed class ItemSalvageService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    private const string OperationType = "ITEM_SALVAGE";

    public ItemSalvageService(GameDbContext dbContext, GameContentPackage content, TimeProvider timeProvider)
        : this(dbContext, new StaticContentSnapshotProvider(content), timeProvider) { }

    public Task<ItemSalvageOperationResult> SalvageAsync(
        Guid accountId,
        Guid itemInstanceId,
        Guid mutationId,
        bool confirmedHighValue,
        CancellationToken cancellationToken)
    {
        if (mutationId == Guid.Empty)
            return Task.FromResult(ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.InvalidMutationId));
        return dbContext.Database.CreateExecutionStrategy().ExecuteAsync(() =>
            SalvageCoreAsync(accountId, itemInstanceId, mutationId, confirmedHighValue, cancellationToken));
    }

    public async Task<ItemSalvagePreviewResult> GetPreviewAsync(
        Guid accountId,
        Guid itemInstanceId,
        CancellationToken cancellationToken)
    {
        GameContentSnapshot content = contentProvider.GetCurrent();
        ItemSalvageProfileDefinition? profile = content.Package.Itemization?.Salvage;
        if (profile is null) return ItemSalvagePreviewResult.Failure(ItemSalvageErrorCodes.ProfileMissing);

        Character? character = await dbContext.Characters.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null) return ItemSalvagePreviewResult.Failure(ItemSalvageErrorCodes.CharacterNotFound);

        CharacterItem? item = await dbContext.CharacterItems.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == itemInstanceId && candidate.CharacterId == character.Id, cancellationToken);
        if (item is null) return ItemSalvagePreviewResult.Failure(ItemSalvageErrorCodes.ItemNotFound);
        if (item.IsLocked) return ItemSalvagePreviewResult.Failure(ItemSalvageErrorCodes.ItemLocked);
        if (item.TransactionLockId.HasValue) return ItemSalvagePreviewResult.Failure(ItemSalvageErrorCodes.ItemTransactionLocked);
        if (await dbContext.CharacterEquipment.AnyAsync(equipment =>
                equipment.CharacterId == character.Id && equipment.CharacterItemId == item.Id, cancellationToken))
            return ItemSalvagePreviewResult.Failure(ItemSalvageErrorCodes.ItemEquipped);

        ItemDefinition? definition = content.Indexes.ItemsById.GetValueOrDefault(item.ItemDefinitionId);
        if (definition?.Type != ItemType.Equipment)
            return ItemSalvagePreviewResult.Failure(ItemSalvageErrorCodes.ItemNotEquipment);

        ItemSalvageYield reward = ItemSalvageYieldCalculator.Calculate(
            definition, item.ItemLevel, item.Stars, profile);
        return new ItemSalvagePreviewResult(
            true,
            null,
            reward,
            RequiresConfirmation(definition.Rarity, profile.HighValueConfirmationRarity));
    }

    private async Task<ItemSalvageOperationResult> SalvageCoreAsync(
        Guid accountId, Guid itemInstanceId, Guid mutationId, bool confirmedHighValue, CancellationToken cancellationToken)
    {
        GameContentSnapshot content = contentProvider.GetCurrent();
        ItemSalvageProfileDefinition? profile = content.Package.Itemization?.Salvage;
        if (profile is null) return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.ProfileMissing);
        string fingerprint = Fingerprint(itemInstanceId, confirmedHighValue);
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            Character? character = await dbContext.Characters.FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (character is null) return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.CharacterNotFound);

            CharacterMutation? replay = await dbContext.CharacterMutations.AsNoTracking().SingleOrDefaultAsync(
                mutation => mutation.CharacterId == character.Id && mutation.MutationId == mutationId, cancellationToken);
            if (replay is not null)
            {
                if (replay.OperationType != OperationType || replay.RequestFingerprint != fingerprint)
                    return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.MutationConflict);
                ItemSalvageOperation receipt = await dbContext.ItemSalvageOperations.AsNoTracking()
                    .SingleAsync(operation => operation.OperationId == mutationId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new ItemSalvageOperationResult(true, null, ToYield(receipt));
            }

            CharacterItem? item = await dbContext.CharacterItems.SingleOrDefaultAsync(
                candidate => candidate.Id == itemInstanceId && candidate.CharacterId == character.Id, cancellationToken);
            if (item is null) return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.ItemNotFound);
            if (item.IsLocked) return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.ItemLocked);
            if (item.TransactionLockId.HasValue) return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.ItemTransactionLocked);
            if (await dbContext.CharacterEquipment.AnyAsync(equipment =>
                    equipment.CharacterId == character.Id && equipment.CharacterItemId == item.Id, cancellationToken))
                return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.ItemEquipped);

            ItemDefinition? definition = content.Indexes.ItemsById.GetValueOrDefault(item.ItemDefinitionId);
            if (definition?.Type != ItemType.Equipment)
                return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.ItemNotEquipment);
            if (RequiresConfirmation(definition.Rarity, profile.HighValueConfirmationRarity) && !confirmedHighValue)
                return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.ConfirmationRequired);

            ItemSalvageYield reward = ItemSalvageYieldCalculator.Calculate(
                definition, item.ItemLevel, item.Stars, profile);
            ItemDefinition? stone = content.Indexes.ItemsById.GetValueOrDefault(reward.ReforgeStoneItemId);
            ItemDefinition? material = content.Indexes.ItemsById.GetValueOrDefault(reward.MaterialItemId);
            if (stone?.Type != ItemType.Material || material?.Type != ItemType.Material)
                return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.ProfileMissing);
            if (!await CanGrantRewardsAsync(character.Id, stone, material, reward, content, cancellationToken))
                return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.InventoryFull);

            dbContext.CharacterMutations.Add(new CharacterMutation(
                character.Id, mutationId, OperationType, fingerprint, timeProvider.GetUtcNow()));
            dbContext.CharacterItems.Remove(item);
            await GrantAsync(character.Id, stone, reward.ReforgeStoneQuantity, cancellationToken);
            await GrantAsync(character.Id, material, reward.MaterialQuantity, cancellationToken);
            dbContext.ItemSalvageOperations.Add(new ItemSalvageOperation(
                mutationId, character.Id, item.Id, definition.Id,
                reward.ReforgeStoneItemId, reward.ReforgeStoneQuantity,
                reward.MaterialItemId, reward.MaterialQuantity, timeProvider.GetUtcNow()));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ItemSalvageOperationResult(true, null, reward);
        }
        catch (DbUpdateException exception) when (IsMutationConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return await ResolveReplayAsync(accountId, mutationId, fingerprint, cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.Conflict);
        }
    }

    private async Task<ItemSalvageOperationResult> ResolveReplayAsync(
        Guid accountId,
        Guid mutationId,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null) return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.CharacterNotFound);

        CharacterMutation? replay = await dbContext.CharacterMutations.AsNoTracking().SingleOrDefaultAsync(
            mutation => mutation.CharacterId == character.Id && mutation.MutationId == mutationId, cancellationToken);
        if (replay is null) return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.Conflict);
        if (replay.OperationType != OperationType || replay.RequestFingerprint != fingerprint)
            return ItemSalvageOperationResult.Failure(ItemSalvageErrorCodes.MutationConflict);

        ItemSalvageOperation receipt = await dbContext.ItemSalvageOperations.AsNoTracking()
            .SingleAsync(operation => operation.OperationId == mutationId, cancellationToken);
        return new ItemSalvageOperationResult(true, null, ToYield(receipt));
    }

    private async Task<bool> CanGrantRewardsAsync(Guid characterId, ItemDefinition stone,
        ItemDefinition material, ItemSalvageYield reward, GameContentSnapshot content, CancellationToken cancellationToken)
    {
        int used = await InventoryCapacity.CountUsedSlotsAsync(dbContext, characterId, cancellationToken) - 1;
        int required = await InventoryCapacity.AdditionalSlotsRequiredAsync(dbContext, characterId, stone, reward.ReforgeStoneQuantity, cancellationToken)
            + await InventoryCapacity.AdditionalSlotsRequiredAsync(dbContext, characterId, material, reward.MaterialQuantity, cancellationToken);
        return used + required <= InventoryCapacity.Resolve(content);
    }

    private async Task GrantAsync(Guid characterId, ItemDefinition definition, int quantity, CancellationToken cancellationToken)
    {
        if (quantity <= 0) return;
        CharacterItem[] stacks = await dbContext.CharacterItems.Where(item => item.CharacterId == characterId
            && item.ItemDefinitionId == definition.Id && item.DefinitionVersion == definition.Version
            && item.Quantity < definition.MaxStack).OrderBy(item => item.AcquiredAtUtc).ToArrayAsync(cancellationToken);
        int remaining = quantity;
        foreach (CharacterItem stack in stacks)
        {
            int added = Math.Min(definition.MaxStack - stack.Quantity, remaining);
            stack.AddQuantity(added, definition.MaxStack);
            remaining -= added;
            if (remaining == 0) return;
        }
        while (remaining > 0)
        {
            int size = Math.Min(definition.MaxStack, remaining);
            dbContext.CharacterItems.Add(new CharacterItem(Guid.CreateVersion7(), characterId, definition.Id, size,
                timeProvider.GetUtcNow(), definition.Version));
            remaining -= size;
        }
    }

    private static bool RequiresConfirmation(ItemRarity rarity, string threshold) =>
        RarityRank(rarity) >= RarityRank(Enum.Parse<ItemRarity>(threshold, true));

    private static int RarityRank(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Common => 1,
        ItemRarity.Uncommon => 2,
        ItemRarity.Rare => 3,
        ItemRarity.Epic => 4,
        ItemRarity.Legendary => 5,
        ItemRarity.Unique => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown item rarity.")
    };

    private static ItemSalvageYield ToYield(ItemSalvageOperation operation) => new(
        operation.ReforgeStoneItemId, operation.ReforgeStoneQuantity,
        operation.MaterialItemId, operation.MaterialQuantity);

    private static string Fingerprint(Guid itemInstanceId, bool confirmedHighValue) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes($"{OperationType}|{itemInstanceId:N}|{confirmedHighValue}")));

    private static bool IsMutationConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "pk_character_mutations"
        };
}
