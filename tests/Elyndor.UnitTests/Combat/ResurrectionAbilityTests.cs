using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;

namespace Elyndor.UnitTests.Combat;

public sealed class ResurrectionAbilityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    internal static AbilityDefinition Resurrection => new(
        "RESURRECTION", AbilityType.Casted, AbilityTargetType.SingleDeadAlly,
        80, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(6), false,
        GlobalCooldownCategory.None, true, "HOLY", AllowSelfTarget: false,
        Actions: [new(AbilityActionType.Resurrect, TargetMaxHpPercent: 30,
            TargetMaxResourcePercent: 20)]);

    [Fact]
    public void CompletionRevivesOnceWithoutResettingTargetRuntime()
    {
        var caster = CombatActorState.CreateDummy(100, maxResource: 200);
        var ally = CombatActorState.CreateDummy(100, maxResource: 100);
        ally.SetCurrentHp(0);
        var runtime = new CombatRuntimeState(caster);
        runtime.AddActor(ally);
        var allyRuntime = new CombatRuntimeState(ally);
        allyRuntime.Cooldowns["USED_ABILITY"] = Now.AddMinutes(2);

        Assert.True(AbilityEngine.Execute(runtime, Resurrection,
            new("cast", Resurrection.Id, ally.ActorId), Now).Succeeded);
        Assert.True(ally.IsDead);
        var completion = AbilityEngine.CompleteCast(runtime, Now.AddSeconds(6));
        Assert.True(completion.Succeeded);
        Assert.Equal(30, ally.CurrentHp);
        Assert.Equal(20, ally.CurrentResource);
        Assert.Equal(Now.AddMinutes(2), allyRuntime.Cooldowns["USED_ABILITY"]);
        Assert.Single(completion.Events, e => e.Type == CombatEventType.ActorResurrected);
        Assert.DoesNotContain(completion.Events, e => e.Type == CombatEventType.HealingApplied);
        Assert.Equal(AbilityErrorCode.NoActiveCast,
            AbilityEngine.CompleteCast(runtime, Now.AddSeconds(7)).ErrorCode);
    }

    [Fact]
    public void CompetingCastsDoNotRestoreHealthOrResourceTwice()
    {
        var ally = CombatActorState.CreateDummy(100, maxResource: 100);
        ally.SetCurrentHp(0);
        var first = new CombatRuntimeState(CombatActorState.CreateDummy(100, maxResource: 200));
        var second = new CombatRuntimeState(CombatActorState.CreateDummy(100, maxResource: 200));
        first.AddActor(ally);
        second.AddActor(ally);
        Assert.True(AbilityEngine.Execute(first, Resurrection, new("first", Resurrection.Id, ally.ActorId), Now).Succeeded);
        Assert.True(AbilityEngine.Execute(second, Resurrection, new("second", Resurrection.Id, ally.ActorId), Now).Succeeded);
        AbilityEngine.CompleteCast(first, Now.AddSeconds(6));
        ally.SetCurrentHp(10);
        ally.TrySpendResource(10);
        var duplicate = AbilityEngine.CompleteCast(second, Now.AddSeconds(6));
        Assert.Equal(10, ally.CurrentHp);
        Assert.Equal(10, ally.CurrentResource);
        Assert.DoesNotContain(duplicate.Events, e => e.Type == CombatEventType.ActorResurrected);
    }

    [Fact]
    public void DeadTimeDoesNotReplayPeriodicHealingOrDamageAfterRevival()
    {
        var ally = CombatActorState.CreateDummy(100);
        EffectEngine.Apply(ally, ally.ActorId,
            new EffectDefinition("OLD_DOT", EffectKind.DamageOverTime, TimeSpan.FromSeconds(30),
                1, EffectStackPolicy.Replace, 10, TickInterval: TimeSpan.FromSeconds(1)), Now);
        EffectEngine.Apply(ally, ally.ActorId,
            new EffectDefinition("OLD_STUN", EffectKind.Stun, TimeSpan.FromSeconds(2),
                1, EffectStackPolicy.Replace, 0), Now);
        ally.SetCurrentHp(0);
        var runtime = new CombatRuntimeState(CombatActorState.CreateDummy(100, maxResource: 200));
        runtime.AddActor(ally);
        AbilityEngine.Execute(runtime, Resurrection, new("res", Resurrection.Id, ally.ActorId), Now);
        AbilityEngine.CompleteCast(runtime, Now.AddSeconds(6));
        Assert.Equal(30, ally.CurrentHp);
        Assert.Empty(ally.ActiveEffects);
        Assert.Empty(EffectEngine.Process(ally, Now.AddSeconds(7)));
    }

    [Fact]
    public void LivingTargetIsRejectedWithoutSpendingMana()
    {
        var runtime = new CombatRuntimeState(CombatActorState.CreateDummy(100, maxResource: 200));
        var ally = CombatActorState.CreateDummy(100);
        runtime.AddActor(ally);
        Assert.Equal(AbilityErrorCode.InvalidTarget, AbilityEngine.Execute(runtime, Resurrection,
            new("invalid", Resurrection.Id, ally.ActorId), Now).ErrorCode);
        Assert.Equal(200, runtime.Actor.CurrentResource);
        Assert.Empty(runtime.Cooldowns);
    }
}
