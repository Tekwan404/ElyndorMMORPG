using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Talents;

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
    public void DodgeProducesACombatEventForReactiveTalents()
    {
        DateTimeOffset now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with { Dodge = 50 });

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 50, DamageType.Physical, CanMiss: false),
            new SequenceGameRandom(0.25m),
            now);

        Assert.Equal(DamageAvoidance.Dodge, result.Avoidance);
        Assert.Contains(result.Events, combatEvent =>
            combatEvent.Type == CombatEventType.Dodge
            && combatEvent.TargetActorId == target.ActorId
            && combatEvent.OccurredAtUtc == now);
    }

    [Fact]
    public void PhysicalDamageCanBeBlockedBeforeAbsorbShields()
    {
        CombatActorState source = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with { Accuracy = 100 });
        CombatActorState target = CombatActorState.CreateDummy(
            200,
            stats: CombatStats.Default with
            {
                BlockChance = 100,
                BlockValueMin = 20,
                BlockValueMax = 20
            });
        EffectDefinition shield = new(
            "TEST_ABSORB",
            EffectKind.Shield,
            TimeSpan.FromSeconds(10),
            1,
            EffectStackPolicy.Independent,
            15);
        EffectEngine.Apply(
            target,
            source.ActorId,
            shield,
            DateTimeOffset.UnixEpoch);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                source,
                target,
                100,
                DamageType.Physical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false),
            new SequenceGameRandom(0m),
            DateTimeOffset.UnixEpoch);

        Assert.True(result.WasBlocked);
        Assert.Equal(20, result.BlockedAmount);
        Assert.Equal(15, result.AbsorbedByShields);
        Assert.Equal(65, result.HpDamage);
        Assert.Equal(135, target.CurrentHp);

        CombatEvent blockEvent = Assert.Single(
            result.Events,
            combatEvent => combatEvent.Type == CombatEventType.DamageBlocked);
        Assert.Equal(20, blockEvent.Amount);
        Assert.Equal(100, blockEvent.RawDamage);
        Assert.Equal(100, blockEvent.DamageAfterMitigation);
        Assert.Equal(100, blockEvent.DamageBeforeBlock);
        Assert.Equal(80, blockEvent.AmountBeforeShields);

        CombatEvent damageEvent = Assert.Single(
            result.Events,
            combatEvent => combatEvent.Type == CombatEventType.DamageDealt);
        Assert.Equal(65, damageEvent.Amount);
        Assert.Equal(100, damageEvent.RawDamage);
        Assert.Equal(100, damageEvent.DamageAfterMitigation);
        Assert.Equal(100, damageEvent.DamageBeforeBlock);
        Assert.Equal(80, damageEvent.AmountBeforeShields);
    }

    [Fact]
    public void DamageEventBreakdownPreservesArmorThenBlockOrder()
    {
        CombatActorState source = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with { Accuracy = 100 });
        CombatActorState target = CombatActorState.CreateDummy(
            200,
            stats: CombatStats.Default with
            {
                Armor = 100,
                BlockChance = 100,
                BlockValueMin = 20,
                BlockValueMax = 20
            });

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                source,
                target,
                100,
                DamageType.Physical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false),
            new SequenceGameRandom(0m),
            DateTimeOffset.UnixEpoch);

        CombatEvent blockEvent = Assert.Single(
            result.Events,
            combatEvent => combatEvent.Type == CombatEventType.DamageBlocked);
        CombatEvent damageEvent = Assert.Single(
            result.Events,
            combatEvent => combatEvent.Type == CombatEventType.DamageDealt);

        Assert.Equal(100, blockEvent.RawDamage);
        Assert.Equal(50, blockEvent.DamageAfterMitigation);
        Assert.Equal(50, blockEvent.DamageBeforeBlock);
        Assert.Equal(20, blockEvent.Amount);
        Assert.Equal(30, blockEvent.AmountBeforeShields);
        Assert.Equal(30, damageEvent.Amount);
    }

    [Fact]
    public void MagicalDamageCannotBeBlocked()
    {
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(
            200,
            stats: CombatStats.Default with
            {
                BlockChance = 100,
                BlockValueMin = 20,
                BlockValueMax = 20
            });

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                source,
                target,
                100,
                DamageType.Magical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false),
            new SequenceGameRandom());

        Assert.False(result.WasBlocked);
        Assert.Equal(0, result.BlockedAmount);
        Assert.Equal(100, result.HpDamage);
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

    [Fact]
    public void IncomingDamageMultiplierEffectParticipatesInDamagePipeline()
    {
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(100);
        EffectEngine.Apply(target, target.ActorId,
            new EffectDefinition(
                "BASTION_DAMAGE_REDUCTION", EffectKind.StatModifier,
                TimeSpan.FromSeconds(6), 1, EffectStackPolicy.Refresh, 0.7m,
                ModifiedStat: EffectStat.IncomingDamageMultiplier,
                ModifierMode: EffectModifierMode.Multiplicative),
            DateTimeOffset.UnixEpoch);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 100, DamageType.True, CanCrit: false),
            new SequenceGameRandom(0.9m),
            DateTimeOffset.UnixEpoch);

        Assert.Equal(70, result.HpDamage);
        Assert.Equal(30, target.CurrentHp);
    }

    [Fact]
    public void TalentDamageModifiersReduceIncomingDamageAndApplyVampirism()
    {
        CombatActorState source = CombatActorState.CreateDummy(
            100,
            talentModifiers: new TalentCombatModifiers(
                DamageDealtPercent: 10,
                VampirismPercent: 10));
        source.SetCurrentHp(50);
        CombatActorState target = CombatActorState.CreateDummy(
            200,
            talentModifiers: new TalentCombatModifiers(
                IncomingPhysicalDamageReductionPercent: 20));

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                source, target, 100, DamageType.Physical,
                CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom(0.9m));

        Assert.Equal(88, result.HpDamage);
        Assert.Equal(58.8m, source.CurrentHp);
        Assert.Equal(112, target.CurrentHp);
    }
}
