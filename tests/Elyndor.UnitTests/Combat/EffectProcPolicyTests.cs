using Elyndor.Core.Combat.Effects;

namespace Elyndor.UnitTests.Combat;

public sealed class EffectProcPolicyTests
{
    [Fact]
    public void DirectActionAndFirstProcCanTriggerButSecondProcIsTerminal()
    {
        EffectExecutionContext directAction = new();
        EffectExecutionContext firstProc = directAction.NextProc();
        EffectExecutionContext secondProc = firstProc.NextProc();

        Assert.True(EffectProcPolicy.CanTriggerProc(directAction));
        Assert.True(EffectProcPolicy.CanTriggerProc(firstProc));
        Assert.False(EffectProcPolicy.CanTriggerProc(secondProc));
        Assert.Equal(0, directAction.ProcDepth);
        Assert.Equal(1, firstProc.ProcDepth);
        Assert.Equal(2, secondProc.ProcDepth);
    }

    [Fact]
    public void PeriodicDamageCannotTriggerProcEvenWhenMarkedCritical()
    {
        EffectExecutionContext periodicCritical = new(
            ProcDepth: 0,
            IsPeriodicDamage: true,
            IsCriticalHit: true);

        Assert.False(EffectProcPolicy.CanTriggerProc(periodicCritical));
    }

    [Fact]
    public void DefaultInternalCooldownUsesHalfBaseCooldownWithOneSecondFloor()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(5),
            EffectProcPolicy.ResolveDefaultInternalCooldown(TimeSpan.FromSeconds(10)));
        Assert.Equal(
            TimeSpan.FromSeconds(1),
            EffectProcPolicy.ResolveDefaultInternalCooldown(TimeSpan.FromSeconds(1)));
        Assert.Equal(
            TimeSpan.FromSeconds(1),
            EffectProcPolicy.ResolveDefaultInternalCooldown(TimeSpan.Zero));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void InvalidProcChanceIsRejected(double value)
    {
        EffectDefinition definition = new(
            "INVALID_PROC_CHANCE",
            EffectKind.Buff,
            TimeSpan.FromSeconds(1),
            1,
            EffectStackPolicy.Refresh,
            0,
            ProcChance: (decimal)value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EffectProcPolicy.Validate(definition));
    }
}