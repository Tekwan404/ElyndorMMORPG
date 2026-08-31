namespace Elyndor.Core.Combat.Damage;

public sealed record HealingRequest(
    CombatActorState Target,
    decimal BaseAmount,
    decimal HealingMultiplier = 1,
    bool CanHealDead = false);

public sealed record HealingResult(
    decimal AttemptedAmount,
    decimal ModifiedAmount,
    decimal EffectiveHealing,
    decimal Overheal,
    decimal ResultingHp,
    IReadOnlyList<CombatEvent> Events);

public static class HealingPipeline
{
    public static HealingResult Resolve(HealingRequest request)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(request.BaseAmount);
        if (request.Target.IsDead && !request.CanHealDead)
        {
            return new(request.BaseAmount, 0, 0, 0, request.Target.CurrentHp, []);
        }

        decimal modified = decimal.Round(
            request.BaseAmount * Math.Max(0, request.HealingMultiplier),
            0,
            MidpointRounding.AwayFromZero);
        decimal effective = Math.Min(modified, request.Target.MaxHp - request.Target.CurrentHp);
        request.Target.ApplyHealing(effective);
        return new HealingResult(
            request.BaseAmount,
            modified,
            effective,
            modified - effective,
            request.Target.CurrentHp,
            [new CombatEvent(CombatEventType.HealingApplied, DateTimeOffset.UnixEpoch, request.Target.ActorId, Amount: effective)]);
    }
}
