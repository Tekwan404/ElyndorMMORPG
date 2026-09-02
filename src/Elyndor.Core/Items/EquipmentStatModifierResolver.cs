using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

public static class EquipmentStatModifierResolver
{
    public static PrimaryStats Resolve(IEnumerable<ItemDefinition> equippedItems)
    {
        ArgumentNullException.ThrowIfNull(equippedItems);

        decimal strength = 0;
        decimal agility = 0;
        decimal intellect = 0;
        decimal stamina = 0;

        foreach (ItemDefinition item in equippedItems)
        {
            if (item.Type != ItemType.Equipment)
                continue;

            strength += item.Stats.Strength;
            agility += item.Stats.Agility;
            intellect += item.Stats.Intellect;
            stamina += item.Stats.Stamina;
        }

        return new PrimaryStats(strength, agility, intellect, stamina);
    }
}
