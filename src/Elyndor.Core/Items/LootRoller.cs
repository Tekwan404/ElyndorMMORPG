using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Items;

public sealed record LootRoll(
    string ItemId,
    int Quantity,
    string SourceQualityProfileId = "NORMAL");

public static class LootRoller
{
    public static IReadOnlyList<LootRoll> Roll(
        LootTableDefinition table,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(random);

        List<LootRoll> result = [];
        foreach (LootTableEntry entry in table.Entries)
        {
            if (entry.DropChance <= 0 || random.NextUnit() >= entry.DropChance)
                continue;

            int quantity = entry.MinQuantity;
            if (entry.MaxQuantity > entry.MinQuantity)
            {
                int range = checked(entry.MaxQuantity - entry.MinQuantity + 1);
                quantity += (int)decimal.Floor(random.NextUnit() * range);
                quantity = Math.Min(quantity, entry.MaxQuantity);
            }

            result.Add(new LootRoll(entry.ItemId, quantity));
        }

        foreach (LootSelectionGroup group in table.SelectionGroups ?? [])
        {
            bool equalWeight = string.Equals(group.SelectionMode, "EqualWeight", StringComparison.Ordinal);
            decimal totalWeight = equalWeight
                ? group.Entries.Count
                : group.Entries.Sum(entry => entry.Weight);
            for (int roll = 0; roll < group.Rolls; roll++)
            {
                decimal selection = random.NextUnit() * totalWeight;
                LootSelectionEntry selected = group.Entries[^1];
                foreach (LootSelectionEntry entry in group.Entries)
                {
                    selection -= equalWeight ? 1m : entry.Weight;
                    if (selection < 0)
                    {
                        selected = entry;
                        break;
                    }
                }

                int quantity = selected.MinQuantity;
                if (selected.MaxQuantity > selected.MinQuantity)
                {
                    int range = checked(selected.MaxQuantity - selected.MinQuantity + 1);
                    quantity += (int)decimal.Floor(random.NextUnit() * range);
                    quantity = Math.Min(quantity, selected.MaxQuantity);
                }

                result.Add(new LootRoll(selected.ItemId, quantity));
            }
        }

        return result;
    }
}
