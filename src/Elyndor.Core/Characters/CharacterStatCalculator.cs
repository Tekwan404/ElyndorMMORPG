using Elyndor.Core.Content;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Characters;

public sealed class CharacterStatCalculator(
    StatFormulaProfile formula,
    IReadOnlyList<ClassProfile> profiles)
{
    public CharacterStats Calculate(
        string classId,
        int level,
        CharacterStatInputs? inputs = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        ClassProfile profile = profiles.Single(candidate =>
            string.Equals(candidate.Id, classId, StringComparison.Ordinal));
        decimal completedLevels = level - 1;
        PrimaryStats classStats = new(
            profile.BaseStats.Strength + (profile.LevelGrowth.Strength * completedLevels),
            profile.BaseStats.Agility + (profile.LevelGrowth.Agility * completedLevels),
            profile.BaseStats.Intellect + (profile.LevelGrowth.Intellect * completedLevels),
            profile.BaseStats.Stamina + (profile.LevelGrowth.Stamina * completedLevels));
        CharacterStatInputs sources = inputs ?? CharacterStatInputs.Empty;
        PrimaryStats beforeTalents = Add(classStats, sources.Equipment);
        PrimaryStats talentPercentages = new(
            beforeTalents.Strength * sources.TalentPercentages.Strength / 100,
            beforeTalents.Agility * sources.TalentPercentages.Agility / 100,
            beforeTalents.Intellect * sources.TalentPercentages.Intellect / 100,
            beforeTalents.Stamina * sources.TalentPercentages.Stamina / 100);
        PrimaryStats primary = Add(beforeTalents, sources.Talents, talentPercentages, sources.Effects);

        return new CharacterStats(
            primary.Strength,
            primary.Agility,
            primary.Intellect,
            primary.Stamina,
            formula.MaxHpBase + (primary.Stamina * formula.MaxHpPerStamina),
            (primary.Strength * formula.AttackPowerPerStrength)
                + (primary.Agility * formula.AttackPowerPerAgility),
            primary.Intellect * formula.SpellPowerPerIntellect,
            decimal.Clamp(
                formula.CriticalChanceBase
                    + (primary.Agility * formula.CriticalChancePerAgility),
                0,
                100),
            formula.CriticalDamageBase,
            formula.AccuracyBase,
            0,
            0,
            formula.AttackSpeedBase,
            (primary.Stamina * formula.ArmorPerStamina)
                + (primary.Strength * formula.ArmorPerStrength),
            (primary.Stamina * formula.MagicResistancePerStamina)
                + (primary.Intellect * formula.MagicResistancePerIntellect),
            decimal.Clamp(primary.Agility * formula.DodgePerAgility, 0, 100));
    }

    private static PrimaryStats Add(PrimaryStats first, params PrimaryStats[] sources) =>
        sources.Aggregate(
            first,
            (total, source) => new PrimaryStats(
                total.Strength + source.Strength,
                total.Agility + source.Agility,
                total.Intellect + source.Intellect,
                total.Stamina + source.Stamina));
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
}
