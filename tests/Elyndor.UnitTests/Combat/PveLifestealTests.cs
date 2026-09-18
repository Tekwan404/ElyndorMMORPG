using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class PveLifestealTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void LifestealUsesActualHpDamageAfterOverkill()
    {
        CombatActorState caster = CombatActorState.CreateDummy(200);
        caster.SetCurrentHp(100);
        CombatActorState target = CombatActorState.CreateDummy(200);
        target.SetCurrentHp(40);
        CombatRuntimeState runtime = new(caster);
        runtime.AddActor(target);
        AbilityDefinition ability = DamageAbility(
            "SOUL_BITE",
            damage: 100,
            lifestealPercent: 50);

        AbilityExecutionResult result = AbilityEngine.Execute(
            runtime,
            ability,
            new AbilityIntent("soul-bite", ability.Id, target.ActorId),
            Now,
            new SequenceGameRandom(0.5m));

        Assert.True(result.Succeeded);
        Assert.Equal(0, target.CurrentHp);
        Assert.Equal(120, caster.CurrentHp);
        CombatEvent healing = Assert.Single(result.Events, combatEvent =>
            combatEvent.Type == CombatEventType.HealingApplied);
        Assert.Equal(20, healing.Amount);
        Assert.Equal(HealingOrigin.Secondary, healing.HealingOrigin);
        Assert.Equal(caster.ActorId, healing.SourceActorId);
        Assert.Equal(caster.ActorId, healing.TargetActorId);
    }

    [Fact]
    public void CastedLifestealResolvesWhenCastCompletes()
    {
        CombatActorState caster = CombatActorState.CreateDummy(1_000);
        caster.SetCurrentHp(100);
        CombatActorState target = CombatActorState.CreateDummy(1_000);
        CombatRuntimeState runtime = new(caster);
        runtime.AddActor(target);
        AbilityDefinition ability = DamageAbility(
            "SOUL_DRAIN",
            damage: 100,
            lifestealPercent: 70) with
        {
            Type = AbilityType.Casted,
            CastTime = TimeSpan.FromSeconds(2)
        };

        AbilityExecutionResult started = AbilityEngine.Execute(
            runtime,
            ability,
            new AbilityIntent("soul-drain", ability.Id, target.ActorId),
            Now,
            new SequenceGameRandom(0.5m));
        AbilityExecutionResult completed = AbilityEngine.CompleteCast(
            runtime,
            Now.AddSeconds(2),
            new SequenceGameRandom(0.5m));

        Assert.True(started.Succeeded);
        Assert.Equal(100, caster.CurrentHp);
        Assert.True(completed.Succeeded);
        Assert.Equal(900, target.CurrentHp);
        Assert.Equal(170, caster.CurrentHp);
        Assert.Contains(completed.Events, combatEvent =>
            combatEvent.Type == CombatEventType.HealingApplied
            && combatEvent.Amount == 70
            && combatEvent.HealingOrigin == HealingOrigin.Secondary);
    }

    [Fact]
    public void FullyAbsorbedDamageDoesNotTriggerLifesteal()
    {
        CombatActorState caster = CombatActorState.CreateDummy(200);
        caster.SetCurrentHp(100);
        CombatActorState target = CombatActorState.CreateDummy(200);
        EffectEngine.Apply(
            target,
            target.ActorId,
            new EffectDefinition(
                "FULL_SHIELD",
                EffectKind.Shield,
                TimeSpan.FromSeconds(10),
                1,
                EffectStackPolicy.Replace,
                200),
            Now);
        CombatRuntimeState runtime = new(caster);
        runtime.AddActor(target);
        AbilityDefinition ability = DamageAbility(
            "SHIELDED_BITE",
            damage: 100,
            lifestealPercent: 50);

        AbilityExecutionResult result = AbilityEngine.Execute(
            runtime,
            ability,
            new AbilityIntent("shielded-bite", ability.Id, target.ActorId),
            Now,
            new SequenceGameRandom(0.5m));

        Assert.True(result.Succeeded);
        Assert.Equal(200, target.CurrentHp);
        Assert.Equal(100, caster.CurrentHp);
        Assert.DoesNotContain(result.Events, combatEvent =>
            combatEvent.Type == CombatEventType.HealingApplied);
    }

    [Fact]
    public void LifestealIsRejectedOnNonDamageAction()
    {
        CombatActorState caster = CombatActorState.CreateDummy(200);
        CombatRuntimeState runtime = new(caster);
        AbilityDefinition invalid = new(
            "INVALID_LIFESTEAL",
            AbilityType.Instant,
            AbilityTargetType.Self,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "SHADOW",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Healing,
                    Amount: 10,
                    LifestealPercent: 50)
            ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            AbilityEngine.Execute(
                runtime,
                invalid,
                new AbilityIntent("invalid", invalid.Id, caster.ActorId),
                Now));

        Assert.Contains("Lifesteal", exception.Message, StringComparison.Ordinal);
    }

    private static AbilityDefinition DamageAbility(
        string id,
        decimal damage,
        decimal lifestealPercent) =>
        new(
            id,
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            true,
            "SHADOW",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    Amount: damage,
                    DamageType: DamageType.True,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false,
                    LifestealPercent: lifestealPercent)
            ]);
}
