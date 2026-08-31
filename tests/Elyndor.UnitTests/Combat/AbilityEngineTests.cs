using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class AbilityEngineTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InstantAbilitySpendsResourceAndStartsCooldownAndGcdOnce()
    {
        CombatRuntimeState runtime = CreateRuntime(resource: 50);
        AbilityDefinition ability = Instant("TEST_STRIKE", cost: 20, cooldownSeconds: 5);
        AbilityIntent intent = new("command-1", ability.Id, runtime.Actor.ActorId);

        AbilityExecutionResult first = AbilityEngine.Execute(runtime, ability, intent, Now);
        AbilityExecutionResult duplicate = AbilityEngine.Execute(runtime, ability, intent, Now);

        Assert.True(first.Succeeded);
        Assert.Equal(30, runtime.Actor.CurrentResource);
        Assert.Equal(Now.AddSeconds(5), runtime.Cooldowns[ability.Id]);
        Assert.Equal(Now.AddSeconds(1.5), runtime.GlobalCooldownEndsAtUtc);
        Assert.Equal(AbilityErrorCode.DuplicateCommand, duplicate.ErrorCode);
        Assert.Equal(30, runtime.Actor.CurrentResource);
    }

    [Fact]
    public void CastSpendsAtStartCompletesAtExactBoundaryAndThenStartsCooldown()
    {
        CombatRuntimeState runtime = CreateRuntime(resource: 50);
        AbilityDefinition ability = Instant("TEST_CAST", 10, 8) with
        {
            Type = AbilityType.Casted,
            CastTime = TimeSpan.FromSeconds(2),
            School = "ARCANE"
        };

        AbilityExecutionResult started = AbilityEngine.Execute(
            runtime, ability, new AbilityIntent("start", ability.Id, runtime.Actor.ActorId), Now);
        AbilityExecutionResult early = AbilityEngine.CompleteCast(runtime, Now.AddSeconds(1));
        AbilityExecutionResult completed = AbilityEngine.CompleteCast(runtime, Now.AddSeconds(2));

        Assert.True(started.Succeeded);
        Assert.Equal(AbilityErrorCode.CastNotReady, early.ErrorCode);
        Assert.True(completed.Succeeded);
        Assert.Null(runtime.ActiveCast);
        Assert.Equal(Now.AddSeconds(10), runtime.Cooldowns[ability.Id]);
    }

    [Fact]
    public void InterruptAppliesSchoolLockoutButDoesNotStartInterruptedAbilityCooldown()
    {
        CombatRuntimeState runtime = CreateRuntime(resource: 50);
        AbilityDefinition ability = Instant("TEST_CAST", 10, 8) with
        {
            Type = AbilityType.Casted,
            CastTime = TimeSpan.FromSeconds(2),
            School = "FIRE"
        };
        AbilityEngine.Execute(runtime, ability, new AbilityIntent("start", ability.Id, runtime.Actor.ActorId), Now);

        AbilityExecutionResult result = AbilityEngine.Interrupt(runtime, Now.AddSeconds(1), TimeSpan.FromSeconds(3));

        Assert.True(result.Succeeded);
        Assert.Null(runtime.ActiveCast);
        Assert.False(runtime.Cooldowns.ContainsKey(ability.Id));
        Assert.Equal(Now.AddSeconds(4), runtime.SchoolLockouts["FIRE"]);
    }

    [Fact]
    public void StunAndSilenceRejectOnlyTheirDocumentedAbilityCategories()
    {
        CombatRuntimeState runtime = CreateRuntime(resource: 50);
        EffectEngine.Apply(runtime.Actor, runtime.Actor.ActorId,
            new EffectDefinition("TEST_SILENCE", EffectKind.Silence, TimeSpan.FromSeconds(2), 1, EffectStackPolicy.Replace, 0), Now);

        AbilityExecutionResult spell = AbilityEngine.Execute(
            runtime, Instant("TEST_SPELL", 0, 0) with { IsSpell = true },
            new AbilityIntent("spell", "TEST_SPELL", runtime.Actor.ActorId), Now);
        AbilityExecutionResult physical = AbilityEngine.Execute(
            runtime, Instant("TEST_PHYSICAL", 0, 0) with
            {
                UsesGlobalCooldown = false,
                CanUseWhileSilenced = true
            },
            new AbilityIntent("physical", "TEST_PHYSICAL", runtime.Actor.ActorId), Now);

        Assert.Equal(AbilityErrorCode.ActorSilenced, spell.ErrorCode);
        Assert.True(physical.Succeeded);
    }

    [Fact]
    public void AbilityDamageActionUsesAuthoritativePipelineAgainstRuntimeTarget()
    {
        CombatRuntimeState runtime = CreateRuntime(resource: 50);
        CombatActorState target = CombatActorState.CreateDummy(100);
        runtime.AddActor(target);
        AbilityDefinition ability = Instant("TEST_HIT", 5, 0) with
        {
            TargetType = AbilityTargetType.SingleEnemy,
            Actions =
            [
                new AbilityActionDefinition(AbilityActionType.Damage, 25,
                    DamageType.True, CanMiss: false, CanCrit: false, CanDodge: false)
            ]
        };

        AbilityExecutionResult result = AbilityEngine.Execute(runtime, ability,
            new AbilityIntent("hit", ability.Id, target.ActorId), Now,
            new SequenceGameRandom(0.9m));

        Assert.True(result.Succeeded);
        Assert.Equal(75, target.CurrentHp);
        Assert.Contains(result.Events, combatEvent => combatEvent.Type == CombatEventType.DamageApplied);
    }

    private static CombatRuntimeState CreateRuntime(decimal resource) =>
        new(CombatActorState.CreateDummy(100, 100, resource));

    private static AbilityDefinition Instant(string id, decimal cost, double cooldownSeconds) =>
        new(id, AbilityType.Instant, AbilityTargetType.Self, cost,
            TimeSpan.FromSeconds(cooldownSeconds), TimeSpan.Zero, true,
            GlobalCooldownCategory.Standard, false, "PHYSICAL");
}
