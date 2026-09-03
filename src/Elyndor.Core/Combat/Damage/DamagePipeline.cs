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
    decimal MinimumDamage = 1,
    decimal ArmorPenetrationBonus = 0,
    bool ForceCritical = false,
    bool SkipDefenseMitigation = false,
    decimal AccuracyBonus = 0,
    decimal CriticalChanceBonus = 0,
    decimal CriticalDamageBonus = 0,
    decimal MagicPenetrationBonus = 0);

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

        if (request.CanMiss || request.CanDodge)
        {
            decimal avoidanceRoll = random.NextUnit();
            decimal levelPenalty = Math.Min(
                Math.Max(0, request.Target.Stats.Level - request.Source.Stats.Level) * LevelPenaltyPerLevel,
                MaxLevelPenalty);
            decimal effectiveAccuracy = request.Source.Stats.Accuracy + request.AccuracyBonus;
            decimal missChance = request.CanMiss
                ? Math.Clamp(
                    BaseMissChance + levelPenalty - effectiveAccuracy / 100m,
                    0,
                    MaxMissChance)
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
        }

        decimal criticalChance = EffectEngine.CalculateStat(
            request.Source,
            EffectStat.CriticalChance,
            request.Source.Stats.CriticalChance,
            occurredAtUtc) + request.CriticalChanceBonus;
        bool critical = request.ForceCritical
                        || request.CanCrit
                        && random.NextUnit() < Math.Clamp(criticalChance / 100m, 0, 1);
        decimal criticalDamage = request.Source.Stats.CriticalDamage
            + request.CriticalDamageBonus / 100m;
        decimal raw = request.BaseAmount
            * (critical ? 1 + Math.Max(0, criticalDamage) : 1);
        decimal afterMitigation = request.SkipDefenseMitigation ? raw : Mitigate(request, raw);
        decimal incomingMultiplier = EffectEngine.CalculateStat(
            request.Target,
            EffectStat.IncomingDamageMultiplier,
            1,
            occurredAtUtc,
            request.Source.ActorId);
        decimal outgoingPhysicalMultiplier = request.Type == DamageType.Physical
            ? EffectEngine.CalculateStat(
                request.Source,
                EffectStat.OutgoingPhysicalDamageMultiplier,
                1,
                occurredAtUtc,
                request.Source.ActorId)
            : 1;
        decimal incomingPhysicalMultiplier = request.Type == DamageType.Physical
            ? EffectEngine.CalculateStat(
                request.Target,
                EffectStat.IncomingPhysicalDamageMultiplier,
                1,
                occurredAtUtc,
                request.Source.ActorId)
            : 1;
        decimal talentDamageMultiplier =
            1 + request.Source.TalentModifiers.DamageDealtPercent / 100m;
        decimal talentReduction = request.Type switch
        {
            DamageType.Physical =>
                request.Target.TalentModifiers.IncomingPhysicalDamageReductionPercent,
            DamageType.Magical =>
                request.Target.TalentModifiers.IncomingMagicalDamageReductionPercent,
            _ => 0
        };
        decimal talentIncomingMultiplier = Math.Max(0, 1 - talentReduction / 100m);
        decimal modified = afterMitigation
            * Math.Max(0, request.DamageMultiplier)
            * incomingMultiplier
            * outgoingPhysicalMultiplier
            * incomingPhysicalMultiplier
            * Math.Max(0, talentDamageMultiplier)
            * talentIncomingMultiplier;
        decimal minimumApplied = modified > 0
            ? Math.Max(modified, Math.Max(0, request.MinimumDamage))
            : 0;
        decimal rounded = decimal.Round(minimumApplied, 0, MidpointRounding.AwayFromZero);
        decimal absorbed = request.IgnoreShields ? 0 : AbsorbShields(request.Target, rounded);
        decimal hpDamage = Math.Min(
            request.Target.CurrentHp,
            Math.Max(0, rounded - absorbed));
        bool lethal = request.Target.CanDie
                      && hpDamage >= request.Target.CurrentHp
                      && hpDamage > 0;
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

        decimal vampirismHealing = request.Type == DamageType.Physical
            ? hpDamage
                * Math.Max(0, request.Source.TalentModifiers.VampirismPercent)
                / 100m
            : 0;
        if (vampirismHealing > 0 && request.Source.ActorId != request.Target.ActorId)
        {
            request.Source.ApplyHealing(vampirismHealing);
        }

        List<CombatEvent> events = [];
        if (absorbed > 0)
        {
            events.Add(new CombatEvent(
                CombatEventType.ShieldAbsorbed,
                occurredAtUtc,
                request.Target.ActorId,
                Amount: absorbed,
                SourceActorId: request.Source.ActorId,
                TargetActorId: request.Target.ActorId,
                DamageType: request.Type));
        }

        if (critical && rounded > 0)
        {
            events.Add(new CombatEvent(
                CombatEventType.CriticalHit,
                occurredAtUtc,
                request.Source.ActorId,
                Amount: hpDamage,
                SourceActorId: request.Source.ActorId,
                TargetActorId: request.Target.ActorId,
                AmountBeforeShields: rounded,
                DamageType: request.Type));
        }

        events.Add(new CombatEvent(
            CombatEventType.DamageDealt,
            occurredAtUtc,
            request.Target.ActorId,
            Amount: hpDamage,
            SourceActorId: request.Source.ActorId,
            TargetActorId: request.Target.ActorId,
            AmountBeforeShields: rounded,
            DamageType: request.Type));
        if (vampirismHealing > 0)
        {
            events.Add(new CombatEvent(
                CombatEventType.HealingApplied,
                occurredAtUtc,
                request.Source.ActorId,
                Amount: vampirismHealing,
                SourceActorId: request.Source.ActorId,
                TargetActorId: request.Source.ActorId));
        }

        if (request.Target.IsDead)
        {
            events.Add(new CombatEvent(
                CombatEventType.ActorDied,
                occurredAtUtc,
                request.Target.ActorId,
                SourceActorId: request.Source.ActorId,
                TargetActorId: request.Target.ActorId,
                DamageType: request.Type));
        }

        return new DamageResult(
            request.BaseAmount,
            DamageAvoidance.None,
            critical,
            raw,
            raw - afterMitigation,
            decimal.Round(afterMitigation, 0, MidpointRounding.AwayFromZero),
            rounded,
            absorbed,
            hpDamage,
            lethal,
            preventionTriggered,
            request.Target.CurrentHp,
            events);
    }

    private static decimal Mitigate(DamageRequest request, decimal damage)
    {
        if (request.Type == DamageType.True)
        {
            return damage;
        }

        decimal defense = request.Type == DamageType.Physical
            ? request.Target.Stats.Armor
            : request.Target.Stats.MagicResistance;
        decimal penetration = request.Type == DamageType.Physical
            ? request.Source.Stats.ArmorPenetration + request.ArmorPenetrationBonus
            : request.Source.Stats.MagicPenetration + request.MagicPenetrationBonus;
        decimal effectiveDefense =
            Math.Max(0, defense * (1 - Math.Clamp(penetration, 0, 1)));
        return damage * MitigationConstant / (MitigationConstant + effectiveDefense);
    }

    private static decimal AbsorbShields(CombatActorState target, decimal incoming)
    {
        decimal remaining = incoming;
        foreach (ActiveEffect shield in target.ActiveEffects
                     .Where(effect =>
                         effect.Definition.Kind == EffectKind.Shield
                         && effect.RemainingMagnitude > 0)
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
        new(
            request.BaseAmount,
            avoidance,
            false,
            0,
            0,
            0,
            0,
            0,
            0,
            false,
            false,
            request.Target.CurrentHp,
            []);
}
