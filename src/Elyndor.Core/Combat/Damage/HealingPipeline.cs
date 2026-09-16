using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Combat.Damage;

public enum HealingOrigin
{
    Direct,
    Periodic,
    Copied,
    Secondary
}

public sealed record HealingRequest(
    CombatActorState Target,
    decimal BaseAmount,
    decimal HealingMultiplier = 1,
    bool CanHealDead = false,
    DateTimeOffset OccurredAtUtc = default,
    CombatActorState? Source = null,
    bool CanCrit = false,
    bool ForceCritical = false,
    decimal CriticalChanceBonus = 0,
    decimal CriticalDamageBonus = 0,
    decimal SpellPowerCoefficient = 0,
    HealingOrigin Origin = HealingOrigin.Direct,
    string? DefinitionId = null);

public sealed record HealingResult(
    decimal AttemptedAmount,
    decimal ModifiedAmount,
    decimal EffectiveHealing,
    decimal Overheal,
    decimal ResultingHp,
    IReadOnlyList<CombatEvent> Events,
    bool IsCritical = false,
    decimal RawAmount = 0,
    Guid? SourceActorId = null,
    HealingOrigin Origin = HealingOrigin.Direct);

public static class HealingPipeline
{
    public static HealingResult Resolve(
        HealingRequest request,
        IGameRandom? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(request.BaseAmount);
        ArgumentOutOfRangeException.ThrowIfNegative(request.SpellPowerCoefficient);

        if (request.Target.IsDead && !request.CanHealDead)
        {
            return new(
                request.BaseAmount,
                0,
                0,
                0,
                request.Target.CurrentHp,
                [],
                SourceActorId: request.Source?.ActorId,
                Origin: request.Origin);
        }

        DateTimeOffset calculationTimeUtc = request.OccurredAtUtc == default
            ? DateTimeOffset.MaxValue
            : request.OccurredAtUtc;
        DateTimeOffset eventTimeUtc = request.OccurredAtUtc == default
            ? DateTimeOffset.UnixEpoch
            : request.OccurredAtUtc;

        decimal spellPower = request.Source is null
            ? 0
            : EffectEngine.CalculateStat(
                request.Source,
                EffectStat.SpellPower,
                request.Source.Stats.SpellPower,
                calculationTimeUtc);
        decimal scaledBase = request.BaseAmount
            + Math.Max(0, spellPower) * request.SpellPowerCoefficient;

        bool critical = ResolveCritical(request, random, calculationTimeUtc);
        decimal criticalBonus = request.Source is null
            ? 0
            : Math.Max(
                0,
                request.Source.Stats.CriticalDamage
                    + request.CriticalDamageBonus / 100m);
        decimal raw = scaledBase * (critical ? 1 + criticalBonus : 1);

        decimal outgoingMultiplier = request.Source is null
            ? 1
            : EffectEngine.CalculateStat(
                request.Source,
                EffectStat.OutgoingHealingMultiplier,
                1,
                calculationTimeUtc);
        decimal receivedMultiplier = EffectEngine.CalculateStat(
            request.Target,
            EffectStat.HealingReceivedMultiplier,
            1,
            calculationTimeUtc);
        decimal modified = decimal.Round(
            raw
                * Math.Max(0, request.HealingMultiplier)
                * outgoingMultiplier
                * receivedMultiplier,
            0,
            MidpointRounding.AwayFromZero);
        decimal effective = Math.Min(modified, request.Target.MaxHp - request.Target.CurrentHp);
        request.Target.ApplyHealing(effective);

        CombatEvent healingEvent = new(
            CombatEventType.HealingApplied,
            eventTimeUtc,
            request.Target.ActorId,
            request.DefinitionId,
            effective,
            SourceActorId: request.Source?.ActorId,
            TargetActorId: request.Target.ActorId,
            IsPeriodic: request.Origin == HealingOrigin.Periodic,
            IsCritical: critical,
            HealingOrigin: request.Origin);

        return new HealingResult(
            request.BaseAmount,
            modified,
            effective,
            modified - effective,
            request.Target.CurrentHp,
            [healingEvent],
            critical,
            raw,
            request.Source?.ActorId,
            request.Origin);
    }

    private static bool ResolveCritical(
        HealingRequest request,
        IGameRandom? random,
        DateTimeOffset occurredAtUtc)
    {
        if (request.ForceCritical)
        {
            if (request.Source is null)
            {
                throw new InvalidOperationException(
                    "Forced critical healing requires a source actor.");
            }

            return true;
        }

        if (!request.CanCrit)
        {
            return false;
        }

        if (request.Source is null)
        {
            throw new InvalidOperationException(
                "Critical healing requires a source actor.");
        }

        if (random is null)
        {
            throw new InvalidOperationException(
                "Critical healing requires an injected game RNG.");
        }

        decimal criticalChance = EffectEngine.CalculateStat(
            request.Source,
            EffectStat.CriticalChance,
            request.Source.Stats.CriticalChance,
            occurredAtUtc) + request.CriticalChanceBonus;
        return random.NextUnit() < Math.Clamp(criticalChance / 100m, 0, 1);
    }
}
