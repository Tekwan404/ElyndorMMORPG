using Elyndor.Core.Content;

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
        PrimaryStats primary = Add(
            classStats,
            sources.Equipment,
            sources.Talents,
            sources.Effects);

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
    PrimaryStats Effects)
{
    private static readonly PrimaryStats EmptyStats = new(0, 0, 0, 0);

    public static CharacterStatInputs Empty { get; } =
        new(EmptyStats, EmptyStats, EmptyStats);
}
