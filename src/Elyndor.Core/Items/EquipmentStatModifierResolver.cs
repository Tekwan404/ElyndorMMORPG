using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

public sealed record EquipmentModifierSummary(
    PrimaryStats PrimaryStats,
    decimal MaxHpFlat,
    decimal AttackPowerFlat,
    decimal SpellPowerFlat,
    decimal CriticalChancePercent,
    decimal CriticalDamagePercent,
    decimal AccuracyPercent,
    decimal AttackSpeedPercent,
    decimal ArmorFlat,
    decimal MagicResistanceFlat,
    decimal DodgePercent,
    decimal ArmorPenetrationPercent,
    decimal MagicPenetrationPercent,
    decimal MaxResourceFlat,
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
            .GroupBy(item => CanonicalSlot(item.Slot))
            .Select(group => group
                .OrderByDescending(item => item.Slot == CanonicalSlot(item.Slot))
                .First())
            .ToArray();

        decimal strength = items.Sum(item => item.Stats.Strength);
        decimal agility = items.Sum(item => item.Stats.Agility);
        decimal intellect = items.Sum(item => item.Stats.Intellect);
        decimal stamina = items.Sum(item => item.Stats.Stamina);
        decimal maxHpFlat = items.Sum(item => item.MaxHpFlat);
        decimal attackPowerFlat = items.Sum(item => item.AttackPowerFlat);
        decimal spellPowerFlat = items.Sum(item => item.SpellPowerFlat);
        decimal criticalChancePercent = items.Sum(item => item.CriticalChancePercent);
        decimal criticalDamagePercent = items.Sum(item => item.CriticalDamagePercent);
        decimal accuracyPercent = items.Sum(item => item.AccuracyPercent);
        decimal attackSpeedPercent = items.Sum(item => item.AttackSpeedPercent);
        decimal armorFlat = items.Sum(item => item.ArmorFlat);
        decimal magicResistanceFlat = items.Sum(item => item.MagicResistanceFlat);
        decimal dodgePercent = items.Sum(item => item.DodgePercent);
        decimal armorPenetrationPercent = items.Sum(item => item.ArmorPenetrationPercent);
        decimal magicPenetrationPercent = items.Sum(item => item.MagicPenetrationPercent);
        decimal maxResourceFlat = items.Sum(item => item.MaxResourceFlat);

        decimal? weaponBaseAttackIntervalSeconds = items
            .Where(item => CanonicalSlot(item.Slot) == EquipmentSlot.MainHand)
            .Select(item => item.WeaponBaseAttackIntervalSeconds)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Cast<decimal?>()
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
                maxHpFlat += bonus.MaxHpFlat;
                attackPowerFlat += bonus.AttackPowerFlat;
                spellPowerFlat += bonus.SpellPowerFlat;
                criticalChancePercent += bonus.CriticalChancePercent;
                criticalDamagePercent += bonus.CriticalDamagePercent;
                accuracyPercent += bonus.AccuracyPercent;
                attackSpeedPercent += bonus.AttackSpeedPercent;
                armorFlat += bonus.ArmorFlat;
                magicResistanceFlat += bonus.MagicResistanceFlat;
                dodgePercent += bonus.DodgePercent;
                armorPenetrationPercent += bonus.ArmorPenetrationPercent;
                magicPenetrationPercent += bonus.MagicPenetrationPercent;
                maxResourceFlat += bonus.MaxResourceFlat;
            }
        }

        return new EquipmentModifierSummary(
            new PrimaryStats(strength, agility, intellect, stamina),
            maxHpFlat,
            attackPowerFlat,
            spellPowerFlat,
            criticalChancePercent,
            criticalDamagePercent,
            accuracyPercent,
            attackSpeedPercent,
            armorFlat,
            magicResistanceFlat,
            dodgePercent,
            armorPenetrationPercent,
            magicPenetrationPercent,
            maxResourceFlat,
            weaponBaseAttackIntervalSeconds,
            activeBonuses);
    }

    private static EquipmentSlot? CanonicalSlot(EquipmentSlot? slot) =>
        slot switch
        {
            EquipmentSlot.Weapon => EquipmentSlot.MainHand,
            EquipmentSlot.Boots => EquipmentSlot.Feet,
            EquipmentSlot.Accessory => EquipmentSlot.Amulet,
            _ => slot
        };
}
