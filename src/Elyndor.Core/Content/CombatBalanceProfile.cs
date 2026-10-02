using Elyndor.Core.Monsters;

namespace Elyndor.Core.Content;

public sealed record CombatGearBenchmarkDefinition(
    string Id,
    int ItemLevelLag,
    string QualityProfileId);

public sealed record CombatSecondsRange(
    decimal Minimum,
    decimal Maximum)
{
    public bool Contains(decimal seconds) =>
        seconds >= Minimum && seconds <= Maximum;
}

public sealed record CombatCurveCoefficients(
    decimal BaseValue,
    decimal LinearPerLevel,
    decimal QuadraticPerLevel)
{
    public decimal Evaluate(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        decimal x = level - 1;
        return decimal.Max(0, BaseValue + LinearPerLevel * x + QuadraticPerLevel * x * x);
    }
}

public sealed record MonsterStatCurveDefinition(
    CombatCurveCoefficients MaxHp,
    CombatCurveCoefficients AttackPower,
    CombatCurveCoefficients Armor,
    CombatCurveCoefficients MagicResistance,
    CombatCurveCoefficients AutoAttackBaseDamage);

public sealed record MonsterRankBalanceMultiplier(
    MonsterRank Rank,
    decimal HpMultiplier,
    decimal AttackPowerMultiplier,
    decimal ArmorMultiplier,
    decimal MagicResistanceMultiplier,
    decimal AutoAttackDamageMultiplier);

public sealed record MonsterArchetypeBalanceMultiplier(
    string Id,
    decimal HpMultiplier,
    decimal AttackPowerMultiplier,
    decimal ArmorMultiplier,
    decimal MagicResistanceMultiplier,
    decimal AutoAttackDamageMultiplier);

public sealed record CombatBalanceProfile(
    string Id,
    int MaxBenchmarkLevel,
    IReadOnlyList<CombatGearBenchmarkDefinition> GearStates,
    MonsterStatCurveDefinition NormalMonsterCurve,
    IReadOnlyList<MonsterRankBalanceMultiplier> RankMultipliers,
    IReadOnlyList<MonsterArchetypeBalanceMultiplier> MonsterArchetypes,
    CombatSecondsRange NormalTtkSeconds,
    CombatSecondsRange NormalTtdSeconds,
    CombatSecondsRange EliteTtkSeconds,
    CombatSecondsRange EliteTtdSeconds,
    decimal AuditTolerancePercent = 25m);

public sealed record MonsterStatTarget(
    int Level,
    MonsterRank Rank,
    string ArchetypeId,
    decimal MaxHp,
    decimal AttackPower,
    decimal Armor,
    decimal MagicResistance,
    decimal AutoAttackBaseDamage);

public static class MonsterStatCurve
{
    public static MonsterStatTarget Resolve(
        CombatBalanceProfile profile,
        int level,
        MonsterRank rank,
        string archetypeId)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(archetypeId);

        MonsterRankBalanceMultiplier rankMultiplier = profile.RankMultipliers
            .SingleOrDefault(item => item.Rank == rank)
            ?? throw new InvalidOperationException(
                $"Combat balance profile '{profile.Id}' has no rank multiplier for '{rank}'.");
        MonsterArchetypeBalanceMultiplier archetype = profile.MonsterArchetypes
            .SingleOrDefault(item => string.Equals(item.Id, archetypeId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Combat balance profile '{profile.Id}' has no archetype '{archetypeId}'.");

        MonsterStatCurveDefinition curve = profile.NormalMonsterCurve;
        return new MonsterStatTarget(
            level,
            rank,
            archetype.Id,
            Round(curve.MaxHp.Evaluate(level)
                * rankMultiplier.HpMultiplier
                * archetype.HpMultiplier),
            Round(curve.AttackPower.Evaluate(level)
                * rankMultiplier.AttackPowerMultiplier
                * archetype.AttackPowerMultiplier),
            Round(curve.Armor.Evaluate(level)
                * rankMultiplier.ArmorMultiplier
                * archetype.ArmorMultiplier),
            Round(curve.MagicResistance.Evaluate(level)
                * rankMultiplier.MagicResistanceMultiplier
                * archetype.MagicResistanceMultiplier),
            Round(curve.AutoAttackBaseDamage.Evaluate(level)
                * rankMultiplier.AutoAttackDamageMultiplier
                * archetype.AutoAttackDamageMultiplier));
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero);
}
