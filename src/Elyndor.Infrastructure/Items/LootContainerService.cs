using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Elyndor.Infrastructure.Items;

public static class LootContainerErrorCodes
{
    public const string CharacterNotFound = "loot_container_character_not_found";
    public const string ItemNotFound = "loot_container_item_not_found";
    public const string InvalidItem = "loot_container_item_invalid";
    public const string ContentMissing = "loot_container_content_missing";
    public const string MutationConflict = "loot_container_mutation_conflict";
}

public sealed record LootContainerGrantedItem(
    string DefinitionId,
    string Name,
    ItemRarity Rarity,
    int Quantity,
    string? IconId,
    bool Pending);

public sealed record LootContainerOpenResult(
    bool Succeeded,
    string? ErrorCode,
    bool WasReplay,
    int Gold,
    IReadOnlyList<LootContainerGrantedItem> Items)
{
    public static LootContainerOpenResult Failure(string errorCode) =>
        new(false, errorCode, false, 0, []);
}

public sealed class LootContainerService(
    GameDbContext db,
    IContentSnapshotProvider contentProvider,
    TimeProvider time)
{
    private const string OperationType = "OPEN_LOOT_CONTAINER";

    public async Task<LootContainerOpenResult> OpenAsync(
        Guid accountId,
        Guid characterItemId,
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account identifier cannot be empty.", nameof(accountId));
        if (characterItemId == Guid.Empty)
            throw new ArgumentException("Item identifier cannot be empty.", nameof(characterItemId));
        if (mutationId == Guid.Empty)
            throw new ArgumentException("Mutation identifier cannot be empty.", nameof(mutationId));

        IExecutionStrategy strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using IDbContextTransaction transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);

            Character? character = await db.Characters
                .FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (character is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return LootContainerOpenResult.Failure(
                    LootContainerErrorCodes.CharacterNotFound);
            }

            string fingerprint = Fingerprint(characterItemId);
            CharacterMutation? existing = await db.CharacterMutations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.CharacterId == character.Id
                        && candidate.MutationId == mutationId,
                    cancellationToken);
            if (existing is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return string.Equals(existing.OperationType, OperationType, StringComparison.Ordinal)
                    && string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal)
                    ? new(true, null, true, 0, [])
                    : LootContainerOpenResult.Failure(
                        LootContainerErrorCodes.MutationConflict);
            }

            CharacterItem? containerItem = await db.CharacterItems
                .FromSqlInterpolated(
                    $"SELECT * FROM game.character_items WHERE \"Id\" = {characterItemId} AND \"CharacterId\" = {character.Id} AND \"Storage\" = 'INVENTORY' FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (containerItem is null || containerItem.TransactionLockId.HasValue)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return LootContainerOpenResult.Failure(
                    LootContainerErrorCodes.ItemNotFound);
            }

            GameContentSnapshot content = contentProvider.GetCurrent();
            if (!content.Indexes.ItemsById.TryGetValue(
                    containerItem.ItemDefinitionId,
                    out ItemDefinition? containerDefinition)
                || containerDefinition.Type != ItemType.LootContainer)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return LootContainerOpenResult.Failure(
                    LootContainerErrorCodes.InvalidItem);
            }

            if (string.IsNullOrWhiteSpace(containerDefinition.LootContainerTableId)
                || !content.Indexes.LootTablesById.TryGetValue(
                    containerDefinition.LootContainerTableId,
                    out LootTableDefinition? lootTable))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return LootContainerOpenResult.Failure(
                    LootContainerErrorCodes.ContentMissing);
            }

            var random = new SeededGameRandom(SeedToInt32(mutationId));
            LootRoll[] rolls = LootRoller.Roll(lootTable, random)
                .Select(roll => roll with { SourceQualityProfileId = "BOSS" })
                .ToArray();
            if (rolls.Length == 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return LootContainerOpenResult.Failure(
                    LootContainerErrorCodes.ContentMissing);
            }

            int gold = RollInclusive(
                containerDefinition.LootContainerGoldMin,
                containerDefinition.LootContainerGoldMax,
                random);

            containerItem.RemoveQuantity(1);
            if (containerItem.Quantity == 0)
                db.CharacterItems.Remove(containerItem);

            character.AddGold(gold);

            DateTimeOffset now = time.GetUtcNow();
            List<LootContainerGrantedItem> granted = [];
            var ordinal = 0;
            foreach (LootRoll roll in rolls)
            {
                if (!content.Indexes.ItemsById.TryGetValue(
                        roll.ItemId,
                        out ItemDefinition? definition))
                {
                    throw new InvalidOperationException(
                        $"Loot container item '{roll.ItemId}' is missing from content.");
                }

                bool pending = await GrantAsync(
                    character.Id,
                    definition,
                    roll,
                    mutationId,
                    ordinal,
                    now,
                    content,
                    cancellationToken);
                granted.Add(new(
                    definition.Id,
                    definition.Name,
                    definition.Rarity,
                    roll.Quantity,
                    definition.IconId,
                    pending));
                ordinal = checked(ordinal + roll.Quantity);
            }

            db.CharacterMutations.Add(new CharacterMutation(
                character.Id,
                mutationId,
                OperationType,
                fingerprint,
                now));

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(CancellationToken.None);

            return new(true, null, false, gold, granted);
        });
    }

    private async Task<bool> GrantAsync(
        Guid characterId,
        ItemDefinition definition,
        LootRoll roll,
        Guid mutationId,
        int ordinal,
        DateTimeOffset now,
        GameContentSnapshot content,
        CancellationToken cancellationToken)
    {
        if (!definition.Stackable)
        {
            int equipmentFreeSlots = await InventoryCapacity.FreeSlotsAsync(
                db,
                characterId,
                content,
                cancellationToken);
            bool pending = false;
            for (var index = 0; index < roll.Quantity; index++)
            {
                int itemOrdinal = checked(ordinal + index);
                if (equipmentFreeSlots > 0)
                {
                    db.CharacterItems.Add(
                        ItemInstancePersistenceFactory.CreateCharacterItem(
                            characterId,
                            definition,
                            mutationId,
                            "LOOT_CONTAINER",
                            roll.ItemId,
                            itemOrdinal,
                            now,
                            content.Package,
                            roll.SourceQualityProfileId));
                    equipmentFreeSlots--;
                }
                else
                {
                    db.PendingLootItems.Add(
                        ItemInstancePersistenceFactory.CreatePendingLootItem(
                            characterId,
                            definition,
                            mutationId,
                            "LOOT_CONTAINER",
                            roll.ItemId,
                            itemOrdinal,
                            now,
                            content.Package,
                            roll.SourceQualityProfileId));
                    pending = true;
                }
            }

            return pending;
        }

        int remaining = roll.Quantity;
        CharacterItem[] stacks = await db.CharacterItems
            .Where(item => item.CharacterId == characterId
                && item.ItemDefinitionId == definition.Id
                && item.DefinitionVersion == definition.Version
                && item.Quantity < definition.MaxStack
                && item.TransactionLockId == null)
            .OrderBy(item => item.AcquiredAtUtc)
            .ToArrayAsync(cancellationToken);
        foreach (CharacterItem stack in stacks)
        {
            if (remaining <= 0) break;
            int amount = Math.Min(
                definition.MaxStack - stack.Quantity,
                remaining);
            if (amount <= 0) continue;
            stack.AddQuantity(amount, definition.MaxStack);
            remaining -= amount;
        }

        int freeSlots = await InventoryCapacity.FreeSlotsAsync(
            db,
            characterId,
            content,
            cancellationToken);
        while (remaining > 0 && freeSlots > 0)
        {
            int quantity = Math.Min(definition.MaxStack, remaining);
            db.CharacterItems.Add(new CharacterItem(
                Guid.CreateVersion7(),
                characterId,
                definition.Id,
                quantity,
                now,
                definition.Version));
            remaining -= quantity;
            freeSlots--;
        }

        if (remaining <= 0)
            return false;

        db.PendingLootItems.Add(new PendingLootItem(
            Guid.CreateVersion7(),
            characterId,
            mutationId,
            definition.Id,
            remaining,
            definition.Version,
            now));
        return true;
    }

    private static int RollInclusive(
        int min,
        int max,
        SeededGameRandom random)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(min);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, min);
        if (min == max) return min;

        int range = checked(max - min + 1);
        int offset = (int)decimal.Floor(random.NextUnit() * range);
        return checked(min + Math.Min(offset, range - 1));
    }

    private static int SeedToInt32(Guid mutationId)
    {
        Span<byte> bytes = stackalloc byte[16];
        mutationId.TryWriteBytes(bytes);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static string Fingerprint(Guid characterItemId)
    {
        string canonical = $"{OperationType}:{characterItemId:N}";
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
