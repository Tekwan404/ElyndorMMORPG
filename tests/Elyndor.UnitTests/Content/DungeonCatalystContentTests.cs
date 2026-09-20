using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class DungeonCatalystContentTests
{
    private const string CatalystItemId = "DUNGEON_CATALYST";

    [Fact]
    public async Task ComposedPackageKeepsRequiredDungeonCatalystBossDrops()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            RepositoryContentPath());

        ItemDefinition catalyst = Assert.Single(
            package.Items!,
            item => string.Equals(item.Id, CatalystItemId, StringComparison.Ordinal));
        Assert.Equal(ItemType.Material, catalyst.Type);
        Assert.False(catalyst.PremiumEligible);

        AssertDrop(package, "ANCIENT_MINE_BOSS_LOOT", 0.35m, 1, 1);
        AssertDrop(package, "ECLIPSED_CITADEL_BOSS_LOOT", 0.75m, 1, 2);
    }

    [Fact]
    public async Task ValidationRejectsHighEndCatalystWithoutAnyLootSource()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            RepositoryContentPath());

        GameContentPackage withoutCatalystSources = package with
        {
            LootTables = package.LootTables!
                .Select(table => table with
                {
                    Entries = table.Entries
                        .Where(entry => !string.Equals(
                            entry.ItemId,
                            CatalystItemId,
                            StringComparison.Ordinal))
                        .ToArray(),
                    SelectionGroups = (table.SelectionGroups ?? [])
                        .Select(group => group with
                        {
                            Entries = group.Entries
                                .Where(entry => !string.Equals(
                                    entry.ItemId,
                                    CatalystItemId,
                                    StringComparison.Ordinal))
                                .ToArray()
                        })
                        .ToArray()
                })
                .ToArray()
        };

        IReadOnlyList<ContentValidationError> errors =
            ContentValidationPipeline.Default.Validate(withoutCatalystSources);

        Assert.Contains(errors, error =>
            error.Code == "MISSING_ITEM_STAR_UPGRADE_CATALYST_SOURCE");
    }

    private static void AssertDrop(
        GameContentPackage package,
        string lootTableId,
        decimal dropChance,
        int minQuantity,
        int maxQuantity)
    {
        LootTableDefinition table = Assert.Single(
            package.LootTables!,
            candidate => string.Equals(candidate.Id, lootTableId, StringComparison.Ordinal));
        LootTableEntry entry = Assert.Single(
            table.Entries,
            candidate => string.Equals(candidate.ItemId, CatalystItemId, StringComparison.Ordinal));

        Assert.Equal(dropChance, entry.DropChance);
        Assert.Equal(minQuantity, entry.MinQuantity);
        Assert.Equal(maxQuantity, entry.MaxQuantity);
    }

    private static string RepositoryContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
