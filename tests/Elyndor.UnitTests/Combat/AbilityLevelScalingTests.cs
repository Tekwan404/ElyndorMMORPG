using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class AbilityLevelScalingTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 18, 35, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 100, 195)]
    [InlineData(30, 100, 354.5)]
    [InlineData(60, 100, 519.5)]
    [InlineData(60, 200, 644.5)]
    public void DamageCombinesFixedLevelAndSpellPowerScaling(
        int level,
        decimal spellPower,
        decimal expectedDamage)
    {
        CombatRuntimeState runtime = CreateRuntime(level, spellPower);
        CombatActorState target = CombatActorState.CreateDummy(2_000);
        runtime.AddActor(target);
        AbilityDefinition fireball = CreateDamageAbility(
            amount: 70,
            spellPowerCoefficient: 1.25m,
            damagePerCharacterLevel: 5.5m);

        AbilityExecutionResult result = AbilityEngine.Execute(
            runtime,
            fireball,
            new AbilityIntent("fireball", fireball.Id, target.ActorId),
            Now,
            new SequenceGameRandom(0.9m));

        Assert.True(result.Succeeded);
        Assert.Equal(2_000m - expectedDamage, target.CurrentHp);
        CombatEvent damage = Assert.Single(
            result.Events.Where(item => item.Type == CombatEventType.DamageDealt));
        Assert.Equal(expectedDamage, damage.Amount);
    }

    [Fact]
    public void DamageWithoutLevelCoefficientKeepsLegacyBehaviorAtHighLevel()
    {
        CombatRuntimeState runtime = CreateRuntime(level: 60, spellPower: 100);
        CombatActorState target = CombatActorState.CreateDummy(500);
        runtime.AddActor(target);
        AbilityDefinition legacyStrike = CreateDamageAbility(
            amount: 25,
            spellPowerCoefficient: 0,
            damagePerCharacterLevel: 0);

        AbilityExecutionResult result = AbilityEngine.Execute(
            runtime,
            legacyStrike,
            new AbilityIntent("legacy", legacyStrike.Id, target.ActorId),
            Now,
            new SequenceGameRandom(0.9m));

        Assert.True(result.Succeeded);
        Assert.Equal(475m, target.CurrentHp);
    }

    private static CombatRuntimeState CreateRuntime(int level, decimal spellPower)
    {
        CombatStats stats = CombatStats.Default with
        {
            Level = level,
            SpellPower = spellPower
        };
        return new CombatRuntimeState(
            CombatActorState.CreateDummy(
                maxHp: 500,
                maxResource: 100,
                resource: 100,
                stats: stats));
    }

    private static AbilityDefinition CreateDamageAbility(
        decimal amount,
        decimal spellPowerCoefficient,
        decimal damagePerCharacterLevel) =>
        new(
            "TEST_LEVEL_SPELL",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            ResourceCost: 0,
            Cooldown: TimeSpan.Zero,
            CastTime: TimeSpan.Zero,
            UsesGlobalCooldown: false,
            GlobalCooldownCategory.None,
            IsSpell: true,
            School: "FIRE",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    amount,
                    DamageType.True,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false,
                    SpellPowerCoefficient: spellPowerCoefficient,
                    DamagePerCharacterLevel: damagePerCharacterLevel)
            ]);
}