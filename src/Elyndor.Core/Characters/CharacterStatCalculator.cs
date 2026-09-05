using Elyndor.Core.Content;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Characters;

public sealed record CharacterStatContribution(string Source, decimal Value);

public sealed record CharacterStatBreakdown(
    decimal FinalValue,
    IReadOnlyList<CharacterStatContribution> Contributions);

public sealed record CharacterStatCalculation(
    CharacterStats Stats,
    IReadOnlyDictionary<string, CharacterStatBreakdown> Breakdown);

public sealed record CharacterEquipmentDerivedModifiers(
    decimal MaxHpFlat = 0,
    decimal AttackPowerFlat = 0,
    decimal SpellPowerFlat = 0,
    decimal CriticalChancePercent = 0,
    decimal CriticalDamagePercent = 0,
    decimal AccuracyPercent = 0,
    decimal AttackSpeedPercent = 0,
    decimal ArmorFlat = 0,
    decimal MagicResistanceFlat = 0,
    decimal DodgePercent = 0,
    decimal ArmorPenetrationPercent = 0,
    decimal MagicPenetrationPercent = 0);

public sealed class CharacterStatCalculator(
    StatFormulaProfile formula,
    IReadOnlyList<ClassProfile> profiles)
{
    public CharacterStats Calculate(
        string classId,
        int level,
        CharacterStatInputs? inputs = null) =>
        CalculateDetailed(classId, level, inputs).Stats;

    public CharacterStatCalculation CalculateDetailed(
        string classId,
        int level,
        CharacterStatInputs? inputs = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        ClassProfile profile = profiles.Single(candidate =>
            string.Equals(candidate.Id, classId, StringComparison.Ordinal));
        decimal completedLevels = level - 1;
        PrimaryStats levelGrowth = new(
            profile.LevelGrowth.Strength * completedLevels,
            profile.LevelGrowth.Agility * completedLevels,
            profile.LevelGrowth.Intellect * completedLevels,
            profile.LevelGrowth.Stamina * completedLevels);
        PrimaryStats classStats = Add(profile.BaseStats, levelGrowth);
        CharacterStatInputs sources = inputs ?? CharacterStatInputs.Empty;
        PrimaryStats beforeTalents = Add(classStats, sources.Equipment);
        PrimaryStats talentPercentages = new(
            beforeTalents.Strength * sources.TalentPercentages.Strength / 100,
            beforeTalents.Agility * sources.TalentPercentages.Agility / 100,
            beforeTalents.Intellect * sources.TalentPercentages.Intellect / 100,
            beforeTalents.Stamina * sources.TalentPercentages.Stamina / 100);
        PrimaryStats primary = Add(beforeTalents, sources.Talents, talentPercentages, sources.Effects);

        TalentStatModifiers talent = sources.TalentDerived;
        CharacterEquipmentDerivedModifiers equipmentDerived = sources.EquipmentDerived;
        decimal maxHpBeforeTalent = formula.MaxHpBase
            + (primary.Stamina * formula.MaxHpPerStamina)
            + equipmentDerived.MaxHpFlat;
        decimal attackPowerBeforeTalent = (primary.Strength * formula.AttackPowerPerStrength)
            + (primary.Agility * formula.AttackPowerPerAgility)
            + equipmentDerived.AttackPowerFlat;
        decimal spellPower = (primary.Intellect * formula.SpellPowerPerIntellect)
            + equipmentDerived.SpellPowerFlat;
        decimal armorBeforeTalent = (primary.Stamina * formula.ArmorPerStamina)
            + (primary.Strength * formula.ArmorPerStrength)
            + equipmentDerived.ArmorFlat;
        decimal magicResistanceBeforeTalent = (primary.Stamina * formula.MagicResistancePerStamina)
            + (primary.Intellect * formula.MagicResistancePerIntellect)
            + equipmentDerived.MagicResistanceFlat;

        decimal maxHp = ApplyPercent(maxHpBeforeTalent, talent.MaxHpPercent);
        decimal attackPower = ApplyPercent(attackPowerBeforeTalent, talent.AttackPowerPercent);
        decimal criticalChance = decimal.Clamp(
            formula.CriticalChanceBase
                + (primary.Agility * formula.CriticalChancePerAgility)
                + equipmentDerived.CriticalChancePercent
                + talent.CriticalChancePercent,
            0,
            100);
        decimal criticalDamage = formula.CriticalDamageBase
            + equipmentDerived.CriticalDamagePercent
            + talent.CriticalDamagePercent;
        decimal accuracy = decimal.Clamp(
            formula.AccuracyBase
                + equipmentDerived.AccuracyPercent
                + talent.AccuracyPercent,
            0,
            100);
        decimal attackSpeedPercent = talent.AttackSpeedPercent + equipmentDerived.AttackSpeedPercent;
        decimal attackSpeed = ApplyPercent(formula.AttackSpeedBase, attackSpeedPercent);
        decimal armor = ApplyPercent(armorBeforeTalent, talent.ArmorPercent);
        decimal magicResistance = ApplyPercent(magicResistanceBeforeTalent, talent.MagicResistancePercent);
        decimal armorPenetration = equipmentDerived.ArmorPenetrationPercent
            + talent.ArmorPenetrationPercent;
        decimal magicPenetration = equipmentDerived.MagicPenetrationPercent;
        decimal dodge = decimal.Clamp(
            primary.Agility * formula.DodgePerAgility
                + talent.DodgePercent
                + equipmentDerived.DodgePercent,
            0,
            100);

        CharacterStats stats = new(
            primary.Strength,
            primary.Agility,
            primary.Intellect,
            primary.Stamina,
            maxHp,
            attackPower,
            spellPower,
            criticalChance,
            criticalDamage,
            accuracy,
            armorPenetration,
            magicPenetration,
            attackSpeed,
            armor,
            magicResistance,
            dodge);

        Dictionary<string, CharacterStatBreakdown> breakdown = new(StringComparer.Ordinal)
        {
            ["strength"] = PrimaryBreakdown(
                stats.Strength, profile.BaseStats.Strength, levelGrowth.Strength,
                sources.Equipment.Strength, sources.Talents.Strength,
                talentPercentages.Strength, sources.Effects.Strength),
            ["agility"] = PrimaryBreakdown(
                stats.Agility, profile.BaseStats.Agility, levelGrowth.Agility,
                sources.Equipment.Agility, sources.Talents.Agility,
                talentPercentages.Agility, sources.Effects.Agility),
            ["intellect"] = PrimaryBreakdown(
                stats.Intellect, profile.BaseStats.Intellect, levelGrowth.Intellect,
                sources.Equipment.Intellect, sources.Talents.Intellect,
                talentPercentages.Intellect, sources.Effects.Intellect),
            ["stamina"] = PrimaryBreakdown(
                stats.Stamina, profile.BaseStats.Stamina, levelGrowth.Stamina,
                sources.Equipment.Stamina, sources.Talents.Stamina,
                talentPercentages.Stamina, sources.Effects.Stamina),
            ["maxHp"] = Breakdown(stats.MaxHp,
                ("FORMULA_BASE", formula.MaxHpBase),
                ("STAMINA", primary.Stamina * formula.MaxHpPerStamina),
                ("EQUIPMENT_BONUS", equipmentDerived.MaxHpFlat),
                ("TALENT_BONUS", stats.MaxHp - maxHpBeforeTalent)),
            ["attackPower"] = Breakdown(stats.AttackPower,
                ("STRENGTH", primary.Strength * formula.AttackPowerPerStrength),
                ("AGILITY", primary.Agility * formula.AttackPowerPerAgility),
                ("EQUIPMENT_BONUS", equipmentDerived.AttackPowerFlat),
                ("TALENT_BONUS", stats.AttackPower - attackPowerBeforeTalent)),
            ["spellPower"] = Breakdown(stats.SpellPower,
                ("INTELLECT", primary.Intellect * formula.SpellPowerPerIntellect),
                ("EQUIPMENT_BONUS", equipmentDerived.SpellPowerFlat)),
            ["criticalChance"] = Breakdown(stats.CriticalChance,
                ("FORMULA_BASE", formula.CriticalChanceBase),
                ("AGILITY", primary.Agility * formula.CriticalChancePerAgility),
                ("EQUIPMENT_BONUS", equipmentDerived.CriticalChancePercent),
                ("TALENT_BONUS", talent.CriticalChancePercent)),
            ["criticalDamage"] = Breakdown(stats.CriticalDamage,
                ("FORMULA_BASE", formula.CriticalDamageBase),
                ("EQUIPMENT_BONUS", equipmentDerived.CriticalDamagePercent),
                ("TALENT_BONUS", talent.CriticalDamagePercent)),
            ["accuracy"] = Breakdown(stats.Accuracy,
                ("FORMULA_BASE", formula.AccuracyBase),
                ("EQUIPMENT_BONUS", equipmentDerived.AccuracyPercent),
                ("TALENT_BONUS", talent.AccuracyPercent)),
            ["armorPenetration"] = Breakdown(stats.ArmorPenetration,
                ("EQUIPMENT_BONUS", equipmentDerived.ArmorPenetrationPercent),
                ("TALENT_BONUS", talent.ArmorPenetrationPercent)),
            ["magicPenetration"] = Breakdown(stats.MagicPenetration,
                ("EQUIPMENT_BONUS", equipmentDerived.MagicPenetrationPercent)),
            ["attackSpeed"] = Breakdown(stats.AttackSpeed,
                ("FORMULA_BASE", formula.AttackSpeedBase),
                ("EQUIPMENT_BONUS", formula.AttackSpeedBase * equipmentDerived.AttackSpeedPercent / 100m),
                ("TALENT_BONUS", formula.AttackSpeedBase * talent.AttackSpeedPercent / 100m)),
            ["armor"] = Breakdown(stats.Armor,
                ("STAMINA", primary.Stamina * formula.ArmorPerStamina),
                ("STRENGTH", primary.Strength * formula.ArmorPerStrength),
                ("EQUIPMENT_BONUS", equipmentDerived.ArmorFlat),
                ("TALENT_BONUS", stats.Armor - armorBeforeTalent)),
            ["magicResistance"] = Breakdown(stats.MagicResistance,
                ("STAMINA", primary.Stamina * formula.MagicResistancePerStamina),
                ("INTELLECT", primary.Intellect * formula.MagicResistancePerIntellect),
                ("EQUIPMENT_BONUS", equipmentDerived.MagicResistanceFlat),
                ("TALENT_BONUS", stats.MagicResistance - magicResistanceBeforeTalent)),
            ["dodge"] = Breakdown(stats.Dodge,
                ("AGILITY", primary.Agility * formula.DodgePerAgility),
                ("EQUIPMENT_BONUS", equipmentDerived.DodgePercent),
                ("TALENT_BONUS", talent.DodgePercent))
        };

        return new CharacterStatCalculation(stats, breakdown);
    }

    private static CharacterStatBreakdown PrimaryBreakdown(
        decimal finalValue,
        decimal classBase,
        decimal levelGrowth,
        decimal equipment,
        decimal talentFlat,
        decimal talentPercent,
        decimal effects) =>
        Breakdown(finalValue,
            ("CLASS_BASE", classBase),
            ("LEVEL_GROWTH", levelGrowth),
            ("EQUIPMENT", equipment),
            ("TALENT_FLAT", talentFlat),
            ("TALENT_PERCENT", talentPercent),
            ("EFFECTS", effects));

    private static CharacterStatBreakdown Breakdown(
        decimal finalValue,
        params (string Source, decimal Value)[] contributions) =>
        new(
            finalValue,
            contributions
                .Where(contribution => contribution.Value != 0 || contribution.Source == "FORMULA_BASE")
                .Select(contribution => new CharacterStatContribution(contribution.Source, contribution.Value))
                .ToArray());

    private static PrimaryStats Add(PrimaryStats first, params PrimaryStats[] sources) =>
        sources.Aggregate(
            first,
            (total, source) => new PrimaryStats(
                total.Strength + source.Strength,
                total.Agility + source.Agility,
                total.Intellect + source.Intellect,
                total.Stamina + source.Stamina));

    private static decimal ApplyPercent(decimal value, decimal percentage) =>
        value * (1 + percentage / 100m);
}

public sealed record CharacterStatInputs(
    PrimaryStats Equipment,
    PrimaryStats Talents,
    PrimaryStats Effects,
    TalentPrimaryStatPercentages TalentPercentages)
{
    private static readonly PrimaryStats EmptyStats = new(0, 0, 0, 0);

    public static CharacterStatInputs Empty { get; } =
        new(EmptyStats, EmptyStats, EmptyStats, TalentPrimaryStatPercentages.Empty);

    public TalentStatModifiers TalentDerived { get; init; } = new();
    public CharacterEquipmentDerivedModifiers EquipmentDerived { get; init; } = new();
}
