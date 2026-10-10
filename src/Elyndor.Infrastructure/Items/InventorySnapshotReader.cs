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
        Dictionary<string, EquipmentSetDefinition> equipmentSets =
            (content.EquipmentSets ?? [])
                .ToDictionary(set => set.Id, StringComparer.Ordinal);

        var setSizes = definitions.Values.Where(item => item.SetId is not null)
            .GroupBy(item => item.SetId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        InventoryItemSnapshot[] snapshots = items.Select(item =>
        {
            ItemDefinition definition = definitions.TryGetValue(
                    item.ItemDefinitionId,
                    out ItemDefinition? resolved)
                ? resolved
                : CreateOrphanedDefinition(item);

            if (!string.IsNullOrWhiteSpace(definition.SetId)
                && equipmentSets.TryGetValue(definition.SetId, out EquipmentSetDefinition? equipmentSet)
                && equipmentSet.AllowedClassIds is { Count: > 0 })
            {
                IReadOnlyList<string> effectiveClasses = definition.AllowedClassIds is { Count: > 0 }
                    ? definition.AllowedClassIds
                        .Intersect(equipmentSet.AllowedClassIds, StringComparer.Ordinal)
                        .ToArray()
                    : equipmentSet.AllowedClassIds;
                definition = definition with { AllowedClassIds = effectiveClasses };
            }

            EquipmentSlot? equippedSlot = equippedSlots.TryGetValue(item.Id, out EquipmentSlot slot)
                ? slot
                : null;
            GeneratedItemInstance? generated = ItemInstancePersistenceFactory.ToGeneratedInstance(
                item,
                definition,
                content.Itemization);

            // Family structural stats scale to the concrete generated item level first.
            // Enhancement then affects those structural stats, while random affixes remain unscaled.
            ItemDefinition familyDefinition = generated is not null && content.Itemization is { } itemization
                ? ItemFamilyScalingPolicy.Apply(definition, itemization, generated.ItemLevel)
                : definition;
            ItemDefinition enhancedDefinition = ItemEnhancementRules.ApplyStructuralEnhancement(
                familyDefinition,
                item.EnhancementLevel);
            ItemDefinition leveledDefinition = enhancedDefinition with
            {
                RequiredLevel = ItemRequiredLevelPolicy.Resolve(
                    definition,
                    item.ItemLevel,
                    content.LevelProgression?.MaxLevel)
            };
            ItemDefinition effectiveDefinition = generated is null
                ? leveledDefinition
                : ItemInstanceGenerator.ApplyGeneratedAffixes(
                    leveledDefinition,
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
                item.BindState,
                definition.SetId is { } setId ? equipmentSets.GetValueOrDefault(setId) : null,
                definition.SetId is { } sizeSetId ? setSizes.GetValueOrDefault(sizeSetId) : 0,
                item.SourceType);
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
