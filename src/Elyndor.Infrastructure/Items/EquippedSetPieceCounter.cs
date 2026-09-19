namespace Elyndor.Infrastructure.Items;

/// <summary>
/// Counts how many pieces of each equipment set a character currently wears. The result is
/// what drives both the static set bonuses and the set passive runtime, so it must count an
/// item once even when the same item occupies more than one equipment slot.
/// </summary>
public static class EquippedSetPieceCounter
{
    public static IReadOnlyDictionary<string, int> Count(InventorySnapshot inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        return Count(inventory.Equipped.Values);
    }

    public static IReadOnlyDictionary<string, int> Count(
        IEnumerable<InventoryItemSnapshot> equippedItems)
    {
        ArgumentNullException.ThrowIfNull(equippedItems);

        return equippedItems
            .DistinctBy(item => item.Id)
            .Select(item => item.Definition.SetId)
            .Where(setId => !string.IsNullOrWhiteSpace(setId))
            .GroupBy(setId => setId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }
}
