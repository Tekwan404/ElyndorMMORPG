using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class WarriorAbilityKitTests
{
    [Fact]
    public void StrikeScalesFromAttackPowerAndGeneratesRageExactlyOnce()
    {
        CombatActorState warrior = CombatActorState.CreateDummy(
            200,
            resource: 0,
            stats: CombatStats.Default with { Accuracy = 100, AttackPower = 100 });
        CombatActorState dummy = CombatActorState.CreateDummy(500);
        CombatRuntimeState runtime = new(warrior);
        runtime.AddActor(dummy);
        AbilityDefinition strike = new(
            "STRIKE", AbilityType.Instant, AbilityTargetType.SingleEnemy,
            0, TimeSpan.Zero, TimeSpan.Zero, true,
            GlobalCooldownCategory.Standard, false, "PHYSICAL",
            Actions:
            [
                new AbilityActionDefinition(AbilityActionType.Damage,
                    DamageType: DamageType.Physical, CanCrit: false,
                    CanDodge: false, AttackPowerCoefficient: 0.8m),
                new AbilityActionDefinition(AbilityActionType.ResourceChange, Amount: 10)
            ]);
        AbilityIntent intent = new("strike-1", strike.Id, dummy.ActorId);

        AbilityExecutionResult result = AbilityEngine.Execute(
            runtime, strike, intent, DateTimeOffset.UnixEpoch,
            new SequenceGameRandom(0.9m));
        AbilityExecutionResult duplicate = AbilityEngine.Execute(
            runtime, strike, intent, DateTimeOffset.UnixEpoch,
            new SequenceGameRandom(0.9m));

        Assert.True(result.Succeeded);
        Assert.Equal(420, dummy.CurrentHp);
        Assert.Equal(10, warrior.CurrentResource);
        Assert.Equal(AbilityErrorCode.DuplicateCommand, duplicate.ErrorCode);
        Assert.Equal(10, warrior.CurrentResource);
    }
}
