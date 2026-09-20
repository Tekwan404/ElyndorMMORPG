using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public sealed class EnhancementCatalystSourceValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ItemStarUpgradeProfileDefinition? upgrades = context.Package.Itemization?.StarUpgrades;
        if (upgrades is null
            || upgrades.HighEndCatalystQuantity <= 0
            || string.IsNullOrWhiteSpace(upgrades.HighEndCatalystItemId))
        {
            return;
        }

        string catalystItemId = upgrades.HighEndCatalystItemId;
        ItemDefinition? catalyst = (context.Package.Items ?? []).FirstOrDefault(item =>
            string.Equals(item.Id, catalystItemId, StringComparison.Ordinal));
        if (catalyst?.Type != ItemType.Material)
            return;

        bool hasLootSource = (context.Package.LootTables ?? []).Any(table =>
            table.Entries.Any(entry =>
                string.Equals(entry.ItemId, catalystItemId, StringComparison.Ordinal))
            || (table.SelectionGroups ?? []).Any(group =>
                group.Entries.Any(entry =>
                    string.Equals(entry.ItemId, catalystItemId, StringComparison.Ordinal))));

        if (!hasLootSource)
        {
            context.Errors.Add(new(
                "MISSING_ITEM_STAR_UPGRADE_CATALYST_SOURCE",
                "itemization.starUpgrades.highEndCatalystItemId",
                $"High-end enhancement catalyst '{catalystItemId}' must have at least one loot source."));
        }
    }
}
