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
            .Include(item => item.Affixes)
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
            ItemDefinition definition = definitions.TryGetValue(
                    item.ItemDefinitionId,
                    out ItemDefinition? resolved)
                ? resolved
                : CreateOrphanedDefinition(item);

            EquipmentSlot? equippedSlot = equippedSlots.TryGetValue(item.Id, out EquipmentSlot slot)
                ? slot
                : null;
            GeneratedItemInstance? generated = ItemInstancePersistenceFactory.ToGeneratedInstance(
                item,
                definition,
                content.Itemization);

            // Enhancement is applied to structural template stats before generated affixes are
            // overlaid. This guarantees +N affects combat while random affixes remain unscaled.
            ItemDefinition enhancedDefinition = ItemEnhancementRules.ApplyStructuralEnhancement(
                definition,
                item.EnhancementLevel);
            ItemDefinition effectiveDefinition = generated is null
                ? enhancedDefinition
                : ItemInstanceGenerator.ApplyGeneratedAffixes(
                    enhancedDefinition,
                    generated.Affixes,
                    generated.DisplayName);

            return new InventoryItemSnapshot(
                item.Id,
                effectiveDefinition,
                item.Quantity,
                item.AcquiredAtUtc,
                equippedSlot,
                item.IsLocked,
                item.RolledPrimaryStats,
                generated,
                item.ReforgeCount,
                item.ReforgeSlotKey,
                item.TransactionLockId.HasValue,
                item.BindState);
        }).ToArray();

        Dictionary<EquipmentSlot, InventoryItemSnapshot> equipped = snapshots
            .Where(item => item.EquippedSlot.HasValue)
            .ToDictionary(item => item.EquippedSlot!.Value);
        return new InventorySnapshot(snapshots, equipped);
    }

    private static ItemDefinition CreateOrphanedDefinition(CharacterItem item) =>
        new(
            item.ItemDefinitionId,
            $"Устаревший предмет · {item.ItemDefinitionId}",
            ItemType.Material,
            ItemRarity.Common,
            1,
            true,
            Math.Max(2, item.Quantity),
            null,
            new PrimaryStats(0, 0, 0, 0),
            "Сохранённый предмет ссылается на удалённое определение контента. "
            + "Предмет сохранён без характеристик до восстановления его определения.",
            Version: item.DefinitionVersion);
}
