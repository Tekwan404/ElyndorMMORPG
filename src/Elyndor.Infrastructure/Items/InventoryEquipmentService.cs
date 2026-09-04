using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Items;

public static class InventoryErrorCodes
{
    public const string CharacterNotFound = "character_not_found";
    public const string ItemNotFound = "inventory_item_not_found";
    public const string ItemNotOwned = "inventory_item_not_owned";
    public const string NotEquipment = "inventory_item_not_equipment";
    public const string NotConsumable = "inventory_item_not_consumable";
    public const string ConsumableNotNeeded = "inventory_consumable_not_needed";
    public const string InvalidSlot = "inventory_invalid_slot";
    public const string RequiredLevel = "inventory_required_level";
    public const string InvalidMutationId = "inventory_mutation_id_invalid";
    public const string MutationConflict = "inventory_mutation_conflict";
    public const string Conflict = "inventory_conflict";
}

public sealed record InventoryItemSnapshot(
    Guid Id,
    ItemDefinition Definition,
    int Quantity,
    DateTimeOffset AcquiredAtUtc,
    EquipmentSlot? EquippedSlot);

public sealed record InventorySnapshot(
    IReadOnlyList<InventoryItemSnapshot> Items,
    IReadOnlyDictionary<EquipmentSlot, InventoryItemSnapshot> Equipped);

public sealed record InventoryOperationResult(
    bool IsSuccess,
    string? ErrorCode,
    InventorySnapshot? Snapshot)
{
    public static InventoryOperationResult Success(InventorySnapshot snapshot) =>
        new(true, null, snapshot);

    public static InventoryOperationResult Failure(string errorCode) =>
        new(false, errorCode, null);
}

public sealed class InventoryEquipmentService(
    GameDbContext dbContext,
    GameContentPackage content,
    TimeProvider timeProvider)
{
    private const string EquipOperation = "INVENTORY_EQUIP";
    private const string UnequipOperation = "INVENTORY_UNEQUIP";
    private const string UseConsumableOperation = "INVENTORY_USE_CONSUMABLE";

    public async Task<InventoryOperationResult> GetAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        return character is null
            ? InventoryOperationResult.Failure(InventoryErrorCodes.CharacterNotFound)
            : InventoryOperationResult.Success(
                await GetForCharacterAsync(character.Id, cancellationToken));
    }

    public async Task<InventorySnapshot> GetForCharacterAsync(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        CharacterItem[] items = await dbContext.CharacterItems
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId)
            .OrderByDescending(item => item.AcquiredAtUtc)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        CharacterEquipment[] equipment = await dbContext.CharacterEquipment
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId)
            .ToArrayAsync(cancellationToken);
        Dictionary<Guid, EquipmentSlot> equippedSlots = equipment
            .ToDictionary(item => item.CharacterItemId, item => item.Slot);
        Dictionary<string, ItemDefinition> definitions = RequiredDefinitions();

        InventoryItemSnapshot[] snapshots = items.Select(item =>
        {
            if (!definitions.TryGetValue(item.ItemDefinitionId, out ItemDefinition? definition))
            {
                throw new InvalidOperationException(
                    $"Inventory item '{item.ItemDefinitionId}' is missing from game content.");
            }

            EquipmentSlot? equippedSlot = equippedSlots.TryGetValue(item.Id, out EquipmentSlot slot)
                ? slot
                : null;
            return new InventoryItemSnapshot(
                item.Id,
                definition,
                item.Quantity,
                item.AcquiredAtUtc,
                equippedSlot);
        }).ToArray();
        Dictionary<EquipmentSlot, InventoryItemSnapshot> equipped = snapshots
            .Where(item => item.EquippedSlot.HasValue)
            .ToDictionary(item => item.EquippedSlot!.Value);
        return new InventorySnapshot(snapshots, equipped);
    }

    public Task<InventoryOperationResult> EquipAsync(
        Guid accountId,
        Guid characterItemId,
        Guid mutationId,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            accountId,
            mutationId,
            EquipOperation,
            Fingerprint(EquipOperation, characterItemId.ToString("N")),
            async character =>
            {
                CharacterItem? item = await dbContext.CharacterItems
                    .SingleOrDefaultAsync(candidate => candidate.Id == characterItemId, cancellationToken);
                if (item is null)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.ItemNotFound);
                if (item.CharacterId != character.Id)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.ItemNotOwned);

                ItemDefinition? definition = FindItem(item.ItemDefinitionId);
                if (definition is null || definition.Type != ItemType.Equipment)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.NotEquipment);
                if (definition.Slot is null)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.InvalidSlot);
                if (character.Level < definition.RequiredLevel)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.RequiredLevel);

                CharacterEquipment? equipped = await dbContext.CharacterEquipment
                    .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id
                        && candidate.Slot == definition.Slot.Value, cancellationToken);
                if (equipped is null)
                {
                    dbContext.CharacterEquipment.Add(new CharacterEquipment(
                        character.Id,
                        definition.Slot.Value,
                        item.Id));
                }
                else
                {
                    equipped.Equip(item.Id);
                }

                return null;
            },
            cancellationToken);

    public Task<InventoryOperationResult> UnequipAsync(
        Guid accountId,
        EquipmentSlot slot,
        Guid mutationId,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            accountId,
            mutationId,
            UnequipOperation,
            Fingerprint(UnequipOperation, slot.ToString()),
            async character =>
            {
                CharacterEquipment? equipped = await dbContext.CharacterEquipment
                    .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id
                        && candidate.Slot == slot, cancellationToken);
                if (equipped is not null)
                    dbContext.CharacterEquipment.Remove(equipped);
                return null;
            },
            cancellationToken);

    public Task<InventoryOperationResult> UseConsumableOutOfCombatAsync(
        Guid accountId,
        Guid characterItemId,
        Guid mutationId,
        decimal maxHp,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            accountId,
            mutationId,
            UseConsumableOperation,
            Fingerprint(UseConsumableOperation, characterItemId.ToString("N")),
            async character =>
            {
                CharacterItem? item = await dbContext.CharacterItems
                    .SingleOrDefaultAsync(candidate => candidate.Id == characterItemId, cancellationToken);
                if (item is null)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.ItemNotFound);
                if (item.CharacterId != character.Id)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.ItemNotOwned);

                ItemDefinition? definition = FindItem(item.ItemDefinitionId);
                if (definition is null || definition.Type != ItemType.Consumable || definition.HealAmount <= 0)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.NotConsumable);

                CharacterVitals vitals = await dbContext.CharacterVitals.SingleAsync(
                    candidate => candidate.CharacterId == character.Id,
                    cancellationToken);
                decimal currentHp = Math.Min(maxHp, vitals.CurrentHp);
                if (currentHp >= maxHp)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.ConsumableNotNeeded);

                vitals.Checkpoint(
                    Math.Min(maxHp, currentHp + definition.HealAmount),
                    vitals.CurrentResource,
                    now);
                ConsumeOne(item);
                return null;
            },
            cancellationToken);

    public async Task<string?> ConsumeOneForCombatAsync(
        Guid accountId,
        string itemDefinitionId,
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
                Character? character = await LockCharacterAsync(accountId, cancellationToken);
                if (character is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return InventoryErrorCodes.CharacterNotFound;
                }

                CharacterItem? item = await dbContext.CharacterItems
                    .Where(candidate => candidate.CharacterId == character.Id
                        && candidate.ItemDefinitionId == itemDefinitionId
                        && candidate.Quantity > 0)
                    .OrderBy(candidate => candidate.AcquiredAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);
                if (item is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return InventoryErrorCodes.ItemNotFound;
                }

                ItemDefinition? definition = FindItem(item.ItemDefinitionId);
                if (definition is null || definition.Type != ItemType.Consumable)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return InventoryErrorCodes.NotConsumable;
                }

                ConsumeOne(item);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InventoryErrorCodes.Conflict;
            }
        });
    }

    private void ConsumeOne(CharacterItem item)
    {
        item.RemoveQuantity(1);
        if (item.Quantity == 0)
            dbContext.CharacterItems.Remove(item);
    }

    private async Task<InventoryOperationResult> ExecuteMutationAsync(
        Guid accountId,
        Guid mutationId,
        string operationType,
        string requestFingerprint,
        Func<Character, Task<InventoryOperationResult?>> mutation,
        CancellationToken cancellationToken)
    {
        if (mutationId == Guid.Empty)
            return InventoryOperationResult.Failure(InventoryErrorCodes.InvalidMutationId);

        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                Character? character = await LockCharacterAsync(accountId, cancellationToken);
                if (character is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return InventoryOperationResult.Failure(InventoryErrorCodes.CharacterNotFound);
                }

                CharacterMutation? existing = await dbContext.CharacterMutations
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        candidate => candidate.CharacterId == character.Id
                            && candidate.MutationId == mutationId,
                        cancellationToken);
                if (existing is not null)
                {
                    if (!string.Equals(existing.OperationType, operationType, StringComparison.Ordinal)
                        || !string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return InventoryOperationResult.Failure(InventoryErrorCodes.MutationConflict);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    return InventoryOperationResult.Success(
                        await GetForCharacterAsync(character.Id, cancellationToken));
                }

                dbContext.CharacterMutations.Add(new CharacterMutation(
                    character.Id,
                    mutationId,
                    operationType,
                    requestFingerprint,
                    timeProvider.GetUtcNow()));
                await dbContext.SaveChangesAsync(cancellationToken);

                InventoryOperationResult? failure = await mutation(character);
                if (failure is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return failure;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return InventoryOperationResult.Success(
                    await GetForCharacterAsync(character.Id, cancellationToken));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return InventoryOperationResult.Failure(InventoryErrorCodes.Conflict);
            }
        });
    }

    private Task<Character?> LockCharacterAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        dbContext.Characters
            .FromSqlInterpolated(
                $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private ItemDefinition? FindItem(string definitionId) =>
        (content.Items ?? []).SingleOrDefault(candidate =>
            string.Equals(candidate.Id, definitionId, StringComparison.Ordinal));

    private Dictionary<string, ItemDefinition> RequiredDefinitions() =>
        (content.Items ?? throw new InvalidOperationException("Item content is required."))
            .ToDictionary(item => item.Id, StringComparer.Ordinal);

    private static string Fingerprint(params string[] parts)
    {
        string canonical = string.Join(
            "\u001F",
            parts.Select(part => $"{part.Length.ToString(CultureInfo.InvariantCulture)}:{part}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
