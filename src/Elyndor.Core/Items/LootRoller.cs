using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Items;

public sealed record LootRoll(string ItemId, int Quantity);

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

        return result;
    }
}
