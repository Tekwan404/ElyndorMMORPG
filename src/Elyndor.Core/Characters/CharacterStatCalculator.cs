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

        decimal maxHpBeforeTalent = formula.MaxHpBase + (primary.Stamina * formula.MaxHpPerStamina);
        decimal attackPowerBeforeTalent = (primary.Strength * formula.AttackPowerPerStrength)
            + (primary.Agility * formula.AttackPowerPerAgility);
        decimal armorBeforeTalent = (primary.Stamina * formula.ArmorPerStamina)
            + (primary.Strength * formula.ArmorPerStrength);
        decimal magicResistanceBeforeTalent = (primary.Stamina * formula.MagicResistancePerStamina)
            + (primary.Intellect * formula.MagicResistancePerIntellect);
        TalentStatModifiers talent = sources.TalentDerived;

        decimal maxHp = ApplyPercent(maxHpBeforeTalent, talent.MaxHpPercent);
        decimal attackPower = ApplyPercent(attackPowerBeforeTalent, talent.AttackPowerPercent);
        decimal spellPower = primary.Intellect * formula.SpellPowerPerIntellect;
        decimal criticalChance = decimal.Clamp(
            formula.CriticalChanceBase
                + (primary.Agility * formula.CriticalChancePerAgility)
                + talent.CriticalChancePercent,
            0,
            100);
        decimal criticalDamage = formula.CriticalDamageBase + talent.CriticalDamagePercent;
        decimal accuracy = decimal.Clamp(formula.AccuracyBase + talent.AccuracyPercent, 0, 100);
        decimal attackSpeed = ApplyPercent(formula.AttackSpeedBase, talent.AttackSpeedPercent);
        decimal armor = ApplyPercent(armorBeforeTalent, talent.ArmorPercent);
        decimal magicResistance = ApplyPercent(magicResistanceBeforeTalent, talent.MagicResistancePercent);
        decimal dodge = decimal.Clamp(
            primary.Agility * formula.DodgePerAgility + talent.DodgePercent,
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
            talent.ArmorPenetrationPercent,
            0,
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
                ("TALENT_BONUS", stats.MaxHp - maxHpBeforeTalent)),
            ["attackPower"] = Breakdown(stats.AttackPower,
                ("STRENGTH", primary.Strength * formula.AttackPowerPerStrength),
                ("AGILITY", primary.Agility * formula.AttackPowerPerAgility),
                ("TALENT_BONUS", stats.AttackPower - attackPowerBeforeTalent)),
            ["spellPower"] = Breakdown(stats.SpellPower,
                ("INTELLECT", spellPower)),
            ["criticalChance"] = Breakdown(stats.CriticalChance,
                ("FORMULA_BASE", formula.CriticalChanceBase),
                ("AGILITY", primary.Agility * formula.CriticalChancePerAgility),
                ("TALENT_BONUS", talent.CriticalChancePercent)),
            ["criticalDamage"] = Breakdown(stats.CriticalDamage,
                ("FORMULA_BASE", formula.CriticalDamageBase),
                ("TALENT_BONUS", talent.CriticalDamagePercent)),
            ["accuracy"] = Breakdown(stats.Accuracy,
                ("FORMULA_BASE", formula.AccuracyBase),
                ("TALENT_BONUS", talent.AccuracyPercent)),
            ["armorPenetration"] = Breakdown(stats.ArmorPenetration,
                ("TALENT_BONUS", talent.ArmorPenetrationPercent)),
            ["magicPenetration"] = Breakdown(stats.MagicPenetration,
                ("FORMULA_BASE", 0)),
            ["attackSpeed"] = Breakdown(stats.AttackSpeed,
                ("FORMULA_BASE", formula.AttackSpeedBase),
                ("TALENT_BONUS", stats.AttackSpeed - formula.AttackSpeedBase)),
            ["armor"] = Breakdown(stats.Armor,
                ("STAMINA", primary.Stamina * formula.ArmorPerStamina),
                ("STRENGTH", primary.Strength * formula.ArmorPerStrength),
                ("TALENT_BONUS", stats.Armor - armorBeforeTalent)),
            ["magicResistance"] = Breakdown(stats.MagicResistance,
                ("STAMINA", primary.Stamina * formula.MagicResistancePerStamina),
                ("INTELLECT", primary.Intellect * formula.MagicResistancePerIntellect),
                ("TALENT_BONUS", stats.MagicResistance - magicResistanceBeforeTalent)),
            ["dodge"] = Breakdown(stats.Dodge,
                ("AGILITY", primary.Agility * formula.DodgePerAgility),
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
}
