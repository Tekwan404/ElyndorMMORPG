using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class TrainingDummyCombatTests
{
    [Fact]
    public void NonLethalActorStopsAtOneHpAndNeverEmitsDeath()
    {
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(
            100,
            canDie: false);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                source,
                target,
                500,
                DamageType.True,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false),
            new SequenceGameRandom());

        Assert.Equal(1, target.CurrentHp);
        Assert.False(target.IsDead);
        Assert.False(result.IsLethal);
        Assert.Equal(500, result.ModifiedAmount);
        Assert.DoesNotContain(
            result.Events,
            combatEvent => combatEvent.Type == CombatEventType.ActorDied);
    }
}
