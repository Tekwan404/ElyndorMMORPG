using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.UnitTests.Combat;

public sealed class DamageAndHealingPipelineTests
{
    [Fact]
    public void PhysicalCriticalUsesPenetrationMitigationAndNewestShield()
    {
        CombatActorState source = CombatActorState.CreateDummy(100, stats: new CombatStats(
            Level: 1, Accuracy: 100, Dodge: 0, CriticalChance: 100,
            CriticalDamage: 1, Armor: 0, MagicResistance: 0,
            ArmorPenetration: 0.2m, MagicPenetration: 0));
        CombatActorState target = CombatActorState.CreateDummy(200, stats: CombatStats.Default with { Armor = 100 });
        EffectDefinition shield = new(
            "TEST_SHIELD", EffectKind.Shield, TimeSpan.FromSeconds(10), 1,
            EffectStackPolicy.Independent, 25);
        EffectEngine.Apply(target, source.ActorId, shield, DateTimeOffset.UnixEpoch);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 100, DamageType.Physical),
            new SequenceGameRandom(0.9m, 0.9m, 0m));

        Assert.True(result.IsCritical);
        Assert.Equal(200, result.RawAmount);
        Assert.Equal(111, result.AfterMitigation);
        Assert.Equal(25, result.AbsorbedByShields);
        Assert.Equal(86, result.HpDamage);
        Assert.Equal(114, target.CurrentHp);
    }

    [Fact]
    public void SameRollResolvesMissBeforeDodge()
    {
        CombatActorState source = CombatActorState.CreateDummy(100, stats: CombatStats.Default with { Accuracy = 0 });
        CombatActorState target = CombatActorState.CreateDummy(100, stats: CombatStats.Default with { Dodge = 50 });

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 50, DamageType.Physical),
            new SequenceGameRandom(0.01m));

        Assert.Equal(DamageAvoidance.Miss, result.Avoidance);
        Assert.Equal(100, target.CurrentHp);
    }

    [Fact]
    public void HealingReportsOverhealAndCapsAtMaximumHp()
    {
        CombatActorState target = CombatActorState.CreateDummy(100);
        target.SetCurrentHp(80);

        HealingResult result = HealingPipeline.Resolve(new HealingRequest(target, 35));

        Assert.Equal(20, result.EffectiveHealing);
        Assert.Equal(15, result.Overheal);
        Assert.Equal(100, target.CurrentHp);
    }

    [Fact]
    public void LethalPreventionIsConsumedAndLeavesTargetAtOneHp()
    {
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(50);
        EffectEngine.Apply(target, source.ActorId,
            new EffectDefinition("TEST_CHEAT_DEATH", EffectKind.LethalDamagePrevention,
                TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 1),
            DateTimeOffset.UnixEpoch);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 500, DamageType.True, CanCrit: false),
            new SequenceGameRandom(0.9m));

        Assert.True(result.LethalPreventionTriggered);
        Assert.False(result.IsLethal);
        Assert.Equal(1, target.CurrentHp);
        Assert.DoesNotContain(target.ActiveEffects,
            effect => effect.Definition.Kind == EffectKind.LethalDamagePrevention);
    }
}
