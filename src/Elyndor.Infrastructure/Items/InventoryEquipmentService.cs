using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Content;
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
    public const string ConsumableUnavailable = "inventory_consumable_unavailable";
    public const string InvalidSlot = "inventory_invalid_slot";
    public const string RequiredLevel = "inventory_required_level";
    public const string ClassRestricted = "inventory_class_restricted";
    public const string WeaponCategoryRestricted = "inventory_weapon_category_restricted";
    public const string ArmorCategoryRestricted = "inventory_armor_category_restricted";
    public const string OffHandCategoryRestricted = "inventory_off_hand_category_restricted";
    public const string TwoHandedConflict = "inventory_two_handed_conflict";
    public const string DualWieldPermissionRequired = "inventory_dual_wield_permission_required";
    public const string InvalidMutationId = "inventory_mutation_id_invalid";
    public const string MutationConflict = "inventory_mutation_conflict";
    public const string Conflict = "inventory_conflict";
    public const string InventoryFull = "inventory_full";
    public const string TransactionLocked = "inventory_item_transaction_locked";
}

public sealed record InventoryItemSnapshot(
    Guid Id,
    ItemDefinition Definition,
    int Quantity,
    DateTimeOffset AcquiredAtUtc,
    EquipmentSlot? EquippedSlot,
    bool IsLocked,
    PrimaryStats? RolledPrimaryStats = null,
    GeneratedItemInstance? GeneratedItem = null,
    int ReforgeCount = 0,
    string? ReforgeSlotKey = null,
    bool TransactionLocked = false,
    string BindState = ItemBindStates.Unbound)
{
    public PrimaryStats EffectiveStats => RolledPrimaryStats ?? Definition.Stats;
}

public sealed record InventorySnapshot(
    IReadOnlyList<InventoryItemSnapshot> Items,
    IReadOnlyDictionary<EquipmentSlot, InventoryItemSnapshot> Equipped);

public sealed record PendingLootItemSnapshot(
    Guid Id,
    ItemDefinition Definition,
    int Quantity,
    DateTimeOffset CreatedAtUtc,
    PrimaryStats? RolledPrimaryStats,
    GeneratedItemInstance? GeneratedItem = null);

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
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider)
{
    private const string EquipOperation = "INVENTORY_EQUIP";
    private const string UnequipOperation = "INVENTORY_UNEQUIP";
    private const string UseConsumableOperation = "INVENTORY_USE_CONSUMABLE";
    private const string SetItemLockOperation = "INVENTORY_SET_LOCK";
    private const string ClaimPendingLootOperation = "INVENTORY_CLAIM_PENDING_LOOT";
    private readonly CharacterDerivedStateService derivedStateService =
        new(dbContext, contentProvider);

    public InventoryEquipmentService(
        GameDbContext dbContext,
        GameContentPackage content,
        TimeProvider timeProvider)
        : this(
            dbContext,
            new StaticContentSnapshotProvider(content),
            timeProvider)
    {
    }

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

    public Task<InventorySnapshot> GetForCharacterAsync(
        Guid characterId,
        CancellationToken cancellationToken) =>
        GetForCharacterAsync(
            characterId,
            contentProvider.GetCurrent(),
            cancellationToken);

    public Task<InventorySnapshot> GetForCharacterAsync(
        Guid characterId,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contentSnapshot);
        return InventorySnapshotReader.ReadAsync(
            dbContext,
            contentSnapshot.Package,
            characterId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PendingLootItemSnapshot>> GetPendingLootAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId,
                cancellationToken);
        if (character is null)
            return [];

        GameContentSnapshot content = contentProvider.GetCurrent();
        PendingLootItem[] pending = await dbContext.PendingLootItems
            .AsNoTracking()
            .Where(item => item.CharacterId == character.Id)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

        return pending.Select(item =>
        {
            if (!content.Indexes.ItemsById.TryGetValue(
                    item.ItemDefinitionId,
                    out ItemDefinition? definition))
            {
                throw new InvalidOperationException(
                    $"Pending loot item '{item.ItemDefinitionId}' is missing from content.");
            }

            GeneratedItemInstance? generated = string.IsNullOrWhiteSpace(item.GeneratedItemJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<GeneratedItemInstance>(
                    item.GeneratedItemJson);
            ItemDefinition effective = generated is null
                ? definition
                : ItemInstanceGenerator.ApplyGeneratedAffixes(
                    definition,
                    generated.Affixes,
                    generated.DisplayName);
            return new PendingLootItemSnapshot(
                item.Id,
                effective,
                item.Quantity,
                item.CreatedAtUtc,
                item.RolledPrimaryStats,
                generated);
        }).ToArray();
    }

    public Task<InventoryOperationResult> ClaimPendingLootAsync(
        Guid accountId,
        Guid mutationId,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            accountId,
            mutationId,
            ClaimPendingLootOperation,
            Fingerprint(ClaimPendingLootOperation),
            async character =>
            {
                await ClaimPendingLootCoreAsync(
                    character.Id,
                    contentProvider.GetCurrent(),
                    cancellationToken);
                return null;
            },
            cancellationToken);

    public Task<InventoryOperationResult> EquipAsync(
        Guid accountId,
        Guid characterItemId,
        Guid mutationId,
        CancellationToken cancellationToken) =>
        EquipAsync(accountId, characterItemId, null, mutationId, cancellationToken);

    public Task<InventoryOperationResult> EquipAsync(
        Guid accountId,
        Guid characterItemId,
        EquipmentSlot? targetSlot,
        Guid mutationId,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            accountId,
            mutationId,
            EquipOperation,
            EquipFingerprint(characterItemId, targetSlot),
            async character =>
            {
                CharacterItem? item = await dbContext.CharacterItems
                    .SingleOrDefaultAsync(candidate => candidate.Id == characterItemId, cancellationToken);
                if (item is null)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.ItemNotFound);
                if (item.CharacterId != character.Id)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.ItemNotOwned);
                if (item.TransactionLockId.HasValue)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.TransactionLocked);

                ItemDefinition? definition = FindItem(item.ItemDefinitionId);
                if (definition is null || definition.Type != ItemType.Equipment)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.NotEquipment);
                if (definition.Slot is null)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.InvalidSlot);
                if (character.Level < definition.RequiredLevel)
                    return InventoryOperationResult.Failure(InventoryErrorCodes.RequiredLevel);

                if (!contentProvider.GetCurrent().Indexes.ClassesById.TryGetValue(
                        character.ClassId,
                        out ClassProfile? classProfile))
                {
                    throw new InvalidOperationException(
                        $"Class profile '{character.ClassId}' is missing from game content.");
                }

                if (definition.AllowedClassIds is { Count: > 0 }
                    && !definition.AllowedClassIds.Contains(character.ClassId, StringComparer.Ordinal))
                {
                    return InventoryOperationResult.Failure(InventoryErrorCodes.ClassRestricted);
                }

                if (definition.WeaponCategory is not null
                    && !classProfile.AllowedWeaponCategories.Contains(
                        definition.WeaponCategory,
                        StringComparer.Ordinal))
                {
                    return InventoryOperationResult.Failure(
                        InventoryErrorCodes.WeaponCategoryRestricted);
                }

                if (definition.ArmorCategory is not null
                    && !classProfile.AllowedArmorCategories.Contains(
                        definition.ArmorCategory,
                        StringComparer.Ordinal))
                {
                    return InventoryOperationResult.Failure(
                        InventoryErrorCodes.ArmorCategoryRestricted);
                }

                if (definition.OffHandCategory is not null
                    && !(classProfile.AllowedOffHandCategories ?? []).Contains(
                        definition.OffHandCategory,
                        StringComparer.Ordinal))
                {
                    return InventoryOperationResult.Failure(
                        InventoryErrorCodes.OffHandCategoryRestricted);
                }

                EquipmentSlot definitionSlot = CanonicalizeEquipmentSlot(definition.Slot.Value);
                EquipmentSlot canonicalSlot = targetSlot.HasValue
                    ? CanonicalizeEquipmentSlot(targetSlot.Value)
                    : definitionSlot;

                bool isOffHandOneHandWeapon = canonicalSlot == EquipmentSlot.OffHand
                    && EquipmentCategoryIds.IsOneHandedWeapon(definition.WeaponCategory);
                if (canonicalSlot != definitionSlot
                    && !(definitionSlot == EquipmentSlot.MainHand && isOffHandOneHandWeapon))
                {
                    return InventoryOperationResult.Failure(
                        canonicalSlot == EquipmentSlot.OffHand
                            && EquipmentCategoryIds.UsesBothHands(definition.WeaponCategory)
                            ? InventoryErrorCodes.TwoHandedConflict
                            : InventoryErrorCodes.InvalidSlot);
                }

                if (canonicalSlot == EquipmentSlot.OffHand && definition.WeaponCategory is not null)
                {
                    if (EquipmentCategoryIds.UsesBothHands(definition.WeaponCategory))
                    {
                        return InventoryOperationResult.Failure(
                            InventoryErrorCodes.TwoHandedConflict);
                    }

                    if (!EquipmentCategoryIds.IsOneHandedWeapon(definition.WeaponCategory))
                    {
                        return InventoryOperationResult.Failure(InventoryErrorCodes.InvalidSlot);
                    }

                    if (!await HasEquipmentPermissionAsync(
                            character.Id,
                            character.ClassId,
                            EquipmentPermissionIds.DualWieldOneHandWeapon,
                            cancellationToken))
                    {
                        return InventoryOperationResult.Failure(
                            InventoryErrorCodes.DualWieldPermissionRequired);
                    }
                }

                if (canonicalSlot == EquipmentSlot.OffHand
                    && await HasTwoHandedMainHandAsync(character.Id, cancellationToken))
                {
                    return InventoryOperationResult.Failure(
                        InventoryErrorCodes.TwoHandedConflict);
                }

                if (!await CanApplyEquipmentProjectionAsync(
                        character.Id,
                        item.Id,
                        canonicalSlot,
                        definition,
                        cancellationToken))
                {
                    return InventoryOperationResult.Failure(
                        InventoryErrorCodes.InventoryFull);
                }

                CharacterEquipment? currentItemEquipment = await dbContext.CharacterEquipment
                    .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id
                        && candidate.CharacterItemId == item.Id, cancellationToken);
                if (currentItemEquipment is not null
                    && CanonicalizeEquipmentSlot(currentItemEquipment.Slot) != canonicalSlot)
                {
                    dbContext.CharacterEquipment.Remove(currentItemEquipment);
                }

                EquipmentSlot[] aliases = EquivalentEquipmentSlots(canonicalSlot);

                CharacterEquipment[] equippedAliases = await dbContext.CharacterEquipment
                    .Where(candidate => candidate.CharacterId == character.Id
                        && aliases.Contains(candidate.Slot))
                    .ToArrayAsync(cancellationToken);
                CharacterEquipment? canonicalEquipment = equippedAliases
                    .SingleOrDefault(candidate => candidate.Slot == canonicalSlot);

                if (canonicalEquipment is null)
                {
                    if (equippedAliases.Length > 0)
                    {
                        dbContext.CharacterEquipment.RemoveRange(equippedAliases);
                    }

                    dbContext.CharacterEquipment.Add(new CharacterEquipment(
                        character.Id,
                        canonicalSlot,
                        item.Id));
                }
                else
                {
                    canonicalEquipment.Equip(item.Id);

                    CharacterEquipment[] legacyAliases = equippedAliases
                        .Where(candidate => candidate != canonicalEquipment)
                        .ToArray();
                    if (legacyAliases.Length > 0)
                    {
                        dbContext.CharacterEquipment.RemoveRange(legacyAliases);
                    }
                }

                if (canonicalSlot == EquipmentSlot.MainHand
                    && EquipmentCategoryIds.UsesBothHands(definition.WeaponCategory))
                {
                    CharacterEquipment[] offHand = await dbContext.CharacterEquipment
                        .Where(candidate => candidate.CharacterId == character.Id
                            && candidate.Slot == EquipmentSlot.OffHand)
                        .ToArrayAsync(cancellationToken);
                    if (offHand.Length > 0)
                    {
                        dbContext.CharacterEquipment.RemoveRange(offHand);
                    }
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
                EquipmentSlot canonicalSlot = CanonicalizeEquipmentSlot(slot);
                EquipmentSlot[] aliases = EquivalentEquipmentSlots(canonicalSlot);
                CharacterEquipment[] equipped = await dbContext.CharacterEquipment
                    .Where(candidate => candidate.CharacterId == character.Id
                        && aliases.Contains(candidate.Slot))
                    .ToArrayAsync(cancellationToken);
                if (equipped.Length > 0)
                {
                    GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
                    if (await InventoryCapacity.FreeSlotsAsync(
                            dbContext,
                            character.Id,
                            contentSnapshot,
                            cancellationToken) < equipped.Length)
                    {
                        return InventoryOperationResult.Failure(
                            InventoryErrorCodes.InventoryFull);
                    }

                    dbContext.CharacterEquipment.RemoveRange(equipped);
                }
                return null;
            },
            cancellationToken);

    public Task<InventoryOperationResult> UseConsumableOutOfCombatAsync(
        Guid accountId,
        Guid characterItemId,
        Guid mutationId,
        decimal maxHp,
        string resourceType,
        decimal maxResource,
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
                if (definition is null
                    || definition.Type != ItemType.Consumable
                    || definition.ConsumableActions is not { Count: > 0 })
                {
                    return InventoryOperationResult.Failure(
                        InventoryErrorCodes.NotConsumable);
                }

                if (definition.ConsumableActions.Any(action =>
                        action.Type is ConsumableActionType.ApplyEffect
                            or ConsumableActionType.RemoveEffect))
                {
                    return InventoryOperationResult.Failure(
                        InventoryErrorCodes.ConsumableUnavailable);
                }

                CharacterVitals vitals = await dbContext.CharacterVitals.SingleAsync(
                    candidate => candidate.CharacterId == character.Id,
                    cancellationToken);
                decimal currentHp = Math.Min(maxHp, vitals.CurrentHp);
                decimal currentResource = Math.Clamp(
                    vitals.CurrentResource,
                    0,
                    maxResource);
                decimal nextHp = currentHp;
                decimal nextResource = currentResource;
                bool changesState = false;

                foreach (ConsumableActionDefinition action in definition.ConsumableActions)
                {
                    switch (action.Type)
                    {
                        case ConsumableActionType.RestoreHp:
                            if (action.Amount <= 0)
                            {
                                return InventoryOperationResult.Failure(
                                    InventoryErrorCodes.NotConsumable);
                            }
                            decimal healedHp = Math.Min(maxHp, nextHp + action.Amount);
                            changesState |= healedHp > nextHp;
                            nextHp = healedHp;
                            break;
                        case ConsumableActionType.RestoreResource:
                            if (action.Amount <= 0
                                || !string.Equals(
                                    action.ResourceType,
                                    resourceType,
                                    StringComparison.Ordinal))
                            {
                                return InventoryOperationResult.Failure(
                                    InventoryErrorCodes.ConsumableUnavailable);
                            }
                            decimal restoredResource = Math.Min(
                                maxResource,
                                nextResource + action.Amount);
                            changesState |= restoredResource > nextResource;
                            nextResource = restoredResource;
                            break;
                        default:
                            return InventoryOperationResult.Failure(
                                InventoryErrorCodes.ConsumableUnavailable);
                    }
                }

                if (!changesState)
                {
                    return InventoryOperationResult.Failure(
                        InventoryErrorCodes.ConsumableNotNeeded);
                }

                vitals.Checkpoint(nextHp, nextResource, now);
                ConsumeOne(item);
                return null;
            },
            cancellationToken);

    public Task<InventoryOperationResult> SetItemLockAsync(
        Guid accountId,
        Guid characterItemId,
        bool isLocked,
        Guid mutationId,
        CancellationToken cancellationToken) =>
        ExecuteMutationAsync(
            accountId,
            mutationId,
            SetItemLockOperation,
            Fingerprint(
                SetItemLockOperation,
                characterItemId.ToString("N"),
                isLocked ? "1" : "0"),
            async character =>
            {
                CharacterItem? item = await dbContext.CharacterItems
                    .SingleOrDefaultAsync(
                        candidate => candidate.Id == characterItemId,
                        cancellationToken);
                if (item is null)
                    return InventoryOperationResult.Failure(
                        InventoryErrorCodes.ItemNotFound);
                if (item.CharacterId != character.Id)
                    return InventoryOperationResult.Failure(
                        InventoryErrorCodes.ItemNotOwned);
                if (item.TransactionLockId.HasValue)
                    return InventoryOperationResult.Failure(
                        InventoryErrorCodes.TransactionLocked);

                item.SetLocked(isLocked);
                return null;
            },
            cancellationToken);

    public Task<string?> ConsumeOneForCombatAsync(
        Guid accountId,
        string itemDefinitionId,
        CancellationToken cancellationToken) =>
        ConsumeOneForCombatAsync(
            accountId,
            itemDefinitionId,
            contentProvider.GetCurrent(),
            cancellationToken);

    public async Task<string?> ConsumeOneForCombatAsync(
        Guid accountId,
        string itemDefinitionId,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contentSnapshot);
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
                        && candidate.Quantity > 0
                        && candidate.TransactionLockId == null)
                    .OrderBy(candidate => candidate.AcquiredAtUtc)
                    .FirstOrDefaultAsync(cancellationToken);
                if (item is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return InventoryErrorCodes.ItemNotFound;
                }

                ItemDefinition? definition =
                    contentSnapshot.Indexes.ItemsById.GetValueOrDefault(
                        item.ItemDefinitionId);
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

    private async Task<bool> CanApplyEquipmentProjectionAsync(
        Guid characterId,
        Guid itemId,
        EquipmentSlot targetSlot,
        ItemDefinition definition,
        CancellationToken cancellationToken)
    {
        CharacterEquipment[] current = await dbContext.CharacterEquipment
            .AsNoTracking()
            .Where(equipment => equipment.CharacterId == characterId)
            .ToArrayAsync(cancellationToken);

        HashSet<Guid> projectedEquippedItemIds =
            current.Select(equipment => equipment.CharacterItemId).ToHashSet();

        foreach (CharacterEquipment equipment in current)
        {
            bool occupiesTarget = EquivalentEquipmentSlots(targetSlot)
                .Contains(equipment.Slot);
            bool displacedByTwoHandedMain =
                targetSlot == EquipmentSlot.MainHand
                && EquipmentCategoryIds.UsesBothHands(definition.WeaponCategory)
                && equipment.Slot == EquipmentSlot.OffHand;
            bool isMovingItem = equipment.CharacterItemId == itemId;

            if (occupiesTarget || displacedByTwoHandedMain || isMovingItem)
                projectedEquippedItemIds.Remove(equipment.CharacterItemId);
        }

        projectedEquippedItemIds.Add(itemId);

        int totalItemSlots = await dbContext.CharacterItems
            .AsNoTracking()
            .CountAsync(
                item => item.CharacterId == characterId,
                cancellationToken);
        int projectedInventorySlots =
            totalItemSlots - projectedEquippedItemIds.Count;

        return projectedInventorySlots <=
            InventoryCapacity.Resolve(contentProvider.GetCurrent());
    }

    private async Task ClaimPendingLootCoreAsync(
        Guid characterId,
        GameContentSnapshot content,
        CancellationToken cancellationToken)
    {
        PendingLootItem[] pending = await dbContext.PendingLootItems
            .Where(item => item.CharacterId == characterId)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

        foreach (PendingLootItem pendingItem in pending)
        {
            if (!content.Indexes.ItemsById.TryGetValue(
                    pendingItem.ItemDefinitionId,
                    out ItemDefinition? definition))
            {
                throw new InvalidOperationException(
                    $"Pending loot item '{pendingItem.ItemDefinitionId}' is missing from content.");
            }
            if (!definition.Stackable)
            {
                if (await InventoryCapacity.FreeSlotsAsync(
                        dbContext,
                        characterId,
                        content,
                        cancellationToken) < 1)
                {
                    break;
                }

                dbContext.CharacterItems.Add(
                    ItemInstancePersistenceFactory.MaterializePending(
                        pendingItem,
                        definition,
                        timeProvider.GetUtcNow()));
                dbContext.PendingLootItems.Remove(pendingItem);
                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            int remaining = pendingItem.Quantity;
            CharacterItem[] stacks = await dbContext.CharacterItems
                .Where(item => item.CharacterId == characterId
                    && item.ItemDefinitionId == definition.Id
                    && item.DefinitionVersion == definition.Version
                    && item.Quantity < definition.MaxStack)
                .OrderBy(item => item.AcquiredAtUtc)
                .ToArrayAsync(cancellationToken);

            foreach (CharacterItem stack in stacks)
            {
                if (remaining <= 0) break;
                int toAdd = Math.Min(
                    definition.MaxStack - stack.Quantity,
                    remaining);
                if (toAdd <= 0) continue;
                stack.AddQuantity(toAdd, definition.MaxStack);
                remaining -= toAdd;
            }

            int freeSlots = await InventoryCapacity.FreeSlotsAsync(
                dbContext,
                characterId,
                content,
                cancellationToken);
            while (remaining > 0 && freeSlots > 0)
            {
                int quantity = Math.Min(definition.MaxStack, remaining);
                dbContext.CharacterItems.Add(new CharacterItem(
                    Guid.NewGuid(),
                    characterId,
                    definition.Id,
                    quantity,
                    timeProvider.GetUtcNow(),
                    definition.Version));
                remaining -= quantity;
                freeSlots--;
            }

            if (remaining == 0)
                dbContext.PendingLootItems.Remove(pendingItem);
            else
                pendingItem.SetQuantity(remaining);

            await dbContext.SaveChangesAsync(cancellationToken);
            if (remaining > 0)
                break;
        }
    }

    private async Task<bool> HasEquipmentPermissionAsync(
        Guid characterId,
        string classId,
        string permissionId,
        CancellationToken cancellationToken)
    {
        CharacterTalentState? state = await dbContext.CharacterTalentStates
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.CharacterId == characterId, cancellationToken);
        if (state is null) return false;

        GameContentSnapshot content = contentProvider.GetCurrent();
        if (!content.Indexes.TalentTreesById.TryGetValue(state.TalentTreeId, out TalentTreeDefinition? tree)
            || !string.Equals(tree.ClassId, classId, StringComparison.Ordinal))
        {
            return false;
        }

        return TalentEquipmentPermissionResolver.HasPermission(tree, state, permissionId);
    }

    private async Task<bool> HasTwoHandedMainHandAsync(
        Guid characterId,
        CancellationToken cancellationToken)
    {
        EquipmentSlot[] mainHandSlots = EquivalentEquipmentSlots(EquipmentSlot.MainHand);
        string? definitionId = await (
                from equipment in dbContext.CharacterEquipment.AsNoTracking()
                join item in dbContext.CharacterItems.AsNoTracking()
                    on equipment.CharacterItemId equals item.Id
                where equipment.CharacterId == characterId
                    && mainHandSlots.Contains(equipment.Slot)
                select item.ItemDefinitionId)
            .FirstOrDefaultAsync(cancellationToken);

        return definitionId is not null
            && FindItem(definitionId) is { } definition
            && EquipmentCategoryIds.UsesBothHands(definition.WeaponCategory);
    }

    private static EquipmentSlot CanonicalizeEquipmentSlot(EquipmentSlot slot) =>
        slot switch
        {
            EquipmentSlot.Weapon => EquipmentSlot.MainHand,
            EquipmentSlot.Boots => EquipmentSlot.Feet,
            EquipmentSlot.Accessory => EquipmentSlot.Amulet,
            _ => slot
        };

    private static EquipmentSlot[] EquivalentEquipmentSlots(EquipmentSlot canonicalSlot) =>
        canonicalSlot switch
        {
            EquipmentSlot.MainHand => [EquipmentSlot.MainHand, EquipmentSlot.Weapon],
            EquipmentSlot.Feet => [EquipmentSlot.Feet, EquipmentSlot.Boots],
            EquipmentSlot.Amulet => [EquipmentSlot.Amulet, EquipmentSlot.Accessory],
            _ => [canonicalSlot]
        };

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

                CharacterDerivedState? oldDerivedState = null;
                if (operationType == EquipOperation || operationType == UnequipOperation)
                {
                    oldDerivedState = await derivedStateService.ResolveAsync(
                        character.Id,
                        character.ClassId,
                        character.Level,
                        cancellationToken);
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

                if (oldDerivedState is not null)
                {
                    CharacterDerivedState newDerivedState = await derivedStateService.ResolveAsync(
                        character.Id,
                        character.ClassId,
                        character.Level,
                        cancellationToken);
                    CharacterVitals vitals = await dbContext.CharacterVitals.SingleAsync(
                        candidate => candidate.CharacterId == character.Id,
                        cancellationToken);
                    CharacterVitalsScaler.ScaleToDerivedMaximums(
                        vitals,
                        oldDerivedState.Stats.MaxHp,
                        newDerivedState.Stats.MaxHp,
                        oldDerivedState.EffectiveResourceProfile.MaxValue,
                        newDerivedState.EffectiveResourceProfile.MaxValue,
                        timeProvider.GetUtcNow());
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

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
        contentProvider.GetCurrent().Indexes.ItemsById.GetValueOrDefault(definitionId);

    private static string EquipFingerprint(Guid characterItemId, EquipmentSlot? targetSlot) =>
        targetSlot.HasValue
            ? Fingerprint(
                EquipOperation,
                characterItemId.ToString("N"),
                CanonicalizeEquipmentSlot(targetSlot.Value).ToString())
            : Fingerprint(EquipOperation, characterItemId.ToString("N"));

    private static string Fingerprint(params string[] parts)
    {
        string canonical = string.Join(
            "\u001F",
            parts.Select(part => $"{part.Length.ToString(CultureInfo.InvariantCulture)}:{part}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
