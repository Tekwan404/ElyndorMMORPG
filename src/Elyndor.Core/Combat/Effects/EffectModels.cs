namespace Elyndor.Core.Combat.Effects;

public enum EffectKind
{
    Buff,
    Debuff,
    DamageOverTime,
    HealingOverTime,
    Shield,
    StatModifier,
    ConditionalModifier,
    Stun,
    Silence,
    LethalDamagePrevention
}

public enum EffectStackPolicy
{
    Stack,
    Replace,
    Refresh,
    Independent,
    StrongestWins
}

public enum EffectStat { AttackPower }
public enum EffectModifierMode { Flat, Percent, Multiplicative }

public sealed record EffectDefinition(
    string Id,
    EffectKind Kind,
    TimeSpan Duration,
    int MaxStacks,
    EffectStackPolicy StackPolicy,
    decimal Magnitude,
    TimeSpan? TickInterval = null,
    int ApplicationPriority = 0,
    bool IsDynamic = false,
    string? DispelCategory = null,
    int Version = 1,
    EffectStat? ModifiedStat = null,
    EffectModifierMode ModifierMode = EffectModifierMode.Flat);

public sealed class ActiveEffect
{
    internal ActiveEffect(
        long sequence,
        Guid sourceId,
        Guid targetId,
        EffectDefinition definition,
        DateTimeOffset appliedAtUtc)
    {
        InstanceId = Guid.NewGuid();
        Sequence = sequence;
        SourceId = sourceId;
        TargetId = targetId;
        Definition = definition;
        AppliedAtUtc = appliedAtUtc;
        ExpiresAtUtc = appliedAtUtc + definition.Duration;
        NextTickAtUtc = definition.TickInterval is { } interval ? appliedAtUtc + interval : null;
        Stacks = 1;
        RemainingMagnitude = definition.Magnitude;
    }

    public Guid InstanceId { get; }
    public long Sequence { get; }
    public Guid SourceId { get; }
    public Guid TargetId { get; }
    public EffectDefinition Definition { get; }
    public DateTimeOffset AppliedAtUtc { get; internal set; }
    public DateTimeOffset ExpiresAtUtc { get; internal set; }
    public DateTimeOffset? NextTickAtUtc { get; internal set; }
    public int Stacks { get; internal set; }
    public decimal RemainingMagnitude { get; internal set; }
}
