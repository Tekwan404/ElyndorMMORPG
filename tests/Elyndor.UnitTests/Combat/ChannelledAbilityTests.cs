using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class ChannelledAbilityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void ChannelStartsImmediatelyTicksAtBoundariesAndCompletesExactlyOnce()
    {
        var (runtime, ability, enemy) = Create();
        Assert.True(AbilityEngine.Execute(runtime, ability, new("start", ability.Id, enemy.ActorId), Now, Random()).Succeeded);
        Assert.Equal(1000, enemy.CurrentHp);
        Assert.Equal(80, runtime.Actor.CurrentResource);
        Assert.Equal(Now.AddSeconds(10), runtime.Cooldowns[ability.Id]);
        Assert.Equal(Now.AddSeconds(1), runtime.ActiveCast!.NextResolutionAtUtc);
        Assert.False(AbilityEngine.CompleteCast(runtime, Now.AddMilliseconds(999), Random()).Succeeded);
        var first = AbilityEngine.CompleteCast(runtime, Now.AddSeconds(1), Random());
        Assert.Equal(975, enemy.CurrentHp);
        Assert.DoesNotContain(first.Events, e => e.Type == CombatEventType.AbilityCompleted);
        Assert.False(AbilityEngine.CompleteCast(runtime, Now.AddSeconds(1), Random()).Succeeded);
        var rest = AbilityEngine.CompleteCast(runtime, Now.AddSeconds(4), Random());
        Assert.Equal(900, enemy.CurrentHp);
        Assert.Single(rest.Events, e => e.Type == CombatEventType.AbilityCompleted);
        Assert.Null(runtime.ActiveCast);
        Assert.False(AbilityEngine.CompleteCast(runtime, Now.AddSeconds(4), Random()).Succeeded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptOrSilenceKeepsPastTicksAndCancelsFutureTicks(bool silence)
    {
        var (runtime, ability, enemy) = Create();
        AbilityEngine.Execute(runtime, ability, new("start", ability.Id, enemy.ActorId), Now, Random());
        AbilityEngine.CompleteCast(runtime, Now.AddSeconds(1), Random());
        if (silence)
            EffectEngine.Apply(runtime.Actor, enemy.ActorId,
                new("SILENCE", EffectKind.Silence, TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 0), Now.AddSeconds(1));
        else
            AbilityEngine.Interrupt(runtime, Now.AddSeconds(1), TimeSpan.FromSeconds(3));
        var result = AbilityEngine.CompleteCast(runtime, Now.AddSeconds(4), Random());
        Assert.Equal(975, enemy.CurrentHp);
        Assert.Null(runtime.ActiveCast);
        Assert.DoesNotContain(result.Events, e => e.Type == CombatEventType.AbilityCompleted);
        Assert.Equal(80, runtime.Actor.CurrentResource);
        Assert.Equal(Now.AddSeconds(10), runtime.Cooldowns[ability.Id]);
    }

    [Fact]
    public void ChannelRefundUsesCasterCapacityAtEachTickAndStopsOnDeath()
    {
        var (runtime, ability, _) = Create();
        ability = ability with { TargetType = AbilityTargetType.Self, ResourceCost = 0,
            Actions = [new(AbilityActionType.ResourceChange, CasterMaxResourcePercent: 10)] };
        runtime.Actor.TrySpendResource(90);
        AbilityEngine.Execute(runtime, ability, new("start", ability.Id, runtime.Actor.ActorId), Now);
        AbilityEngine.CompleteCast(runtime, Now.AddSeconds(1));
        Assert.Equal(20, runtime.Actor.CurrentResource);
        runtime.Actor.ApplyDamage(1000);
        AbilityEngine.CompleteCast(runtime, Now.AddSeconds(4));
        Assert.Equal(20, runtime.Actor.CurrentResource);
        Assert.Null(runtime.ActiveCast);
    }

    private static (CombatRuntimeState Runtime, AbilityDefinition Ability, CombatActorState Enemy) Create()
    {
        var owner = new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 100, CombatStats.Default);
        var enemy = new CombatActorState(Guid.NewGuid(), 1000, 1000, 0, 0, CombatStats.Default);
        var runtime = new CombatRuntimeState(owner);
        runtime.AddActor(enemy);
        var ability = new AbilityDefinition("CHANNEL", AbilityType.Channelled, AbilityTargetType.SingleEnemy,
            20, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(4), false, GlobalCooldownCategory.None, true, "ARCANE",
            Actions: [new(AbilityActionType.Damage, 25, DamageType.True, CanMiss: false, CanCrit: false, CanDodge: false)],
            ChannelTickInterval: TimeSpan.FromSeconds(1));
        return (runtime, ability, enemy);
    }

    private static SequenceGameRandom Random() => new(.99m, .99m, .99m, .99m);
}
