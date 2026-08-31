using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Combat.Damage;

public enum DamageType { Physical, Magical, True }
public enum DamageAvoidance { None, Miss, Dodge, Immune }

public sealed record DamageRequest(
    CombatActorState Source,
    CombatActorState Target,
    decimal BaseAmount,
    DamageType Type,
    bool CanMiss = true,
    bool CanDodge = true,
    bool CanCrit = true,
    bool IgnoreShields = false,
    decimal DamageMultiplier = 1,
    decimal MinimumDamage = 1);

public sealed record DamageResult(
    decimal AttemptedAmount,
    DamageAvoidance Avoidance,
    bool IsCritical,
    decimal RawAmount,
    decimal MitigatedAmount,
    decimal AfterMitigation,
    decimal ModifiedAmount,
    decimal AbsorbedByShields,
    decimal HpDamage,
    bool IsLethal,
    bool LethalPreventionTriggered,
    decimal ResultingHp,
    IReadOnlyList<CombatEvent> Events);

public static class DamagePipeline
{
    private const decimal BaseMissChance = 0.05m;
    private const decimal LevelPenaltyPerLevel = 0.01m;
    private const decimal MaxLevelPenalty = 0.10m;
    private const decimal MaxMissChance = 0.30m;
    private const decimal MitigationConstant = 100m;

    public static DamageResult Resolve(
        DamageRequest request,
        IGameRandom random,
        DateTimeOffset occurredAtUtc = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(request.BaseAmount);
        if (request.Source.IsDead || request.Target.IsDead)
        {
            return Empty(request, DamageAvoidance.Immune);
        }

        decimal avoidanceRoll = random.NextUnit();
        decimal levelPenalty = Math.Min(
            Math.Max(0, request.Target.Stats.Level - request.Source.Stats.Level) * LevelPenaltyPerLevel,
            MaxLevelPenalty);
        decimal missChance = request.CanMiss
            ? Math.Clamp(BaseMissChance + levelPenalty - request.Source.Stats.Accuracy / 100m, 0, MaxMissChance)
            : 0;
        decimal dodgeChance = request.CanDodge
            ? Math.Clamp(request.Target.Stats.Dodge / 100m, 0, 1)
            : 0;

        if (avoidanceRoll < missChance)
        {
            return Empty(request, DamageAvoidance.Miss);
        }

        if (avoidanceRoll < missChance + dodgeChance)
        {
            return Empty(request, DamageAvoidance.Dodge);
        }

        bool critical = request.CanCrit
                        && random.NextUnit() < Math.Clamp(request.Source.Stats.CriticalChance / 100m, 0, 1);
        decimal raw = request.BaseAmount * (critical ? 1 + request.Source.Stats.CriticalDamage : 1);
        decimal afterMitigation = Mitigate(raw, request.Type, request.Source.Stats, request.Target.Stats);
        decimal modified = afterMitigation * Math.Max(0, request.DamageMultiplier);
        decimal minimumApplied = modified > 0 ? Math.Max(modified, Math.Max(0, request.MinimumDamage)) : 0;
        decimal rounded = decimal.Round(minimumApplied, 0, MidpointRounding.AwayFromZero);
        decimal absorbed = request.IgnoreShields ? 0 : AbsorbShields(request.Target, rounded);
        decimal hpDamage = Math.Min(request.Target.CurrentHp, Math.Max(0, rounded - absorbed));
        bool lethal = hpDamage >= request.Target.CurrentHp && hpDamage > 0;
        bool preventionTriggered = false;
        if (lethal)
        {
            ActiveEffect? prevention = request.Target.ActiveEffects
                .Where(effect => effect.Definition.Kind == EffectKind.LethalDamagePrevention)
                .OrderByDescending(effect => effect.Definition.ApplicationPriority)
                .ThenBy(effect => effect.Sequence)
                .FirstOrDefault();
            if (prevention is not null)
            {
                hpDamage = Math.Max(0, request.Target.CurrentHp - 1);
                lethal = false;
                preventionTriggered = true;
                request.Target.ActiveEffects.Remove(prevention);
            }
        }
        request.Target.ApplyDamage(hpDamage);

        List<CombatEvent> events = [];
        if (absorbed > 0)
        {
            events.Add(new CombatEvent(CombatEventType.ShieldAbsorbed, occurredAtUtc,
                request.Target.ActorId, Amount: absorbed));
        }

        events.Add(new CombatEvent(CombatEventType.DamageApplied, occurredAtUtc,
            request.Target.ActorId, Amount: hpDamage));
        if (request.Target.IsDead)
        {
            events.Add(new CombatEvent(CombatEventType.ActorDied, occurredAtUtc, request.Target.ActorId));
        }

        return new DamageResult(
            request.BaseAmount, DamageAvoidance.None, critical, raw, raw - afterMitigation,
            decimal.Round(afterMitigation, 0, MidpointRounding.AwayFromZero), rounded,
            absorbed, hpDamage, lethal, preventionTriggered, request.Target.CurrentHp, events);
    }

    private static decimal Mitigate(
        decimal damage,
        DamageType type,
        CombatStats source,
        CombatStats target)
    {
        if (type == DamageType.True)
        {
            return damage;
        }

        decimal defense = type == DamageType.Physical ? target.Armor : target.MagicResistance;
        decimal penetration = type == DamageType.Physical ? source.ArmorPenetration : source.MagicPenetration;
        decimal effectiveDefense = Math.Max(0, defense * (1 - Math.Clamp(penetration, 0, 1)));
        return damage * MitigationConstant / (MitigationConstant + effectiveDefense);
    }

    private static decimal AbsorbShields(CombatActorState target, decimal incoming)
    {
        decimal remaining = incoming;
        foreach (ActiveEffect shield in target.ActiveEffects
                     .Where(effect => effect.Definition.Kind == EffectKind.Shield && effect.RemainingMagnitude > 0)
                     .OrderByDescending(effect => effect.AppliedAtUtc)
                     .ThenByDescending(effect => effect.Sequence)
                     .ToArray())
        {
            decimal absorbed = Math.Min(shield.RemainingMagnitude, remaining);
            shield.RemainingMagnitude -= absorbed;
            remaining -= absorbed;
            if (shield.RemainingMagnitude <= 0)
            {
                target.ActiveEffects.Remove(shield);
            }

            if (remaining <= 0)
            {
                break;
            }
        }

        return incoming - remaining;
    }

    private static DamageResult Empty(DamageRequest request, DamageAvoidance avoidance) =>
        new(request.BaseAmount, avoidance, false, 0, 0, 0, 0, 0, 0, false, false,
            request.Target.CurrentHp, []);
}
