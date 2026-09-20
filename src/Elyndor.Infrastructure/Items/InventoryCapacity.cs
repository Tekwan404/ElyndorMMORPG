using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Items;

public static class InventoryCapacity
{
    public const int DefaultCapacity = 100;

    public static int Resolve(GameContentSnapshot contentSnapshot) =>
        Resolve(contentSnapshot.Package);

    public static int Resolve(GameContentPackage package) =>
        Math.Max(1, package.InventoryProfile?.DefaultCapacity ?? DefaultCapacity);

    public static Task<int> CountUsedSlotsAsync(
        GameDbContext dbContext,
        Guid characterId,
        CancellationToken cancellationToken) =>
        dbContext.CharacterItems
            .AsNoTracking()
            .Where(item => item.CharacterId == characterId)
            .Where(item => !dbContext.CharacterEquipment
                .Any(equipment =>
                    equipment.CharacterId == characterId
                    && equipment.CharacterItemId == item.Id))
            .CountAsync(cancellationToken);

    public static async Task<bool> CanAddAsync(
        GameDbContext dbContext,
        Guid characterId,
        ItemDefinition definition,
        int quantity,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        int capacity = Resolve(contentSnapshot);
        int used = await CountUsedSlotsAsync(
            dbContext,
            characterId,
            cancellationToken);
        int required = await AdditionalSlotsRequiredAsync(
            dbContext,
            characterId,
            definition,
            quantity,
            cancellationToken);
        return used + required <= capacity;
    }

    public static async Task<int> FreeSlotsAsync(
        GameDbContext dbContext,
        Guid characterId,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        int projectedUsed = await CountProjectedUsedSlotsAsync(
            dbContext,
            characterId,
            cancellationToken);
        return Math.Max(0, Resolve(contentSnapshot) - projectedUsed);
    }

    public static async Task<int> AdditionalSlotsRequiredAsync(
        GameDbContext dbContext,
        Guid characterId,
        ItemDefinition definition,
        int quantity,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        if (!definition.Stackable)
            return quantity;

        int freeUnits = await dbContext.CharacterItems
            .AsNoTracking()
            .Where(item =>
                item.CharacterId == characterId
                && item.ItemDefinitionId == definition.Id
                && item.DefinitionVersion == definition.Version
                && item.Quantity < definition.MaxStack)
            .Where(item => !dbContext.CharacterEquipment
                .Any(equipment =>
                    equipment.CharacterId == characterId
                    && equipment.CharacterItemId == item.Id))
            .SumAsync(
                item => definition.MaxStack - item.Quantity,
                cancellationToken);
        int remaining = Math.Max(0, quantity - freeUnits);
        return remaining == 0
            ? 0
            : (remaining + definition.MaxStack - 1) / definition.MaxStack;
    }

    private static async Task<int> CountProjectedUsedSlotsAsync(
        GameDbContext dbContext,
        Guid characterId,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> itemIds = (await dbContext.CharacterItems
                .AsNoTracking()
                .Where(item => item.CharacterId == characterId)
                .Select(item => item.Id)
                .ToArrayAsync(cancellationToken))
            .ToHashSet();

        Dictionary<Guid, int> equippedCounts = (await dbContext.CharacterEquipment
                .AsNoTracking()
                .Where(equipment => equipment.CharacterId == characterId)
                .Select(equipment => equipment.CharacterItemId)
                .ToArrayAsync(cancellationToken))
            .GroupBy(itemId => itemId)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var entry in dbContext.ChangeTracker.Entries<CharacterItem>())
        {
            if (entry.Entity.CharacterId != characterId)
                continue;

            if (entry.State == EntityState.Added)
                itemIds.Add(entry.Entity.Id);
            else if (entry.State == EntityState.Deleted)
                itemIds.Remove(entry.Entity.Id);
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<CharacterEquipment>())
        {
            if (entry.Entity.CharacterId != characterId)
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    Increment(equippedCounts, entry.Entity.CharacterItemId);
                    break;
                case EntityState.Deleted:
                    Decrement(equippedCounts, entry.Entity.CharacterItemId);
                    break;
                case EntityState.Modified:
                    Guid originalItemId = entry
                        .Property(equipment => equipment.CharacterItemId)
                        .OriginalValue;
                    Decrement(equippedCounts, originalItemId);
                    Increment(equippedCounts, entry.Entity.CharacterItemId);
                    break;
            }
        }

        return itemIds.Count(itemId => !equippedCounts.ContainsKey(itemId));
    }

    private static void Increment(Dictionary<Guid, int> counts, Guid itemId) =>
        counts[itemId] = counts.GetValueOrDefault(itemId) + 1;

    private static void Decrement(Dictionary<Guid, int> counts, Guid itemId)
    {
        if (!counts.TryGetValue(itemId, out int count))
            return;
        if (count <= 1)
            counts.Remove(itemId);
        else
            counts[itemId] = count - 1;
    }
}
