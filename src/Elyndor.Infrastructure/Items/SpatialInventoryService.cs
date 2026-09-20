using System.Data;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Items;

public static class SpatialInventoryErrorCodes
{
    public const string CharacterNotFound = "character_not_found";
    public const string ItemNotFound = "inventory_item_not_found";
    public const string ItemNotOwned = "inventory_item_not_owned";
    public const string NotSpatialArtifact = "inventory_item_not_spatial_artifact";
    public const string TransactionLocked = "inventory_item_transaction_locked";
    public const string InventoryFull = "inventory_full";
    public const string Conflict = "inventory_conflict";
}

public sealed record SpatialArtifactSnapshot(
    Guid CharacterItemId,
    ItemDefinition Definition);

public sealed record SpatialInventorySnapshot(
    SpatialArtifactSnapshot? EquippedArtifact,
    InventoryCapacityState Capacity);

public sealed record SpatialInventoryOperationResult(
    bool IsSuccess,
    string? ErrorCode,
    SpatialInventorySnapshot? Snapshot)
{
    public static SpatialInventoryOperationResult Success(SpatialInventorySnapshot snapshot) =>
        new(true, null, snapshot);

    public static SpatialInventoryOperationResult Failure(string errorCode) =>
        new(false, errorCode, null);
}

public sealed class SpatialInventoryService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider)
{
    public async Task<SpatialInventoryOperationResult> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Guid? characterId = await dbContext.Characters
            .AsNoTracking()
            .Where(character => character.AccountId == accountId)
            .Select(character => (Guid?)character.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (!characterId.HasValue)
            return SpatialInventoryOperationResult.Failure(SpatialInventoryErrorCodes.CharacterNotFound);

        return SpatialInventoryOperationResult.Success(
            await ReadAsync(characterId.Value, contentProvider.GetCurrent(), cancellationToken));
    }

    public async Task<SpatialInventoryOperationResult> EquipAsync(
        Guid accountId,
        Guid characterItemId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            Character? character = await dbContext.Characters
                .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
            if (character is null)
                return await RollbackFailureAsync(transaction, SpatialInventoryErrorCodes.CharacterNotFound, cancellationToken);

            CharacterItem? item = await dbContext.CharacterItems
                .SingleOrDefaultAsync(candidate => candidate.Id == characterItemId, cancellationToken);
            if (item is null)
                return await RollbackFailureAsync(transaction, SpatialInventoryErrorCodes.ItemNotFound, cancellationToken);
            if (item.CharacterId != character.Id)
                return await RollbackFailureAsync(transaction, SpatialInventoryErrorCodes.ItemNotOwned, cancellationToken);
            if (item.TransactionLockId.HasValue)
                return await RollbackFailureAsync(transaction, SpatialInventoryErrorCodes.TransactionLocked, cancellationToken);

            GameContentSnapshot content = contentProvider.GetCurrent();
            if (!content.Indexes.ItemsById.TryGetValue(item.ItemDefinitionId, out ItemDefinition? definition)
                || definition.Type != ItemType.SpatialArtifact
                || definition.InventoryCapacityBonus <= 0)
            {
                return await RollbackFailureAsync(transaction, SpatialInventoryErrorCodes.NotSpatialArtifact, cancellationToken);
            }

            CharacterSpatialArtifact? current = await dbContext.CharacterSpatialArtifacts
                .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
            if (current is null)
                dbContext.CharacterSpatialArtifacts.Add(new CharacterSpatialArtifact(character.Id, item.Id));
            else if (current.CharacterItemId != item.Id)
                current.Equip(item.Id);

            InventoryCapacityState projected = await InventoryCapacity.GetStateAsync(
                dbContext,
                character.Id,
                content,
                cancellationToken);
            if (projected.IsOverflow)
                return await RollbackFailureAsync(transaction, SpatialInventoryErrorCodes.InventoryFull, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SpatialInventoryOperationResult.Success(
                await ReadAsync(character.Id, content, cancellationToken));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return SpatialInventoryOperationResult.Failure(SpatialInventoryErrorCodes.Conflict);
        }
    }

    public async Task<SpatialInventoryOperationResult> UnequipAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            Character? character = await dbContext.Characters
                .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
            if (character is null)
                return await RollbackFailureAsync(transaction, SpatialInventoryErrorCodes.CharacterNotFound, cancellationToken);

            CharacterSpatialArtifact? current = await dbContext.CharacterSpatialArtifacts
                .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
            GameContentSnapshot content = contentProvider.GetCurrent();
            if (current is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return SpatialInventoryOperationResult.Success(
                    await ReadAsync(character.Id, content, cancellationToken));
            }

            dbContext.CharacterSpatialArtifacts.Remove(current);
            InventoryCapacityState projected = await InventoryCapacity.GetStateAsync(
                dbContext,
                character.Id,
                content,
                cancellationToken);
            if (projected.IsOverflow)
                return await RollbackFailureAsync(transaction, SpatialInventoryErrorCodes.InventoryFull, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SpatialInventoryOperationResult.Success(
                await ReadAsync(character.Id, content, cancellationToken));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return SpatialInventoryOperationResult.Failure(SpatialInventoryErrorCodes.Conflict);
        }
    }

    private async Task<SpatialInventorySnapshot> ReadAsync(
        Guid characterId,
        GameContentSnapshot content,
        CancellationToken cancellationToken)
    {
        var equipped = await (
                from artifact in dbContext.CharacterSpatialArtifacts.AsNoTracking()
                join item in dbContext.CharacterItems.AsNoTracking()
                    on artifact.CharacterItemId equals item.Id
                where artifact.CharacterId == characterId
                select new { item.Id, item.ItemDefinitionId })
            .SingleOrDefaultAsync(cancellationToken);

        SpatialArtifactSnapshot? artifactSnapshot = null;
        if (equipped is not null
            && content.Indexes.ItemsById.TryGetValue(equipped.ItemDefinitionId, out ItemDefinition? definition))
        {
            artifactSnapshot = new SpatialArtifactSnapshot(equipped.Id, definition);
        }

        InventoryCapacityState capacity = await InventoryCapacity.GetStateAsync(
            dbContext,
            characterId,
            content,
            cancellationToken);
        return new SpatialInventorySnapshot(artifactSnapshot, capacity);
    }

    private async Task<SpatialInventoryOperationResult> RollbackFailureAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        string errorCode,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return SpatialInventoryOperationResult.Failure(errorCode);
    }
}
