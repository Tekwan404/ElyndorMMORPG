using Elyndor.Core.Content;
using Elyndor.Core.Monsters;

namespace Elyndor.Core.Balance;

public sealed record MonsterBalanceAuditEntry(
    string MonsterId,
    int Level,
    MonsterRank Rank,
    string ArchetypeId,
    MonsterStatTarget Target,
    decimal HpDeltaPercent,
    decimal AttackPowerDeltaPercent,
    decimal ArmorDeltaPercent,
    decimal MagicResistanceDeltaPercent,
    decimal AutoAttackBaseDamageDeltaPercent,
    bool WithinTolerance);

public static class MonsterBalanceAudit
{
    public static IReadOnlyList<MonsterBalanceAuditEntry> Run(GameContentPackage content)
    {
        ArgumentNullException.ThrowIfNull(content);
        CombatBalanceProfile profile = content.CombatBalance
            ?? throw new InvalidOperationException("Combat balance profile is required.");

        return (content.Monsters ?? [])
            .Where(monster =>
                monster.Level <= profile.MaxBenchmarkLevel
                && monster.Rank is MonsterRank.Normal or MonsterRank.Elite)
            .Select(monster => Audit(profile, monster))
            .OrderBy(item => item.Level)
            .ThenBy(item => item.Rank)
            .ThenBy(item => item.MonsterId, StringComparer.Ordinal)
            .ToArray();
    }

    public static MonsterBalanceAuditEntry Audit(
        CombatBalanceProfile profile,
        MonsterDefinition monster)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(monster);

        MonsterStatTarget target = MonsterStatCurve.Resolve(
            profile,
            monster.Level,
            monster.Rank,
            monster.BalanceArchetypeId);
        decimal autoAttackBaseDamage = ResolveAutoAttackBaseDamage(monster);
        decimal hpDelta = DeltaPercent(monster.MaxHp, target.MaxHp);
        decimal attackPowerDelta = DeltaPercent(monster.Stats.AttackPower, target.AttackPower);
        decimal armorDelta = DeltaPercent(monster.Stats.Armor, target.Armor);
        decimal magicResistanceDelta = DeltaPercent(
            monster.Stats.MagicResistance,
            target.MagicResistance);
        decimal autoAttackDelta = DeltaPercent(
            autoAttackBaseDamage,
            target.AutoAttackBaseDamage);
        decimal tolerance = profile.AuditTolerancePercent;

        return new MonsterBalanceAuditEntry(
            monster.Id,
            monster.Level,
            monster.Rank,
            monster.BalanceArchetypeId,
            target,
            hpDelta,
            attackPowerDelta,
            armorDelta,
            magicResistanceDelta,
            autoAttackDelta,
            Math.Abs(hpDelta) <= tolerance
            && Math.Abs(attackPowerDelta) <= tolerance
            && Math.Abs(armorDelta) <= tolerance
            && Math.Abs(magicResistanceDelta) <= tolerance
            && Math.Abs(autoAttackDelta) <= tolerance);
    }

    private static decimal ResolveAutoAttackBaseDamage(MonsterDefinition monster)
    {
        decimal minimum = monster.AutoAttackBaseDamageMin ?? monster.AutoAttackBaseDamage;
        decimal maximum = monster.AutoAttackBaseDamageMax ?? minimum;
        return (minimum + maximum) / 2m;
    }

    private static decimal DeltaPercent(decimal actual, decimal expected) =>
        expected <= 0
            ? actual == 0 ? 0 : 100
            : decimal.Round(
                (actual - expected) / expected * 100m,
                2,
                MidpointRounding.AwayFromZero);
}
