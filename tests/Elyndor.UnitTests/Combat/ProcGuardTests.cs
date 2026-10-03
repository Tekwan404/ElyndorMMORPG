using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class ProcGuardTests
{
    [Fact]
    public void ReflectedDamageCannotStartAnotherProcChain()
    {
        var input = new CombatEvent(CombatEventType.DamageDealt,
            DateTimeOffset.UnixEpoch, Guid.NewGuid(), IsReflected: true);
        Assert.False(ProcGuard.IsEligible(input));
    }

    [Theory]
    [InlineData(false, false, 0, true)]
    [InlineData(true, false, 0, false)]
    [InlineData(false, true, 0, false)]
    [InlineData(false, true, 1, false)]
    [InlineData(false, false, 1, false)]
    [InlineData(false, false, 2, false)]
    [InlineData(false, false, -1, false)]
    public void OnlyDirectRootEventsCanCreateDepthOneProc(bool periodic, bool proc, int depth, bool expected)
        => Assert.Equal(expected, ProcGuard.IsEligible(periodic, proc, depth));

    [Fact]
    public void CooldownIsActorScopedAndReadyAtExactBoundary()
    {
        var guard = new ProcGuard();
        Guid owner = Guid.NewGuid();
        guard.StartCooldown(owner, "PROC", DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(5));
        Assert.False(guard.IsReady(owner, "PROC", DateTimeOffset.UnixEpoch.AddSeconds(4.999)));
        Assert.True(guard.IsReady(owner, "PROC", DateTimeOffset.UnixEpoch.AddSeconds(5)));
        Assert.True(guard.IsReady(Guid.NewGuid(), "PROC", DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void SharedGuardDeduplicatesRecordCopiesAcrossRuntimeAdapters()
    {
        Guid owner = Guid.NewGuid();
        var guard = new ProcGuard();
        var first = new TalentRuntimeState(owner, new SequenceGameRandom(0), guard);
        var second = new TalentRuntimeState(owner, new SequenceGameRandom(0), guard);
        var input = new CombatRuntimeEvent(CombatRuntimeEventKind.CriticalHit,
            DateTimeOffset.UnixEpoch, owner);
        var hook = new ResolvedTalentEventHook("PROC", TalentModifierKeys.OnCriticalHit,
            1, 5, null, TimeSpan.Zero, false);
        var modifiers = ResolvedTalentModifiers.Empty with { EventHooks = [hook] };
        Assert.Single(first.Publish(input, modifiers));
        Assert.Empty(second.Publish(input with { Sequence = 10 }, modifiers));
    }
}
