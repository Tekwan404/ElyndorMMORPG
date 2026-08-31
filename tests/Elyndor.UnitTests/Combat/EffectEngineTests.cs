using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Effects;

namespace Elyndor.UnitTests.Combat;

public sealed class EffectEngineTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StackPolicyAddsStacksAndRefreshesDurationWithoutDuplicatingInstance()
    {
        CombatActorState target = CombatActorState.CreateDummy(100);
        EffectDefinition definition = new(
            "TEST_BLEED",
            EffectKind.DamageOverTime,
            TimeSpan.FromSeconds(6),
            4,
            EffectStackPolicy.Stack,
            2,
            TimeSpan.FromSeconds(2));

        EffectEngine.Apply(target, target.ActorId, definition, Now);
        EffectEngine.Apply(target, target.ActorId, definition, Now.AddSeconds(1));

        ActiveEffect effect = Assert.Single(target.ActiveEffects);
        Assert.Equal(2, effect.Stacks);
        Assert.Equal(Now.AddSeconds(7), effect.ExpiresAtUtc);
    }

    [Fact]
    public void PeriodicEffectsTickDeterministicallyAtBoundaryThenExpire()
    {
        CombatActorState target = CombatActorState.CreateDummy(100);
        EffectDefinition definition = new(
            "TEST_BURN",
            EffectKind.DamageOverTime,
            TimeSpan.FromSeconds(4),
            1,
            EffectStackPolicy.Replace,
            10,
            TimeSpan.FromSeconds(2));
        EffectEngine.Apply(target, target.ActorId, definition, Now);

        IReadOnlyList<CombatEvent> events = EffectEngine.Process(target, Now.AddSeconds(4));

        Assert.Equal(80, target.CurrentHp);
        Assert.Equal(2, events.Count(e => e.Type == CombatEventType.EffectTicked));
        Assert.Contains(events, e => e.Type == CombatEventType.EffectExpired);
        Assert.Empty(target.ActiveEffects);
    }

    [Fact]
    public void StunAndSilenceRemainIndependentControlStates()
    {
        CombatActorState target = CombatActorState.CreateDummy(100);
        EffectEngine.Apply(target, target.ActorId, Control("TEST_STUN", EffectKind.Stun), Now);
        EffectEngine.Apply(target, target.ActorId, Control("TEST_SILENCE", EffectKind.Silence), Now);

        Assert.True(EffectEngine.HasControl(target, EffectKind.Stun, Now));
        Assert.True(EffectEngine.HasControl(target, EffectKind.Silence, Now));
        Assert.False(EffectEngine.HasControl(target, EffectKind.Stun, Now.AddSeconds(4)));
    }

    private static EffectDefinition Control(string id, EffectKind kind) =>
        new(id, kind, TimeSpan.FromSeconds(3), 1, EffectStackPolicy.Replace, 0);
}
