using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class PveDelayedAbilityActionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 7, 30, 0, TimeSpan.Zero);

    [Fact]
    public void DelayedDamageDoesNotResolveBeforeDeadlineAndUsesScheduledTimestamp()
    {
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(100);
        CombatRuntimeState runtime = new(source);
        runtime.AddActor(target);
        AbilityDefinition bomb = DelayedDamageAbility("TEST_BOMB", 25, TimeSpan.FromSeconds(2));
        SequenceGameRandom random = new(0.5m);

        AbilityExecutionResult started = AbilityEngine.Execute(
            runtime,
            bomb,
            new AbilityIntent("bomb-1", bomb.Id, target.ActorId),
            Now,
            random);

        Assert.True(started.Succeeded);
        Assert.DoesNotContain(started.Events, item => item.Type == CombatEventType.DamageDealt);
        Assert.Equal(100, target.CurrentHp);
        Assert.Equal(Now.AddSeconds(2), runtime.NextPendingActionAtUtc);
        Assert.Empty(AbilityEngine.ResolvePendingActions(
            runtime,
            Now.AddSeconds(1),
            random));
        Assert.Equal(100, target.CurrentHp);

        IReadOnlyList<CombatEvent> resolved = AbilityEngine.ResolvePendingActions(
            runtime,
            Now.AddSeconds(2),
            random);

        CombatEvent damage = Assert.Single(
            resolved,
            item => item.Type == CombatEventType.DamageDealt);
        Assert.Equal(25, damage.Amount);
        Assert.Equal(target.ActorId, damage.TargetActorId);
        Assert.Equal(source.ActorId, damage.SourceActorId);
        Assert.Equal(bomb.Id, damage.DefinitionId);
        Assert.Equal(Now.AddSeconds(2), damage.OccurredAtUtc);
        Assert.Equal(75, target.CurrentHp);
        Assert.Null(runtime.NextPendingActionAtUtc);
    }

    [Fact]
    public void DelayedMultiTargetDamageLocksTargetsAndResolvesInDeterministicOrder()
    {
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState first = CombatActorState.CreateDummy(100);
        CombatActorState second = CombatActorState.CreateDummy(100);
        CombatRuntimeState runtime = new(source);
        runtime.AddActor(first);
        runtime.AddActor(second);
        AbilityDefinition splash = DelayedDamageAbility(
            "TEST_SPLASH",
            10,
            TimeSpan.FromSeconds(3)) with
        {
            TargetType = AbilityTargetType.NEnemiesInCombat,
            TargetCount = 2
        };
        SequenceGameRandom random = new(0.5m, 0.5m);

        AbilityExecutionResult started = AbilityEngine.Execute(
            runtime,
            splash,
            new AbilityIntent(
                "splash-1",
                splash.Id,
                first.ActorId,
                [first.ActorId, second.ActorId]),
            Now,
            random);

        Assert.True(started.Succeeded);
        Assert.Equal(2, runtime.PendingActions.Count);
        Assert.Equal(
            [first.ActorId, second.ActorId],
            runtime.PendingActions.OrderBy(item => item.Sequence).Select(item => item.TargetId));

        IReadOnlyList<CombatEvent> resolved = AbilityEngine.ResolvePendingActions(
            runtime,
            Now.AddSeconds(3),
            random);
        CombatEvent[] damage = resolved
            .Where(item => item.Type == CombatEventType.DamageDealt)
            .ToArray();

        Assert.Equal(2, damage.Length);
        Assert.Equal([first.ActorId, second.ActorId], damage.Select(item => item.TargetActorId));
        Assert.All(damage, item => Assert.Equal(Now.AddSeconds(3), item.OccurredAtUtc));
        Assert.Equal(90, first.CurrentHp);
        Assert.Equal(90, second.CurrentHp);
        Assert.Empty(runtime.PendingActions);
    }

    [Fact]
    public void DelayedActionSkipsTargetThatDiedBeforeResolution()
    {
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(100);
        CombatRuntimeState runtime = new(source);
        runtime.AddActor(target);
        AbilityDefinition bomb = DelayedDamageAbility("TEST_DEAD_TARGET", 25, TimeSpan.FromSeconds(2));
        SequenceGameRandom random = new(0.5m);

        Assert.True(AbilityEngine.Execute(
            runtime,
            bomb,
            new AbilityIntent("dead-target", bomb.Id, target.ActorId),
            Now,
            random).Succeeded);
        target.SetCurrentHp(0);

        Assert.Empty(AbilityEngine.ResolvePendingActions(
            runtime,
            Now.AddSeconds(2),
            random));
        Assert.Empty(runtime.PendingActions);
    }

    private static AbilityDefinition DelayedDamageAbility(
        string id,
        decimal damage,
        TimeSpan delay) =>
        new(
            id,
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "ARCANE",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    Amount: damage,
                    DamageType: DamageType.True,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false,
                    Delay: delay)
            ]);
}
