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
            GeneratedItemInstance? generated = ToGeneratedItem(item, definition);
            ItemDefinition effectiveDefinition = generated is null
                ? definition
                : ItemInstanceGenerator.ApplyGeneratedAffixes(
                    definition,
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
                generated);
        }).ToArray();

        Dictionary<EquipmentSlot, InventoryItemSnapshot> equipped = snapshots
            .Where(item => item.EquippedSlot.HasValue)
            .ToDictionary(item => item.EquippedSlot!.Value);
        return new InventorySnapshot(snapshots, equipped);
    }


    private static GeneratedItemInstance? ToGeneratedItem(
        CharacterItem item,
        ItemDefinition definition)
    {
        if (!item.IsProcedurallyGenerated
            || !item.ItemLevel.HasValue
            || !item.MinimumTemplateItemPower.HasValue
            || !item.ActualItemPower.HasValue
            || !item.MaxTemplateItemPower.HasValue
            || !item.RollQuality.HasValue
            || !item.Stars.HasValue)
        {
            return null;
        }

        return new GeneratedItemInstance(
            item.ItemLevel.Value,
            item.Affixes
                .OrderBy(affix => affix.GenerationOrdinal)
                .Select(affix => affix.ToGeneratedAffix())
                .ToArray(),
            item.MinimumTemplateItemPower.Value,
            item.ActualItemPower.Value,
            item.MaxTemplateItemPower.Value,
            item.RollQuality.Value,
            item.Stars.Value,
            item.IsPerfect,
            item.PerfectOrigin,
            item.GeneratedPrefixId,
            item.GeneratedSuffixId,
            item.GeneratedDisplayName ?? definition.Name,
            item.GenerationVersion);
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
