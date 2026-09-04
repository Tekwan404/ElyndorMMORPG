using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.Items;

internal static class InventorySnapshotReader
{
    public static async Task<InventorySnapshot> ReadAsync(
        GameDbContext dbContext,
        GameContentPackage content,
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
        Dictionary<string, ItemDefinition> definitions =
            (content.Items ?? throw new InvalidOperationException("Item content is required."))
                .ToDictionary(item => item.Id, StringComparer.Ordinal);

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
}
