using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Items;

public sealed record InventoryCapacityState(
    int BaseCapacity,
    int ArtifactCapacityBonus,
    int Capacity,
    int UsedSlots)
{
    public int FreeSlots => Math.Max(0, Capacity - UsedSlots);
    public bool IsOverflow => UsedSlots > Capacity;
}

public static class InventoryCapacity
{
    public const int DefaultCapacity = 30;

    public static int Resolve(GameContentSnapshot contentSnapshot) =>
        Resolve(contentSnapshot.Package);

    public static int Resolve(GameContentPackage package) =>
        Math.Max(1, package.InventoryProfile?.DefaultCapacity ?? DefaultCapacity);

    public static async Task<int> ResolveAsync(
        GameDbContext dbContext,
        Guid characterId,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        InventoryCapacityState state = await GetStateAsync(
            dbContext,
            characterId,
            contentSnapshot,
            cancellationToken);
        return state.Capacity;
    }

    public static async Task<InventoryCapacityState> GetStateAsync(
        GameDbContext dbContext,
        Guid characterId,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contentSnapshot);

        int baseCapacity = Resolve(contentSnapshot);
        int usedSlots = await CountProjectedUsedSlotsAsync(
            dbContext,
            characterId,
            cancellationToken);
        Guid? artifactItemId = await ResolveProjectedSpatialArtifactItemIdAsync(
            dbContext,
            characterId,
            cancellationToken);
        int artifactBonus = artifactItemId.HasValue
            ? await ResolveArtifactBonusAsync(
                dbContext,
                artifactItemId.Value,
                contentSnapshot,
                cancellationToken)
            : 0;

        return new InventoryCapacityState(
            baseCapacity,
            artifactBonus,
            checked(baseCapacity + artifactBonus),
            usedSlots);
    }

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
            .Where(item => !dbContext.CharacterSpatialArtifacts
                .Any(artifact =>
                    artifact.CharacterId == characterId
                    && artifact.CharacterItemId == item.Id))
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
        InventoryCapacityState state = await GetStateAsync(
            dbContext,
            characterId,
            contentSnapshot,
            cancellationToken);
        int required = await AdditionalSlotsRequiredAsync(
            dbContext,
            characterId,
            definition,
            quantity,
            cancellationToken);
        return state.UsedSlots + required <= state.Capacity;
    }

    public static async Task<int> FreeSlotsAsync(
        GameDbContext dbContext,
        Guid characterId,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        InventoryCapacityState state = await GetStateAsync(
            dbContext,
            characterId,
            contentSnapshot,
            cancellationToken);
        return state.FreeSlots;
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
            .Where(item => !dbContext.CharacterSpatialArtifacts
                .Any(artifact =>
                    artifact.CharacterId == characterId
                    && artifact.CharacterItemId == item.Id))
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

        Guid? persistedArtifactItemId = await dbContext.CharacterSpatialArtifacts
            .AsNoTracking()
            .Where(artifact => artifact.CharacterId == characterId)
            .Select(artifact => (Guid?)artifact.CharacterItemId)
            .SingleOrDefaultAsync(cancellationToken);
        if (persistedArtifactItemId.HasValue)
            Increment(equippedCounts, persistedArtifactItemId.Value);

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

            ApplyEquipmentEntry(equippedCounts, entry);
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<CharacterSpatialArtifact>())
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
                        .Property(artifact => artifact.CharacterItemId)
                        .OriginalValue;
                    Decrement(equippedCounts, originalItemId);
                    Increment(equippedCounts, entry.Entity.CharacterItemId);
                    break;
            }
        }

        return itemIds.Count(itemId => !equippedCounts.ContainsKey(itemId));
    }

    private static async Task<Guid?> ResolveProjectedSpatialArtifactItemIdAsync(
        GameDbContext dbContext,
        Guid characterId,
        CancellationToken cancellationToken)
    {
        Guid? itemId = await dbContext.CharacterSpatialArtifacts
            .AsNoTracking()
            .Where(artifact => artifact.CharacterId == characterId)
            .Select(artifact => (Guid?)artifact.CharacterItemId)
            .SingleOrDefaultAsync(cancellationToken);

        foreach (var entry in dbContext.ChangeTracker.Entries<CharacterSpatialArtifact>())
        {
            if (entry.Entity.CharacterId != characterId)
                continue;

            itemId = entry.State switch
            {
                EntityState.Added or EntityState.Modified => entry.Entity.CharacterItemId,
                EntityState.Deleted => null,
                _ => itemId
            };
        }

        return itemId;
    }

    private static async Task<int> ResolveArtifactBonusAsync(
        GameDbContext dbContext,
        Guid artifactItemId,
        GameContentSnapshot contentSnapshot,
        CancellationToken cancellationToken)
    {
        CharacterItem? trackedItem = dbContext.ChangeTracker.Entries<CharacterItem>()
            .Where(entry => entry.State != EntityState.Deleted)
            .Select(entry => entry.Entity)
            .FirstOrDefault(item => item.Id == artifactItemId);
        string? definitionId = trackedItem?.ItemDefinitionId;
        if (definitionId is null)
        {
            definitionId = await dbContext.CharacterItems
                .AsNoTracking()
                .Where(item => item.Id == artifactItemId)
                .Select(item => item.ItemDefinitionId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        if (definitionId is null
            || !contentSnapshot.Indexes.ItemsById.TryGetValue(definitionId, out ItemDefinition? definition)
            || definition.Type != ItemType.SpatialArtifact)
        {
            return 0;
        }

        return Math.Max(0, definition.InventoryCapacityBonus);
    }

    private static void ApplyEquipmentEntry(
        Dictionary<Guid, int> equippedCounts,
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<CharacterEquipment> entry)
    {
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
