using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Effects;

namespace Elyndor.UnitTests.Combat;

public sealed class EffectApplicationPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static EffectDefinition Stun => new("CONTROL", EffectKind.Stun,
        TimeSpan.FromSeconds(8), 1, EffectStackPolicy.Refresh, 0);

    [Fact]
    public void PolicyAdjustsDurationOnApplicationAndRefreshAndCommitsAfterMutation()
    {
        CombatActorState target = CombatActorState.CreateDummy(100);
        Guid source = Guid.NewGuid();
        int commits = 0;
        target.IncomingControlDurationMultiplier = 0.5m;
        target.EffectApplicationPolicy = (sourceId, definition, now) =>
        {
            Assert.Equal(source, sourceId);
            Assert.Equal(TimeSpan.FromSeconds(4), definition.Duration);
            return new(definition with { Duration = TimeSpan.FromSeconds(commits == 0 ? 2 : 1) }, () =>
            {
                Assert.Equal(now.AddSeconds(commits == 0 ? 2 : 1),
                    Assert.Single(target.ActiveEffects).ExpiresAtUtc);
                commits++;
            });
        };

        EffectEngine.Apply(target, source, Stun, Now);
        EffectEngine.Apply(target, source, Stun, Now.AddSeconds(1));

        Assert.Equal(2, commits);
        Assert.Equal(Now.AddSeconds(2), Assert.Single(target.ActiveEffects).ExpiresAtUtc);
    }

    [Fact]
    public void RejectionEmitsImmunityWithoutEffectOrCommit()
    {
        CombatActorState target = CombatActorState.CreateDummy(100);
        Guid source = Guid.NewGuid();
        bool committed = false;
        target.EffectApplicationPolicy = (_, _, _) => new(null, () => committed = true);

        CombatEvent immunity = Assert.Single(EffectEngine.Apply(target, source, Stun, Now));

        Assert.Equal(CombatEventType.EffectImmune, immunity.Type);
        Assert.Equal(Stun.Id, immunity.DefinitionId);
        Assert.Equal(source, immunity.SourceActorId);
        Assert.Equal(target.ActorId, immunity.TargetActorId);
        Assert.Equal(Now, immunity.OccurredAtUtc);
        Assert.Empty(target.ActiveEffects);
        Assert.False(committed);
    }

    [Fact]
    public void PolicyIsPerActorAndOrdinaryEffectsRemainUnchanged()
    {
        CombatActorState target = CombatActorState.CreateDummy(100);
        CombatActorState other = CombatActorState.CreateDummy(100);
        EffectDefinition shield = Stun with { Kind = EffectKind.Shield, Magnitude = 20 };
        target.EffectApplicationPolicy = (_, definition, _) => new(definition);

        EffectEngine.Apply(target, other.ActorId, shield, Now);
        EffectEngine.Apply(other, target.ActorId, Stun, Now);

        Assert.Same(shield, Assert.Single(target.ActiveEffects).Definition);
        Assert.Equal(Now + Stun.Duration, Assert.Single(other.ActiveEffects).ExpiresAtUtc);
    }

    [Fact]
    public void InvalidAdjustedDefinitionDoesNotApplyOrCommit()
    {
        CombatActorState target = CombatActorState.CreateDummy(100);
        bool committed = false;
        target.EffectApplicationPolicy = (_, definition, _) =>
            new(definition with { Duration = TimeSpan.Zero }, () => committed = true);

        Assert.Throws<ArgumentException>(() => EffectEngine.Apply(target, target.ActorId, Stun, Now));
        Assert.Empty(target.ActiveEffects);
        Assert.False(committed);
    }

    [Fact]
    public void StrongestWinsIgnoredApplicationDoesNotCommit()
    {
        CombatActorState target = CombatActorState.CreateDummy(100);
        EffectDefinition shield = Stun with
        { Kind = EffectKind.Shield, StackPolicy = EffectStackPolicy.StrongestWins, Magnitude = 20 };
        EffectEngine.Apply(target, target.ActorId, shield, Now);
        bool committed = false;
        target.EffectApplicationPolicy = (_, definition, _) => new(definition, () => committed = true);

        Assert.Empty(EffectEngine.Apply(target, target.ActorId, shield with { Magnitude = 10 }, Now));
        Assert.False(committed);
        Assert.Equal(20, Assert.Single(target.ActiveEffects).RemainingMagnitude);
    }
}
