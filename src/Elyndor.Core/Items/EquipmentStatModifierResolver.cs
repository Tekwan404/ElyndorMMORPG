using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

public sealed record EquipmentModifierSummary(
    PrimaryStats PrimaryStats,
    decimal AttackSpeedPercent,
    decimal DodgePercent,
    decimal? WeaponBaseAttackIntervalSeconds,
    IReadOnlyList<EquipmentSetBonusDefinition> ActiveSetBonuses);

public static class EquipmentStatModifierResolver
{
    public static PrimaryStats Resolve(IEnumerable<ItemDefinition> equippedItems) =>
        ResolveDetailed(equippedItems, []).PrimaryStats;

    public static EquipmentModifierSummary ResolveDetailed(
        IEnumerable<ItemDefinition> equippedItems,
        IEnumerable<EquipmentSetDefinition> equipmentSets)
    {
        ArgumentNullException.ThrowIfNull(equippedItems);
        ArgumentNullException.ThrowIfNull(equipmentSets);

        ItemDefinition[] items = equippedItems
            .Where(item => item.Type == ItemType.Equipment)
            .ToArray();

        decimal strength = items.Sum(item => item.Stats.Strength);
        decimal agility = items.Sum(item => item.Stats.Agility);
        decimal intellect = items.Sum(item => item.Stats.Intellect);
        decimal stamina = items.Sum(item => item.Stats.Stamina);
        decimal attackSpeedPercent = items.Sum(item => item.AttackSpeedPercent);
        decimal dodgePercent = items.Sum(item => item.DodgePercent);

        decimal? weaponBaseAttackIntervalSeconds = items
            .Where(item => item.Slot == EquipmentSlot.Weapon)
            .Select(item => item.WeaponBaseAttackIntervalSeconds)
            .SingleOrDefault();

        List<EquipmentSetBonusDefinition> activeBonuses = [];
        foreach (EquipmentSetDefinition set in equipmentSets)
        {
            int pieces = items.Count(item => string.Equals(item.SetId, set.Id, StringComparison.Ordinal));
            foreach (EquipmentSetBonusDefinition bonus in set.Bonuses
                         .Where(bonus => pieces >= bonus.RequiredPieces)
                         .OrderBy(bonus => bonus.RequiredPieces))
            {
                activeBonuses.Add(bonus);
                attackSpeedPercent += bonus.AttackSpeedPercent;
                dodgePercent += bonus.DodgePercent;
            }
        }

        return new EquipmentModifierSummary(
            new PrimaryStats(strength, agility, intellect, stamina),
            attackSpeedPercent,
            dodgePercent,
            weaponBaseAttackIntervalSeconds,
            activeBonuses);
    }
}
