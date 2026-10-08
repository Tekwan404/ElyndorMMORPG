using System.Globalization;
using Elyndor.Contracts.Items;
using Elyndor.Core.Items;

namespace Elyndor.Server.Items;

internal static class EquipmentSetPresentation
{
    public static EquipmentSetSummaryResponse? ToResponse(EquipmentSetDefinition? set, int total) =>
        set is null ? null : new(set.Name, total, set.Bonuses.OrderBy(b => b.RequiredPieces)
            .Select(b => new EquipmentSetBonusResponse(b.RequiredPieces, Describe(b))).ToArray());

    private static string Describe(EquipmentSetBonusDefinition bonus)
    {
        if (!string.IsNullOrWhiteSpace(bonus.Description)) return bonus.Description;
        List<string> stats = [];
        Add(bonus.MaxHpFlat, "HP");
        Add(bonus.MaxResourceFlat, "ресурс");
        Add(bonus.AttackPowerFlat, "Attack Power");
        Add(bonus.SpellPowerFlat, "Spell Power");
        Add(bonus.ArmorFlat, "Armor");
        Add(bonus.MagicResistanceFlat, "Magic Resistance");
        Add(bonus.CriticalChancePercent, "Crit", "%");
        Add(bonus.CriticalDamagePercent, "Crit Damage", "%");
        Add(bonus.AccuracyPercent, "Accuracy", "%");
        Add(bonus.DodgePercent, "Dodge", "%");
        Add(bonus.AttackSpeedPercent, "Attack Speed", "%");
        Add(bonus.ArmorPenetrationPercent, "Armor Pen", "%");
        Add(bonus.MagicPenetrationPercent, "Magic Pen", "%");
        Add(bonus.PhysicalVampirismPercent, "Физический вампиризм", "%");
        Add(bonus.MagicalVampirismPercent, "Магический вампиризм", "%");
        Add(bonus.UniversalVampirismPercent, "Универсальный вампиризм", "%");
        return string.Join(", ", stats);

        void Add(decimal value, string name, string suffix = "")
        {
            if (value != 0) stats.Add($"+{value.ToString("0.##", CultureInfo.InvariantCulture)}{suffix} {name}");
        }
    }
}
