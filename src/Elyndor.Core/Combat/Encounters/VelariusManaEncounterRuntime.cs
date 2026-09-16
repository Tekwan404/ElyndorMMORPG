namespace Elyndor.Core.Combat.Encounters;

public sealed record VelariusManaEncounterDefinition(
    decimal FeederTriggerHpPercent,
    decimal FinalBarrierTriggerHpPercent,
    decimal ShieldMaxHpRatioPerMana,
    int FeederCount,
    decimal ManaPerFeed)
{
    public static VelariusManaEncounterDefinition Default { get; } = new(
        FeederTriggerHpPercent: 0.60m,
        FinalBarrierTriggerHpPercent: 0.10m,
        ShieldMaxHpRatioPerMana: 0.005m,
        FeederCount: 2,
        ManaPerFeed: 5m);

    public void Validate()
    {
        if (FeederTriggerHpPercent <= 0 || FeederTriggerHpPercent >= 1)
            throw new ArgumentOutOfRangeException(nameof(FeederTriggerHpPercent));
        if (FinalBarrierTriggerHpPercent <= 0
            || FinalBarrierTriggerHpPercent >= FeederTriggerHpPercent)
        {
            throw new ArgumentOutOfRangeException(nameof(FinalBarrierTriggerHpPercent));
        }
        if (ShieldMaxHpRatioPerMana <= 0)
            throw new ArgumentOutOfRangeException(nameof(ShieldMaxHpRatioPerMana));
        if (FeederCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(FeederCount));
        if (ManaPerFeed <= 0)
            throw new ArgumentOutOfRangeException(nameof(ManaPerFeed));
    }
}

public sealed class VelariusManaEncounterRuntime
{
    private readonly VelariusManaEncounterDefinition _definition;

    public VelariusManaEncounterRuntime(VelariusManaEncounterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        _definition = definition;
    }

    public bool FeedersTriggered { get; private set; }
    public bool FinalBarrierTriggered { get; private set; }
    public bool FinalPhaseActive => FinalBarrierTriggered;
    public int FeederCount => _definition.FeederCount;
    public decimal ManaPerFeed => _definition.ManaPerFeed;
    public decimal FinalBarrierTriggerHpPercent => _definition.FinalBarrierTriggerHpPercent;

    public bool TryTriggerFeeders(decimal currentHp, decimal maxHp)
    {
        ValidateHp(currentHp, maxHp);
        if (FeedersTriggered || FinalBarrierTriggered)
            return false;
        if (currentHp / maxHp > _definition.FeederTriggerHpPercent)
            return false;
        if (currentHp / maxHp <= _definition.FinalBarrierTriggerHpPercent)
            return false;

        FeedersTriggered = true;
        return true;
    }

    public bool TryTriggerFinalBarrier(
        decimal currentHp,
        decimal maxHp,
        decimal remainingMana,
        out decimal shieldMagnitude)
    {
        ValidateHp(currentHp, maxHp);
        ArgumentOutOfRangeException.ThrowIfNegative(remainingMana);
        shieldMagnitude = 0;
        if (FinalBarrierTriggered
            || currentHp / maxHp > _definition.FinalBarrierTriggerHpPercent)
        {
            return false;
        }

        FinalBarrierTriggered = true;
        shieldMagnitude = maxHp * remainingMana * _definition.ShieldMaxHpRatioPerMana;
        return true;
    }

    private static void ValidateHp(decimal currentHp, decimal maxHp)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHp);
        ArgumentOutOfRangeException.ThrowIfNegative(currentHp);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(currentHp, maxHp);
    }
}
