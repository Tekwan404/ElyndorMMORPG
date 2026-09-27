namespace Elyndor.Core.Combat.Effects;

public sealed record EffectExecutionContext(
    int ProcDepth = 0,
    bool IsPeriodicDamage = false,
    bool IsCriticalHit = false)
{
    public EffectExecutionContext NextProc() => this with
    {
        ProcDepth = checked(ProcDepth + 1),
        IsPeriodicDamage = false,
        IsCriticalHit = false
    };
}

public static class EffectProcPolicy
{
    // Direct player action is depth 0. It may proc depth 1; that proc may proc
    // depth 2. Depth 2 is terminal and cannot start another proc chain.
    public const int MaximumTriggeredProcDepth = 2;
    private static readonly TimeSpan MinimumInternalCooldown = TimeSpan.FromSeconds(1);

    public static bool CanTriggerProc(EffectExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return !context.IsPeriodicDamage
            && context.ProcDepth >= 0
            && context.ProcDepth < MaximumTriggeredProcDepth;
    }

    public static TimeSpan ResolveDefaultInternalCooldown(TimeSpan baseCooldown)
    {
        if (baseCooldown < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(baseCooldown));

        TimeSpan halfCooldown = TimeSpan.FromTicks(baseCooldown.Ticks / 2);
        return halfCooldown < MinimumInternalCooldown
            ? MinimumInternalCooldown
            : halfCooldown;
    }

    public static void Validate(EffectDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.ProcChance is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(definition), "Effect proc chance must be between 0 and 1.");
        if (definition.InternalCooldown < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(definition), "Effect internal cooldown cannot be negative.");
    }
}