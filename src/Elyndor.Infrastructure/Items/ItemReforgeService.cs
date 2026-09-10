using System.Text.Json;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Items;

public static class ItemReforgeErrorCodes
{
    public const string CharacterNotFound = "reforge_character_not_found";
    public const string ItemNotFound = "reforge_item_not_found";
    public const string ItemNotGenerated = "reforge_item_not_generated";
    public const string ItemLocked = "reforge_item_locked";
    public const string ItemEquipped = "reforge_item_equipped";
    public const string ItemTransactionLocked = "reforge_item_transaction_locked";
    public const string InvalidSlot = "reforge_invalid_affix_slot";
    public const string GuaranteedSlot = "reforge_guaranteed_affix_immutable";
    public const string CostProfileMissing = "reforge_cost_profile_missing";
    public const string NotEnoughGold = "reforge_not_enough_gold";
    public const string NotEnoughMaterial = "reforge_not_enough_material";
    public const string NotEnoughCatalyst = "reforge_not_enough_catalyst";
    public const string OperationConflict = "reforge_operation_conflict";
    public const string ProposalNotFound = "reforge_proposal_not_found";
    public const string ProposalNotPending = "reforge_proposal_not_pending";
}

public sealed record ItemReforgeCost(
    int Gold,
    string MaterialItemId,
    int MaterialQuantity,
    string CatalystItemId,
    int CatalystQuantity,
    decimal CountMultiplier);

public sealed record ItemReforgeOperationResult(
    bool Succeeded,
    string? ErrorCode,
    ItemReforgeOperation? Operation,
    GeneratedItemInstance? Current,
    GeneratedItemInstance? Proposed,
    ItemReforgeCost? Cost)
{
    public static ItemReforgeOperationResult Failure(string code) =>
        new(false, code, null, null, null, null);
}

public sealed record ItemReforgePreviewResult(
    bool Succeeded,
    string? ErrorCode,
    GeneratedItemInstance? Current,
    ItemReforgeCost? Cost)
{
    public static ItemReforgePreviewResult Failure(string code) => new(false, code, null, null);
}

public sealed class ItemReforgeService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    public async Task<ItemReforgePreviewResult> GetPreviewAsync(
        Guid accountId,
        Guid itemInstanceId,
        string slotKey,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null) return ItemReforgePreviewResult.Failure(ItemReforgeErrorCodes.CharacterNotFound);

        CharacterItem? item = await dbContext.CharacterItems
            .AsNoTracking()
            .Include(candidate => candidate.Affixes)
            .SingleOrDefaultAsync(candidate => candidate.Id == itemInstanceId && candidate.CharacterId == character.Id, cancellationToken);
        if (item is null) return ItemReforgePreviewResult.Failure(ItemReforgeErrorCodes.ItemNotFound);
        if (item.IsLocked) return ItemReforgePreviewResult.Failure(ItemReforgeErrorCodes.ItemLocked);
        if (item.TransactionLockId.HasValue) return ItemReforgePreviewResult.Failure(ItemReforgeErrorCodes.ItemTransactionLocked);
        if (await dbContext.CharacterEquipment.AsNoTracking().AnyAsync(candidate => candidate.CharacterItemId == item.Id, cancellationToken))
            return ItemReforgePreviewResult.Failure(ItemReforgeErrorCodes.ItemEquipped);

        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        if (!contentSnapshot.Indexes.ItemsById.TryGetValue(item.ItemDefinitionId, out ItemDefinition? definition))
            return ItemReforgePreviewResult.Failure(ItemReforgeErrorCodes.ItemNotFound);
        GeneratedItemInstance? current = ItemInstancePersistenceFactory.ToGeneratedInstance(item, definition);
        if (current is null || contentSnapshot.Package.Itemization is not { } itemization)
            return ItemReforgePreviewResult.Failure(ItemReforgeErrorCodes.ItemNotGenerated);

        GeneratedItemAffix? selected = current.Affixes.SingleOrDefault(affix =>
            string.Equals(affix.SlotKey, slotKey, StringComparison.Ordinal));
        if (selected is null || (item.ReforgeSlotKey is not null && !string.Equals(item.ReforgeSlotKey, slotKey, StringComparison.Ordinal)))
            return ItemReforgePreviewResult.Failure(ItemReforgeErrorCodes.InvalidSlot);
        if (selected.IsGuaranteed) return ItemReforgePreviewResult.Failure(ItemReforgeErrorCodes.GuaranteedSlot);
        if (itemization.ReforgeCosts is not { } costProfile)
            return ItemReforgePreviewResult.Failure(ItemReforgeErrorCodes.CostProfileMissing);

        return new ItemReforgePreviewResult(true, null, current, ResolveCost(costProfile, definition.Rarity, item.ReforgeCount));
    }

    public async Task<ItemReforgeOperationResult?> GetPendingAsync(
        Guid accountId,
        Guid? itemInstanceId,
        CancellationToken cancellationToken)
    {
        Guid? characterId = await dbContext.Characters
            .AsNoTracking()
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!characterId.HasValue)
            return null;

        IQueryable<ItemReforgeOperation> query = dbContext.ItemReforgeOperations
            .AsNoTracking()
            .Where(operation => operation.CharacterId == characterId.Value
                && operation.State == ItemReforgeOperationState.Pending);
        if (itemInstanceId.HasValue)
            query = query.Where(operation => operation.ItemInstanceId == itemInstanceId.Value);

        ItemReforgeOperation? operation = await query
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return operation is null ? null : ToResult(operation);
    }

    public Task<ItemReforgeOperationResult> RollAsync(
        Guid accountId,
        Guid itemInstanceId,
        string slotKey,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
            return Task.FromResult(ItemReforgeOperationResult.Failure(ItemReforgeErrorCodes.OperationConflict));
        return dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => RollCoreAsync(
                accountId,
                itemInstanceId,
                slotKey,
                operationId,
                cancellationToken));
    }

    public Task<ItemReforgeOperationResult> DecideAsync(
        Guid accountId,
        Guid operationId,
        bool acceptProposed,
        CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
            return Task.FromResult(ItemReforgeOperationResult.Failure(ItemReforgeErrorCodes.ProposalNotFound));
        return dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => DecideCoreAsync(
                accountId,
                operationId,
                acceptProposed,
                cancellationToken));
    }

    private async Task<ItemReforgeOperationResult> RollCoreAsync(
        Guid accountId,
        Guid itemInstanceId,
        string slotKey,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        Character? character = await dbContext.Characters
            .FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (character is null)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.CharacterNotFound, cancellationToken);

        ItemReforgeOperation? replay = await dbContext.ItemReforgeOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.OperationId == operationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.CharacterId != character.Id
                || replay.ItemInstanceId != itemInstanceId
                || !string.Equals(replay.SlotKey, slotKey, StringComparison.Ordinal))
            {
                return await RollbackFailureAsync(
                    transaction,
                    ItemReforgeErrorCodes.OperationConflict,
                    cancellationToken);
            }

            ItemReforgeOperationResult replayResult = ToResult(replay);
            await transaction.CommitAsync(cancellationToken);
            return replayResult;
        }

        CharacterItem? item = await dbContext.CharacterItems
            .Include(candidate => candidate.Affixes)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == itemInstanceId
                    && candidate.CharacterId == character.Id,
                cancellationToken);
        if (item is null)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.ItemNotFound, cancellationToken);

        if (item.TransactionLockId.HasValue)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.ItemTransactionLocked, cancellationToken);
        if (item.IsLocked)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.ItemLocked, cancellationToken);

        bool equipped = await dbContext.CharacterEquipment
            .AsNoTracking()
            .AnyAsync(candidate => candidate.CharacterItemId == item.Id, cancellationToken);
        if (equipped)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.ItemEquipped, cancellationToken);

        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        if (!contentSnapshot.Indexes.ItemsById.TryGetValue(
                item.ItemDefinitionId,
                out ItemDefinition? definition))
        {
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.ItemNotFound, cancellationToken);
        }

        GeneratedItemInstance? current =
            ItemInstancePersistenceFactory.ToGeneratedInstance(item, definition);
        if (current is null || contentSnapshot.Package.Itemization is not { } itemization)
        {
            return await RollbackFailureAsync(
                transaction,
                ItemReforgeErrorCodes.ItemNotGenerated,
                cancellationToken);
        }

        GeneratedItemAffix? selected = current.Affixes.SingleOrDefault(affix =>
            string.Equals(affix.SlotKey, slotKey, StringComparison.Ordinal));
        if (selected is null)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.InvalidSlot, cancellationToken);
        if (selected.IsGuaranteed)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.GuaranteedSlot, cancellationToken);
        if (item.ReforgeSlotKey is not null
            && !string.Equals(item.ReforgeSlotKey, slotKey, StringComparison.Ordinal))
        {
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.InvalidSlot, cancellationToken);
        }

        ItemReforgeCostProfileDefinition? costProfile = itemization.ReforgeCosts;
        if (costProfile is null)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.CostProfileMissing, cancellationToken);

        ItemReforgeCost cost = ResolveCost(costProfile, definition.Rarity, item.ReforgeCount);
        if (character.Gold < cost.Gold)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.NotEnoughGold, cancellationToken);
        if (!await HasMaterialAsync(character.Id, cost.MaterialItemId, cost.MaterialQuantity, cancellationToken))
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.NotEnoughMaterial, cancellationToken);
        if (!await HasMaterialAsync(character.Id, cost.CatalystItemId, cost.CatalystQuantity, cancellationToken))
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.NotEnoughCatalyst, cancellationToken);

        ItemGenerationKey key = ItemGenerationKey.Create(
            operationId,
            $"{item.Id:N}|{slotKey}|REFORGE",
            item.ReforgeCount);
        GeneratedItemAffix proposedAffix = ItemInstanceGenerator.RollReforgeAffix(
            definition,
            itemization,
            current.Affixes,
            slotKey,
            "REFORGE",
            new SeededGameRandom(key.Seed));
        GeneratedItemAffix[] proposedAffixes = current.Affixes
            .Select(affix => string.Equals(affix.SlotKey, slotKey, StringComparison.Ordinal)
                ? proposedAffix
                : affix)
            .ToArray();
        GeneratedItemInstance proposed = ItemInstanceGenerator.Recalculate(
            definition,
            itemization,
            current.ItemLevel,
            proposedAffixes,
            perfectOrigin: "REFORGE");

        if (!character.TrySpendGold(cost.Gold))
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.NotEnoughGold, cancellationToken);
        await ConsumeMaterialAsync(
            character.Id,
            cost.MaterialItemId,
            cost.MaterialQuantity,
            cancellationToken);
        await ConsumeMaterialAsync(
            character.Id,
            cost.CatalystItemId,
            cost.CatalystQuantity,
            cancellationToken);

        item.SelectReforgeSlot(slotKey);
        item.RecordReforgeAttempt();
        item.AcquireTransactionLock(operationId);

        GeneratedItemInstance lockedCurrent =
            ItemInstancePersistenceFactory.ToGeneratedInstance(item, definition)
            ?? current;
        ItemReforgeOperation operation = new(
            operationId,
            character.Id,
            item.Id,
            slotKey,
            cost.Gold,
            cost.MaterialItemId,
            cost.MaterialQuantity,
            cost.CatalystItemId,
            cost.CatalystQuantity,
            JsonSerializer.Serialize(lockedCurrent),
            JsonSerializer.Serialize(proposed),
            timeProvider.GetUtcNow());
        dbContext.ItemReforgeOperations.Add(operation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ItemReforgeOperationResult(
            true,
            null,
            operation,
            lockedCurrent,
            proposed,
            cost);
    }

    private async Task<ItemReforgeOperationResult> DecideCoreAsync(
        Guid accountId,
        Guid operationId,
        bool acceptProposed,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        Character? character = await dbContext.Characters
            .FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (character is null)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.CharacterNotFound, cancellationToken);

        ItemReforgeOperation? operation = await dbContext.ItemReforgeOperations
            .SingleOrDefaultAsync(
                candidate => candidate.OperationId == operationId
                    && candidate.CharacterId == character.Id,
                cancellationToken);
        if (operation is null)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.ProposalNotFound, cancellationToken);

        if (operation.State != ItemReforgeOperationState.Pending)
        {
            ItemReforgeOperationResult replay = ToResult(operation);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        CharacterItem? item = await dbContext.CharacterItems
            .Include(candidate => candidate.Affixes)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == operation.ItemInstanceId
                    && candidate.CharacterId == character.Id,
                cancellationToken);
        if (item is null)
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.ItemNotFound, cancellationToken);
        if (item.TransactionLockId != operationId)
            return await RollbackFailureAsync(
                transaction,
                ItemReforgeErrorCodes.ItemTransactionLocked,
                cancellationToken);

        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        if (!contentSnapshot.Indexes.ItemsById.TryGetValue(item.ItemDefinitionId, out ItemDefinition? definition))
            return await RollbackFailureAsync(transaction, ItemReforgeErrorCodes.ItemNotFound, cancellationToken);

        GeneratedItemInstance current =
            JsonSerializer.Deserialize<GeneratedItemInstance>(operation.CurrentItemJson)
            ?? throw new InvalidDataException("Stored Reforge current item payload is invalid.");
        GeneratedItemInstance proposed =
            JsonSerializer.Deserialize<GeneratedItemInstance>(operation.ProposedItemJson)
            ?? throw new InvalidDataException("Stored Reforge proposed item payload is invalid.");

        if (acceptProposed)
        {
            GeneratedItemAffix proposedAffix = proposed.Affixes.Single(affix =>
                string.Equals(affix.SlotKey, operation.SlotKey, StringComparison.Ordinal));
            item.ApplyReforge(
                proposedAffix,
                proposed.MinimumTemplateItemPower,
                proposed.ActualItemPower,
                proposed.MaxTemplateItemPower,
                proposed.RollQuality,
                proposed.Stars,
                proposed.IsPerfect,
                proposed.PerfectOrigin,
                proposed.GeneratedPrefixId,
                proposed.GeneratedSuffixId,
                proposed.DisplayName);
        }

        item.ReleaseTransactionLock(operationId);
        operation.Decide(acceptProposed, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        GeneratedItemInstance final = acceptProposed
            ? ItemInstancePersistenceFactory.ToGeneratedInstance(item, definition) ?? proposed
            : ItemInstancePersistenceFactory.ToGeneratedInstance(item, definition) ?? current;
        return new ItemReforgeOperationResult(
            true,
            null,
            operation,
            final,
            proposed,
            new ItemReforgeCost(
                operation.CostGold,
                operation.MaterialItemId,
                operation.MaterialQuantity,
                operation.CatalystItemId,
                operation.CatalystQuantity,
                0));
    }

    private static ItemReforgeCost ResolveCost(
        ItemReforgeCostProfileDefinition profile,
        ItemRarity rarity,
        int reforgeCount)
    {
        string key = rarity.ToString().ToUpperInvariant();
        if (!profile.BaseGoldByRarity.TryGetValue(key, out int baseGold)
            || !profile.MaterialQuantityByRarity.TryGetValue(key, out int materialQuantity)
            || !profile.CatalystQuantityByRarity.TryGetValue(key, out int catalystQuantity))
        {
            throw new InvalidOperationException($"Reforge cost profile '{profile.Id}' is missing rarity '{key}'.");
        }

        decimal multiplier = ResolveCountMultiplier(profile, reforgeCount);
        return new ItemReforgeCost(
            checked((int)decimal.Ceiling(baseGold * multiplier)),
            profile.MaterialItemId,
            checked((int)decimal.Ceiling(materialQuantity * multiplier)),
            profile.CatalystItemId,
            checked((int)decimal.Ceiling(catalystQuantity * multiplier)),
            multiplier);
    }

    private static decimal ResolveCountMultiplier(
        ItemReforgeCostProfileDefinition profile,
        int reforgeCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reforgeCount);
        if (profile.ReforgeCountMultipliers.Count == 0)
            throw new InvalidOperationException("Reforge cost profile has no count multipliers.");
        if (reforgeCount < profile.ReforgeCountMultipliers.Count)
            return profile.ReforgeCountMultipliers[reforgeCount];

        int overflowAttempts = reforgeCount - profile.ReforgeCountMultipliers.Count + 1;
        decimal last = profile.ReforgeCountMultipliers[^1];
        return last * (decimal)Math.Pow(
            (double)profile.OverflowGrowthMultiplier,
            overflowAttempts);
    }

    private async Task<bool> HasMaterialAsync(
        Guid characterId,
        string itemDefinitionId,
        int requiredQuantity,
        CancellationToken cancellationToken)
    {
        if (requiredQuantity <= 0)
            return true;
        int available = await dbContext.CharacterItems
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId
                && item.ItemDefinitionId == itemDefinitionId
                && !item.IsLocked
                && item.TransactionLockId == null)
            .SumAsync(item => item.Quantity, cancellationToken);
        return available >= requiredQuantity;
    }

    private async Task ConsumeMaterialAsync(
        Guid characterId,
        string itemDefinitionId,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
            return;

        CharacterItem[] stacks = await dbContext.CharacterItems
            .Where(item => item.CharacterId == characterId
                && item.ItemDefinitionId == itemDefinitionId
                && !item.IsLocked
                && item.TransactionLockId == null)
            .OrderBy(item => item.AcquiredAtUtc)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        int remaining = quantity;
        foreach (CharacterItem stack in stacks)
        {
            if (remaining <= 0)
                break;
            int take = Math.Min(remaining, stack.Quantity);
            stack.RemoveQuantity(take);
            remaining -= take;
            if (stack.Quantity == 0)
                dbContext.CharacterItems.Remove(stack);
        }

        if (remaining != 0)
            throw new InvalidOperationException("Reforge material availability changed inside the transaction.");
    }

    private static ItemReforgeOperationResult ToResult(ItemReforgeOperation operation)
    {
        GeneratedItemInstance? original =
            JsonSerializer.Deserialize<GeneratedItemInstance>(operation.CurrentItemJson);
        GeneratedItemInstance? proposed =
            JsonSerializer.Deserialize<GeneratedItemInstance>(operation.ProposedItemJson);
        GeneratedItemInstance? current = operation.State == ItemReforgeOperationState.Accepted
            ? proposed
            : original;
        return new ItemReforgeOperationResult(
            true,
            null,
            operation,
            current,
            proposed,
            new ItemReforgeCost(
                operation.CostGold,
                operation.MaterialItemId,
                operation.MaterialQuantity,
                operation.CatalystItemId,
                operation.CatalystQuantity,
                0));
    }

    private static async Task<ItemReforgeOperationResult> RollbackFailureAsync(
        IDbContextTransaction transaction,
        string code,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return ItemReforgeOperationResult.Failure(code);
    }
}
