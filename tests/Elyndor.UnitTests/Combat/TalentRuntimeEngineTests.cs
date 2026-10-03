using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class TalentRuntimeEngineTests
{
    [Fact]
    public void OlderReplayAfterANewerEventIsIgnored()
    {
        Guid owner = Guid.NewGuid();
        var runtime = new TalentRuntimeState(owner, new SequenceGameRandom(0));
        var hook = new ResolvedTalentEventHook("SAFE", TalentModifierKeys.OnCriticalHit,
            1, 5, null, TimeSpan.Zero, false);
        var input = Event(DateTimeOffset.UnixEpoch, owner, sequence: 1);
        Assert.Single(runtime.Publish(input, Modifiers(hook)));
        Assert.Single(runtime.Publish(Event(DateTimeOffset.UnixEpoch.AddSeconds(1), owner, sequence: 2), Modifiers(hook)));
        Assert.Empty(runtime.Publish(input with { }, Modifiers(hook)));
    }

    [Fact]
    public void RepeatedSequencedEventIsIgnoredWithoutThrowing()
    {
        Guid owner = Guid.NewGuid();
        var runtime = new TalentRuntimeState(owner, new SequenceGameRandom(0));
        var hook = new ResolvedTalentEventHook("SAFE", TalentModifierKeys.OnCriticalHit,
            1, 5, null, TimeSpan.Zero, false);
        var input = Event(DateTimeOffset.UnixEpoch, owner, sequence: 1);
        Assert.Single(runtime.Publish(input, Modifiers(hook)));
        Assert.Empty(runtime.Publish(input with { }, Modifiers(hook)));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    public void UnsafeSourcesCannotProduceAnotherProc(bool periodic, int depth)
    {
        Guid owner = Guid.NewGuid();
        TalentRuntimeState state = new(owner, new SequenceGameRandom(0));
        var hook = new ResolvedTalentEventHook("SAFE", TalentModifierKeys.OnCriticalHit,
            1, 5, null, TimeSpan.Zero, true);
        var input = new CombatRuntimeEvent(CombatRuntimeEventKind.CriticalHit,
            DateTimeOffset.UtcNow, owner, IsPeriodic: periodic, ProcDepth: depth);
        Assert.Empty(state.Publish(input, Modifiers(hook)));
        Assert.Empty(state.Snapshot.Stacks);
    }

    [Fact]
    public void PublishUsesResolvedRankValueForMatchingOwnerEvent()
    {
        Guid ownerId = Guid.NewGuid();
        TalentRuntimeState state = new(ownerId, new SequenceGameRandom(0));
        ResolvedTalentModifiers modifiers = Modifiers(
            new ResolvedTalentEventHook(
                "TALENT_A",
                TalentModifierKeys.OnCriticalHit,
                2,
                12,
                null,
                TimeSpan.Zero,
                false));

        IReadOnlyList<TalentRuntimeAction> actions = state.Publish(
            new CombatRuntimeEvent(
                CombatRuntimeEventKind.CriticalHit,
                new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero),
                ownerId,
                TargetActorId: Guid.NewGuid()),
            modifiers);

        TalentRuntimeAction action = Assert.Single(actions);
        Assert.Equal(TalentRuntimeActionKind.ResourceChange, action.Kind);
        Assert.Equal(12, action.Value);
        Assert.Equal(1, state.Snapshot.Stacks["TALENT_A"]);
    }

    [Fact]
    public void PublishHonorsInternalCooldownAndDoesNotConsumeProcBlockedHook()
    {
        Guid ownerId = Guid.NewGuid();
        DateTimeOffset now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        TalentRuntimeState state = new(ownerId, new SequenceGameRandom(0));
        ResolvedTalentModifiers modifiers = Modifiers(
            new ResolvedTalentEventHook(
                "TALENT_A",
                TalentModifierKeys.OnCriticalHit,
                1,
                5,
                null,
                TimeSpan.FromSeconds(10),
                false));

        Assert.Single(state.Publish(Event(now, ownerId), modifiers));
        Assert.Empty(state.Publish(Event(now.AddSeconds(5), ownerId), modifiers));
        Assert.Empty(state.Publish(Event(now.AddSeconds(6), ownerId, isProc: true), modifiers));
        Assert.Single(state.Publish(Event(now.AddSeconds(11), ownerId), modifiers));
    }

    [Fact]
    public void PublishRejectsOutOfOrderSequencedEventsAndResetClearsRuntimeState()
    {
        Guid ownerId = Guid.NewGuid();
        TalentRuntimeState state = new(ownerId, new SequenceGameRandom(0));
        ResolvedTalentModifiers modifiers = Modifiers(
            new ResolvedTalentEventHook(
                "TALENT_A",
                TalentModifierKeys.OnCriticalHit,
                1,
                5,
                null,
                TimeSpan.Zero,
                false));

        state.Publish(Event(DateTimeOffset.UtcNow, ownerId, sequence: 2), modifiers);

        Assert.Throws<InvalidOperationException>(() =>
            state.Publish(Event(DateTimeOffset.UtcNow, ownerId, sequence: 1), modifiers));

        state.Reset();

        Assert.Empty(state.Snapshot.Stacks);
        Assert.Empty(state.Snapshot.InternalCooldowns);
        Assert.Single(state.Publish(Event(DateTimeOffset.UtcNow, ownerId, sequence: 1), modifiers));
    }

    [Fact]
    public void PublishMatchesDodgeHooksByOwnerTarget()
    {
        Guid ownerId = Guid.NewGuid();
        ResolvedTalentModifiers modifiers = Modifiers(
            new ResolvedTalentEventHook(
                "TALENT_DODGE",
                TalentModifierKeys.OnDodge,
                1,
                3,
                null,
                TimeSpan.Zero,
                false));
        TalentRuntimeState state = new(ownerId, new SequenceGameRandom(0));

        IReadOnlyList<TalentRuntimeAction> actions = state.Publish(
            new CombatRuntimeEvent(
                CombatRuntimeEventKind.Dodge,
                DateTimeOffset.UtcNow,
                Guid.NewGuid(),
                ownerId),
            modifiers);

        Assert.Single(actions);
    }

    private static CombatRuntimeEvent Event(
        DateTimeOffset occurredAtUtc,
        Guid ownerId,
        bool isProc = false,
        long sequence = 0) =>
        new(
            CombatRuntimeEventKind.CriticalHit,
            occurredAtUtc,
            ownerId,
            IsProc: isProc,
            Sequence: sequence);

    private static ResolvedTalentModifiers Modifiers(params ResolvedTalentEventHook[] hooks) =>
        ResolvedTalentModifiers.Empty with { EventHooks = hooks };
}
