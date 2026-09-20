using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.Infrastructure.Content;

internal static class DungeonProgressionContentInvariants
{
    private const string CatalystItemId = "DUNGEON_CATALYST";

    private static readonly RequiredDungeonDrop[] RequiredDrops =
    [
        new("ANCIENT_MINE_BOSS_LOOT", 0.35m, 1, 1),
        new("ECLIPSED_CITADEL_BOSS_LOOT", 0.75m, 1, 2)
    ];

    internal static GameContentPackage Apply(GameContentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        bool catalystExists = (package.Items ?? []).Any(item =>
            string.Equals(item.Id, CatalystItemId, StringComparison.Ordinal));
        if (!catalystExists || package.LootTables is null)
            return package;

        Dictionary<string, RequiredDungeonDrop> requirements = RequiredDrops
            .ToDictionary(requirement => requirement.LootTableId, StringComparer.Ordinal);
        bool changed = false;

        LootTableDefinition[] lootTables = package.LootTables
            .Select(table =>
            {
                if (!requirements.TryGetValue(table.Id, out RequiredDungeonDrop? requirement))
                    return table;

                LootTableEntry expected = new(
                    CatalystItemId,
                    requirement.DropChance,
                    requirement.MinQuantity,
                    requirement.MaxQuantity);
                List<LootTableEntry> entries = table.Entries.ToList();
                int existingIndex = entries.FindIndex(entry =>
                    string.Equals(entry.ItemId, CatalystItemId, StringComparison.Ordinal));

                if (existingIndex >= 0)
                {
                    if (entries[existingIndex] == expected)
                        return table;

                    entries[existingIndex] = expected;
                }
                else
                {
                    entries.Add(expected);
                }

                changed = true;
                return table with { Entries = entries };
            })
            .ToArray();

        return changed
            ? package with { LootTables = lootTables }
            : package;
    }

    private sealed record RequiredDungeonDrop(
        string LootTableId,
        decimal DropChance,
        int MinQuantity,
        int MaxQuantity);
}
