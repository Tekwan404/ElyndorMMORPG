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
        Assert.Equal(106, result.AfterMitigation);
        Assert.Equal(25, result.AbsorbedByShields);
        Assert.Equal(81, result.HpDamage);
        Assert.Equal(119, target.CurrentHp);
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
    public void BaselineAccuracyLeavesFivePercentMissChance()
    {
        CombatActorState source = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with
            {
                Level = 20,
                Accuracy = 95
            });
        CombatActorState target = CombatActorState.CreateDummy(
            200,
            stats: CombatStats.Default with
            {
                Level = 20,
                Dodge = 0
            });

        DamageRequest request = new(
            source,
            target,
            50,
            DamageType.Physical,
            CanDodge: false,
            CanCrit: false);

        DamageResult miss = DamagePipeline.Resolve(
            request,
            new SequenceGameRandom(0.049m));
        DamageResult hit = DamagePipeline.Resolve(
            request,
            new SequenceGameRandom(0.05m));

        Assert.Equal(DamageAvoidance.Miss, miss.Avoidance);
        Assert.Equal(DamageAvoidance.None, hit.Avoidance);
    }

    [Fact]
    public void LevelGapAddsMissChanceOnTopOfBaselineAccuracy()
    {
        CombatActorState source = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with
            {
                Level = 20,
                Accuracy = 95
            });
        CombatActorState target = CombatActorState.CreateDummy(
            200,
            stats: CombatStats.Default with
            {
                Level = 25,
                Dodge = 0
            });

        DamageRequest request = new(
            source,
            target,
            50,
            DamageType.Physical,
            CanDodge: false,
            CanCrit: false);

        DamageResult miss = DamagePipeline.Resolve(
            request,
            new SequenceGameRandom(0.099m));
        DamageResult hit = DamagePipeline.Resolve(
            request,
            new SequenceGameRandom(0.10m));

        Assert.Equal(DamageAvoidance.Miss, miss.Avoidance);
        Assert.Equal(DamageAvoidance.None, hit.Avoidance);
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
    public void BlockValueCanStillFullyAbsorbPhysicalHit()
    {
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(
            200,
            stats: CombatStats.Default with
            {
                BlockChance = 100,
                BlockValueMin = 500,
                BlockValueMax = 500
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
            new SequenceGameRandom(0m));

        Assert.True(result.WasBlocked);
        Assert.Equal(100, result.BlockedAmount);
        Assert.Equal(0, result.HpDamage);
        Assert.Equal(200, target.CurrentHp);
    }

    [Fact]
    public void EffectiveBlockChanceIsCappedAtSixtyPercent()
    {
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with
            {
                BlockChance = 100,
                BlockValueMin = 50,
                BlockValueMax = 50
            });

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                source,
                target,
                50,
                DamageType.Physical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false),
            new SequenceGameRandom(0.61m));

        Assert.False(result.WasBlocked);
        Assert.Equal(50, result.HpDamage);
    }

    [Fact]
    public void BlockEffectsModifyShieldProfileBeforeTheServerSideCap()
    {
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with
            {
                BlockChance = 45,
                BlockValueMin = 10,
                BlockValueMax = 10
            });
        EffectEngine.Apply(target, target.ActorId, new EffectDefinition(
            "TEST_BLOCK_CHANCE", EffectKind.StatModifier, TimeSpan.FromSeconds(10), 1,
            EffectStackPolicy.Replace, 30, ModifiedStat: EffectStat.BlockChance), DateTimeOffset.UnixEpoch);
        EffectEngine.Apply(target, target.ActorId, new EffectDefinition(
            "TEST_BLOCK_VALUE", EffectKind.StatModifier, TimeSpan.FromSeconds(10), 1,
            EffectStackPolicy.Replace, 15, ModifiedStat: EffectStat.BlockValueMin), DateTimeOffset.UnixEpoch);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 50, DamageType.Physical,
                CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom(0.59m),
            DateTimeOffset.UnixEpoch);

        Assert.True(result.WasBlocked);
        Assert.Equal(25, result.BlockedAmount);
        Assert.Equal(25, result.HpDamage);
    }

    [Fact]
    public void UnblockablePhysicalDamageBypassesShieldBlockAndPublishesItsOwnEvent()
    {
        CombatActorState source = CombatActorState.CreateDummy(100);
        CombatActorState target = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with
            {
                BlockChance = 60,
                BlockValueMin = 100,
                BlockValueMax = 100
            });

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 50, DamageType.Physical,
                CanMiss: false, CanDodge: false, CanCrit: false, IsUnblockable: true),
            new SequenceGameRandom(0m),
            DateTimeOffset.UnixEpoch);

        Assert.False(result.WasBlocked);
        Assert.Equal(50, result.HpDamage);
        Assert.Contains(result.Events, item => item.Type == CombatEventType.UnblockableHit
            && item.IsUnblockable);
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
        Assert.Equal(47, blockEvent.DamageAfterMitigation);
        Assert.Equal(47, blockEvent.DamageBeforeBlock);
        Assert.Equal(20, blockEvent.Amount);
        Assert.Equal(27, blockEvent.AmountBeforeShields);
        Assert.Equal(27, damageEvent.Amount);
    }

    [Fact]
    public void HigherLevelAttackerMakesOldArmorLessEffective()
    {
        CombatActorState level18 = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with { Level = 18, Accuracy = 100 });
        CombatActorState level60 = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with { Level = 60, Accuracy = 100 });
        CombatActorState target18 = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with { Armor = 745 });
        CombatActorState target60 = CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with { Armor = 745 });

        DamageResult sameTier = DamagePipeline.Resolve(
            new DamageRequest(
                level18,
                target18,
                100,
                DamageType.Physical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false),
            new SequenceGameRandom());
        DamageResult oldGear = DamagePipeline.Resolve(
            new DamageRequest(
                level60,
                target60,
                100,
                DamageType.Physical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false),
            new SequenceGameRandom());

        Assert.Equal(51, sameTier.HpDamage);
        Assert.Equal(77, oldGear.HpDamage);
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
        Assert.Equal(59m, source.CurrentHp);
        Assert.Equal(112, target.CurrentHp);
        CombatEvent healing = Assert.Single(result.Events, item =>
            item.Type == CombatEventType.HealingApplied);
        Assert.Equal(9m, healing.Amount);
        Assert.Equal(HealingOrigin.Lifesteal, healing.HealingOrigin);
    }
    [Fact]
    public void PassiveLifestealReportsOnlyEffectiveHealingAtHpCap()
    {
        CombatActorState source = CombatActorState.CreateDummy(
            1_000, talentModifiers: new TalentCombatModifiers(VampirismPercent: 50));
        source.SetCurrentHp(990);
        CombatActorState target = CombatActorState.CreateDummy(200);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 100, DamageType.Physical,
                CanMiss: false, CanDodge: false, CanCrit: false, DefinitionId: "HIT"),
            new SequenceGameRandom());

        Assert.Equal(100, result.HpDamage);
        Assert.Equal(1_000, source.CurrentHp);
        CombatEvent healing = Assert.Single(result.Events, item =>
            item.Type == CombatEventType.HealingApplied);
        Assert.Equal(10, healing.Amount);
        Assert.Equal(HealingOrigin.Lifesteal, healing.HealingOrigin);
        Assert.Equal("HIT", healing.DefinitionId);
        Assert.Equal(source.ActorId, healing.SourceActorId);
        Assert.Equal(source.ActorId, healing.TargetActorId);
    }

    [Fact]
    public void PassiveLifestealObeysHealingDoneAndHealingReceivedModifiers()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CombatActorState source = CombatActorState.CreateDummy(
            1_000, talentModifiers: new TalentCombatModifiers(VampirismPercent: 20));
        source.SetCurrentHp(500);
        CombatActorState target = CombatActorState.CreateDummy(200);
        EffectEngine.Apply(source, source.ActorId,
            new EffectDefinition("HEALING_DONE", EffectKind.StatModifier,
                TimeSpan.FromSeconds(20), 1, EffectStackPolicy.Replace, 1.5m,
                ModifiedStat: EffectStat.OutgoingHealingMultiplier,
                ModifierMode: EffectModifierMode.Multiplicative), now);
        EffectEngine.Apply(source, target.ActorId,
            new EffectDefinition("MORTAL_WOUND", EffectKind.StatModifier,
                TimeSpan.FromSeconds(20), 1, EffectStackPolicy.Replace, 0.5m,
                ModifiedStat: EffectStat.HealingReceivedMultiplier,
                ModifierMode: EffectModifierMode.Multiplicative), now);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 100, DamageType.Physical,
                CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom(), now);

        Assert.Equal(100, result.HpDamage);
        Assert.Equal(515m, source.CurrentHp);
        Assert.Equal(15m, Assert.Single(result.Events, item =>
            item.Type == CombatEventType.HealingApplied).Amount);
    }

    [Fact]
    public void PassiveLifestealDoesNotEmitHealingAtFullHp()
    {
        CombatActorState source = CombatActorState.CreateDummy(
            1_000, talentModifiers: new TalentCombatModifiers(VampirismPercent: 50));
        CombatActorState target = CombatActorState.CreateDummy(200);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 100, DamageType.Physical,
                CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom());

        Assert.Equal(1_000, source.CurrentHp);
        Assert.DoesNotContain(result.Events, item => item.Type == CombatEventType.HealingApplied);
    }

    [Theory]
    [InlineData(DamageType.Magical)]
    [InlineData(DamageType.True)]
    public void PassivePhysicalLifestealDoesNotApplyToOtherDamageTypes(DamageType type)
    {
        CombatActorState source = CombatActorState.CreateDummy(
            1_000, talentModifiers: new TalentCombatModifiers(VampirismPercent: 50));
        source.SetCurrentHp(500);
        CombatActorState target = CombatActorState.CreateDummy(200);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 100, type,
                CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom());

        Assert.Equal(100, result.HpDamage);
        Assert.Equal(500, source.CurrentHp);
        Assert.DoesNotContain(result.Events, item => item.Type == CombatEventType.HealingApplied);
    }

    [Fact]
    public void PassiveLifestealDoesNotHealOrPublishHealingFromSelfDamage()
    {
        CombatActorState actor = CombatActorState.CreateDummy(
            1_000, talentModifiers: new TalentCombatModifiers(VampirismPercent: 50));
        actor.SetCurrentHp(500);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(actor, actor, 100, DamageType.Physical,
                CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom());

        Assert.Equal(400, actor.CurrentHp);
        Assert.DoesNotContain(result.Events, item => item.Type == CombatEventType.HealingApplied);
    }

    [Fact]
    public void PassiveLifestealDoesNotHealFullyShieldedDamage()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CombatActorState source = CombatActorState.CreateDummy(
            1_000, talentModifiers: new TalentCombatModifiers(VampirismPercent: 50));
        source.SetCurrentHp(500);
        CombatActorState target = CombatActorState.CreateDummy(200);
        EffectEngine.Apply(target, target.ActorId,
            new EffectDefinition("SHIELD", EffectKind.Shield,
                TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 150), now);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 100, DamageType.Physical,
                CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom(), now);

        Assert.Equal(0, result.HpDamage);
        Assert.Equal(500, source.CurrentHp);
        Assert.DoesNotContain(result.Events, item => item.Type == CombatEventType.HealingApplied);
    }

    [Theory]
    [InlineData(DamageType.Physical, 18)]
    [InlineData(DamageType.Magical, 10)]
    [InlineData(DamageType.True, 2)]
    public void EveryClassCanLeechItsDamageSchoolAndBerserkerBonusStacks(
        DamageType type, decimal expectedHealing)
    {
        CombatActorState source = CombatActorState.CreateDummy(
            1_000,
            stats: CombatStats.Default with
            {
                PhysicalVampirismPercent = 5,
                MagicalVampirismPercent = 8,
                UniversalVampirismPercent = 2
            },
            talentModifiers: new TalentCombatModifiers(VampirismPercent: 11));
        source.SetCurrentHp(500);
        CombatActorState target = CombatActorState.CreateDummy(500);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 100, type,
                CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom());

        Assert.Equal(100, result.HpDamage);
        Assert.Equal(500 + expectedHealing, source.CurrentHp);
        Assert.Equal(expectedHealing, Assert.Single(result.Events, item =>
            item.Type == CombatEventType.HealingApplied).Amount);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void UniversalVampirismExcludesReflectedAndTransferredDamage(
        bool reflected, bool transferred)
    {
        CombatActorState source = CombatActorState.CreateDummy(
            1_000, stats: CombatStats.Default with { UniversalVampirismPercent = 30 });
        source.SetCurrentHp(500);
        CombatActorState target = CombatActorState.CreateDummy(500);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 100, DamageType.True,
                CanMiss: false, CanDodge: false, CanCrit: false,
                IsReflectedDamage: reflected, IsTransferredDamage: transferred),
            new SequenceGameRandom());

        Assert.Equal(500, source.CurrentHp);
        Assert.DoesNotContain(result.Events, item =>
            item.Type == CombatEventType.HealingApplied);
    }

    [Fact]
    public void MixedMagicalLifestealRespectsOverkillAndHealingReduction()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CombatActorState source = CombatActorState.CreateDummy(
            1_000, stats: CombatStats.Default with
            {
                MagicalVampirismPercent = 10,
                UniversalVampirismPercent = 10
            });
        source.SetCurrentHp(500);
        CombatActorState target = CombatActorState.CreateDummy(500);
        target.SetCurrentHp(40);
        EffectEngine.Apply(source, source.ActorId,
            new EffectDefinition("HEAL_REDUCTION", EffectKind.StatModifier,
                TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 0.5m,
                ModifiedStat: EffectStat.HealingReceivedMultiplier,
                ModifierMode: EffectModifierMode.Multiplicative), now);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 100, DamageType.Magical,
                CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom(), now);

        Assert.Equal(40, result.HpDamage);
        Assert.Equal(504, source.CurrentHp);
        Assert.Equal(4, Assert.Single(result.Events, item =>
            item.Type == CombatEventType.HealingApplied).Amount);
    }

    [Fact]
    public void DeadSourceCannotResurrectThroughVampirism()
    {
        CombatActorState source = CombatActorState.CreateDummy(
            1_000, stats: CombatStats.Default with { UniversalVampirismPercent = 50 });
        source.SetCurrentHp(0);
        CombatActorState target = CombatActorState.CreateDummy(500);

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(source, target, 100, DamageType.Magical,
                CanMiss: false, CanDodge: false, CanCrit: false),
            new SequenceGameRandom());

        Assert.Equal(0, source.CurrentHp);
        Assert.DoesNotContain(result.Events, item =>
            item.Type == CombatEventType.HealingApplied);
    }

}
