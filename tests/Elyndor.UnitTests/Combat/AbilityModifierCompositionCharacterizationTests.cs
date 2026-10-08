using System.Reflection;
using System.Text.Json;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class AbilityModifierCompositionCharacterizationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData("WARRIOR")]
    [InlineData("MAGE")]
    [InlineData("ARCHER")]
    [InlineData("PALADIN")]
    public void TwoGenericTalentsAggregateAdditivelyBeforeApplicationAndPreserveNonDamageActions(string classId)
    {
        AbilityDefinition ability = Base("TEST", "PHYSICAL") with
        { TargetType = AbilityTargetType.NEnemiesInCombat, TargetCount = 2 };
        AbilityDefinition result = Both(classId, ability, Generic(ability.Id));
        Assert.Equal(65, result.ResourceCost);
        Assert.Equal(TimeSpan.FromSeconds(8), result.Cooldown);
        Assert.Equal(13, result.Actions![0].Amount);
        Assert.Equal(0.52m, result.Actions[0].AttackPowerCoefficient);
        Assert.Equal(0.52m, result.Actions[0].SpellPowerCoefficient);
        Assert.Equal(0.15m, result.Actions[0].ArmorPenetrationBonus);
        Assert.Equal(0.4m, result.Actions[1].SpellPowerCoefficient);
        Assert.True(result.Actions[1].HealingCanCrit);
        Assert.Equal(7, result.Actions[2].Amount);
        Assert.Equal(TimeSpan.FromSeconds(14), result.Actions[3].Effect!.Duration);
        Assert.Equal(12, result.Actions[3].Effect!.Magnitude);
        Assert.Equal(2, result.TargetCount);
        Assert.Equal(AbilityTargetType.NEnemiesInCombat, result.TargetType);
        Assert.Equal(ability.RuntimeParameters, result.RuntimeParameters);
    }

    [Fact]
    public void BerserkerPercentPrecedesWarlordFlatCostAndCryDurationAddsAfterGenericBonus()
    {
        AbilityDefinition ability = Base("BATTLE_CRY", "PHYSICAL") with { TargetType = AbilityTargetType.Self };
        var talents = Generic(ability.Id) with
        { EventHooks = [Hook("B-5-4", 20), Hook("W-1-1", 7), Hook("W-4-4", 3), Hook("W-9-1", 1)] };
        AbilityDefinition result = Both("WARRIOR", ability, talents,
            f => Buff(f.Player, "BERSERK_ATTACK_POWER"));
        Assert.Equal(45, result.ResourceCost);
        Assert.Equal(TimeSpan.FromSeconds(22), result.Actions![3].Effect!.Duration);
    }

    [Fact]
    public void WarlordBannerCooldownReductionFollowsGenericReduction()
    {
        AbilityDefinition ability = Base("WAR_BANNER", "PHYSICAL") with { TargetType = AbilityTargetType.Self };
        AbilityDefinition result = Both("WARRIOR", ability,
            Generic(ability.Id) with { EventHooks = [Hook("W-5-2", 3)] });
        Assert.Equal(TimeSpan.FromSeconds(5), result.Cooldown);
    }

    [Theory]
    [InlineData(true, 0.624)]
    [InlineData(false, 0.52)]
    public void FireAndArcaneModifiersMultiplyAfterGenericCoefficientsAndUseCurrentResourceCondition(
        bool highResource, decimal coefficient)
    {
        AbilityDefinition ability = Base("MAGE_FIREBALL", "FIRE");
        var talents = Generic(ability.Id) with { EventHooks = [Hook("F-1-4", 10),
            Hook("F-1-3", 0) with { SecondaryValue = 20 }, Hook("F-1-1", 0.5m),
            Hook("F-5-2", 10), Hook("F-9-1", 15), Hook("F-3-4", 4),
            Hook("A-4-3", 10) with { SecondaryValue = 2 }, Hook("A-5-3", 20) with { Threshold = 90 }] };
        AbilityDefinition result = Both("MAGE", ability, talents, f =>
        {
            if (!highResource) Assert.True(f.Player.TrySpendResource(200));
            Buff(f.Player, "MAGE_KINDLING_FLAME", 25); Buff(f.Player, "MAGE_HEAT_DISCOUNT", 25);
        });
        Assert.Equal(35.1m, result.ResourceCost);
        Assert.Equal(1.739375m, result.DamageMultiplier);
        Assert.Equal(6, result.CriticalChanceBonus);
        Assert.Equal(TimeSpan.FromSeconds(2.5), result.CastTime);
        Assert.Equal(coefficient, result.Actions![0].SpellPowerCoefficient);
    }

    [Fact]
    public void PyromancerCastReductionMakesPresenceOfMindApplicableWithoutChangingMageAbilityType()
    {
        AbilityDefinition ability = Base("MAGE_FIREBALL", "FIRE") with { CastTime = TimeSpan.FromSeconds(3.5) };
        var talents = Generic(ability.Id) with
        { EventHooks = [Hook("F-1-1", 0.5m), Hook("A-6-1", 20), Hook("A-8-2", 25)] };
        AbilityDefinition result = Both("MAGE", ability, talents, f => Buff(f.Player, "MAGE_PRESENCE_OF_MIND_ACTIVE"));
        Assert.Equal(TimeSpan.Zero, result.CastTime);
        Assert.Equal(AbilityType.Casted, result.Type);
        Assert.Equal(48.75m, result.ResourceCost);
        Assert.Equal(1.2m, result.DamageMultiplier);
    }

    [Fact]
    public void ClearcastingOverridesPriorCostReductionsAndDoesNotGetUndoneByArcanePower()
    {
        AbilityDefinition ability = Base("MAGE_FIREBALL", "FIRE");
        AbilityDefinition result = Both("MAGE", ability, Generic(ability.Id) with
        { EventHooks = [Hook("F-1-4", 10), Hook("A-5-2", 5), Hook("A-7-3", 7) with { SecondaryValue = 20 }] }, f =>
        { Buff(f.Player, "MAGE_CLEARCASTING"); Buff(f.Player, "MAGE_ARCANE_POWER_ACTIVE"); });
        Assert.Equal(0, result.ResourceCost);
        Assert.Equal(12, result.CriticalChanceBonus);
        Assert.Equal(1.44m, result.DamageMultiplier);
    }

    [Fact]
    public void FrostFollowsArcaneAndTargetModifiersAreCapturedSeparately()
    {
        AbilityDefinition ability = Base("MAGE_ICE_LANCE", "FROST");
        var talents = Generic(ability.Id) with { EventHooks = [Hook("A-4-3", 10),
            Hook("I-1-2", 5) with { SecondaryValue = 10 }, Hook("I-1-3", 20), Hook("I-1-4", 25),
            Hook("I-3-4", 20), Hook("I-9-1", 15), Hook("I-6-2", 3) with { SecondaryValue = 5 },
            Hook("I-3-1", 30), Hook("I-7-2", 20)] };
        AbilityDefinition result = Both("MAGE", ability, talents, f =>
        { Buff(f.Player, "MAGE_COLD_BLOOD", 4, 20); Buff(f.Target, "MAGE_FREEZE", source: f.Player.ActorId); },
            target => { Assert.Equal(3.6m, target.DamageMultiplier); Assert.Equal(35, target.CriticalChanceBonus); });
        Assert.Equal(37.44m, result.ResourceCost);
        Assert.Equal(1.518m, result.DamageMultiplier);
        Assert.Equal(5, result.AccuracyBonus);
        Assert.Equal(4, result.CriticalChanceBonus);
        Assert.Equal(25, result.CriticalDamageBonus);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Cooldown);
    }

    [Theory]
    [InlineData(true, 32.76, 1.518, 2.5, 1.32)]
    [InlineData(false, 32.76, 1.265, 3, 1.2)]
    public void ArcherUsesSelectedMarkAndOrderedOneShotModifiers(bool marked, decimal cost,
        decimal damage, double castSeconds, decimal targetDamage)
    {
        AbilityDefinition ability = Base("AIMED_SHOT", "PHYSICAL");
        var talents = Generic(ability.Id) with { EventHooks = [Hook("M-1-2", 10, "PHYSICAL_FOCUS_COST"),
            Hook("M-1-1", 3, "PHYSICAL_SHOT_CRIT"),
            Hook("M-4-2", 10, "AIMED_ACCURACY_CRIT") with { SecondaryValue = 5 },
            Hook("M-7-1", 20, "PERFECT_AIMED_SHOT") with { CastTimeSeconds = 0.5m },
            Hook("M-5-2", 20, "BOW_PHYSICAL_DAMAGE"), Hook("M-3-2", 20, "PHYSICAL_SHOT_CRIT_DAMAGE")] };
        AbilityDefinition result = Both("ARCHER", ability, talents, f =>
        {
            Buff(f.Player, "ARCHER_COORDINATION", 10, 20);
            Buff(f.Player, "ARCHER_MASTER_TACTICIAN", 15, 30);
            if (marked) Buff(f.Target, "ARCHER_HUNTER_MARK", 10, source: f.Player.ActorId);
        }, target => { Assert.Equal(targetDamage, target.DamageMultiplier); Assert.Equal(20, target.CriticalDamageBonus); });
        Assert.Equal(cost, result.ResourceCost);
        Assert.Equal(damage, result.DamageMultiplier);
        Assert.Equal(8, result.CriticalChanceBonus);
        Assert.Equal(10, result.AccuracyBonus);
        Assert.Equal(TimeSpan.FromSeconds(castSeconds), result.CastTime);
    }

    [Fact]
    public void PaladinDirectHealKeepsHealingCoefficientWhileAddingCritCostAndGrace()
    {
        AbilityDefinition ability = Base("HOLY_LIGHT", "HOLY") with { TargetType = AbilityTargetType.Self };
        AbilityDefinition result = Both("PALADIN", ability, Generic(ability.Id) with
        { EventHooks = [Hook("H-1-3", 0) with { Rank = 3 }, Hook("H-2-2", 0) with { Rank = 2 },
            Hook("H-5-1", 0) with { Rank = 3 }] }, f => Buff(f.Player, "PALADIN_LIGHTS_GRACE", 0.5m));
        Assert.Equal(59.15m, result.ResourceCost);
        Assert.Equal(5, result.CriticalChanceBonus);
        Assert.Equal(TimeSpan.FromSeconds(2.5), result.CastTime);
        Assert.Equal(0.4m, result.Actions![1].SpellPowerCoefficient);
    }

    [Fact]
    public void PaladinHolyShieldDurationOverridesGenericDurationButKeepsMagnitude()
    {
        AbilityDefinition ability = Base("HOLY_SHIELD", "HOLY") with { TargetType = AbilityTargetType.Self };
        ability = ability with { Actions = ability.Actions!.Select(a => a.Effect is null ? a : a with
        { Effect = a.Effect with { Id = "PALADIN_HOLY_SHIELD_BLOCK" } }).ToArray() };
        AbilityDefinition result = Both("PALADIN", ability, Generic(ability.Id) with
        { EventHooks = [Hook("P-3-2", 0) with { Rank = 3 }, Hook("P-9-1", 1)] });
        Assert.Equal(TimeSpan.FromSeconds(13), result.Actions![3].Effect!.Duration);
        Assert.Equal(12, result.Actions[3].Effect!.Magnitude);
    }

    [Fact]
    public void PaladinArtOfWarConvertsFlashOfLightToInstantAfterGenericApplication()
    {
        AbilityDefinition ability = Base("FLASH_OF_LIGHT", "HOLY") with { TargetType = AbilityTargetType.Self };
        AbilityDefinition result = Both("PALADIN", ability, Generic(ability.Id),
            f => Buff(f.Player, "PALADIN_ART_OF_WAR", 2));
        Assert.Equal(AbilityType.Instant, result.Type);
        Assert.Equal(TimeSpan.Zero, result.CastTime);
        // Rank 2 Art of War grants both instant cast and 20% mana reduction
        // after the generic ability modifiers (65 * 0.8 = 52).
        Assert.Equal(52m, result.ResourceCost);
    }

    [Fact]
    public void PaladinArtOfWarRankOneReducesFlashOfLightCastTimeAndManaCost()
    {
        AbilityDefinition ability = Base("FLASH_OF_LIGHT", "HOLY") with { TargetType = AbilityTargetType.Self };
        AbilityDefinition result = Both("PALADIN", ability, Generic(ability.Id),
            f => Buff(f.Player, "PALADIN_ART_OF_WAR", 1));
        Assert.Equal(AbilityType.Casted, result.Type);
        Assert.Equal(TimeSpan.FromSeconds(2.65), result.CastTime);
        Assert.Equal(58.5m, result.ResourceCost);
    }

    [Fact]
    public void PaladinJudgementCombinesGenericCooldownWithRankBonusesAndCapturedTwoHandLoadout()
    {
        AbilityDefinition ability = Base("JUDGEMENT", "PHYSICAL");
        AbilityDefinition result = Both("PALADIN", ability, Generic(ability.Id) with
        { EventHooks = [Hook("R-1-2", 0) with { Rank = 3 }, Hook("R-1-3", 0) with { Rank = 3 },
            Hook("R-2-1", 0) with { Rank = 3 }, Hook("R-5-2", 0) with { Rank = 2 },
            Hook("R-2-3", 0) with { Rank = 2 }] });
        Assert.Equal(61.1m, result.ResourceCost);
        Assert.Equal(TimeSpan.FromSeconds(5.75), result.Cooldown);
        Assert.Equal(9, result.CriticalChanceBonus);
        Assert.Equal(1.06m, result.DamageMultiplier);
    }

    [Fact]
    public void BerserkControlExceptionsAndOpaqueRuntimeParametersSurviveComposition()
    {
        AbilityDefinition berserk = Base("BERSERK", "PHYSICAL") with { TargetType = AbilityTargetType.Self };
        AbilityDefinition result = Both("WARRIOR", berserk,
            Generic(berserk.Id) with { EventHooks = [Hook("B-9-1", 1)] });
        Assert.True(result.CanUseWhileSilenced);
        Assert.True(result.CanUseWhileStunned);

        AbilityDefinition lance = Base("MAGE_ICE_LANCE", "FROST") with
        { RuntimeParameters = new Dictionary<string, decimal> { ["fixtureParameter"] = 3 } };
        result = Both("MAGE", lance, Generic(lance.Id));
        Assert.Same(lance.RuntimeParameters, result.RuntimeParameters);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotCompositionReevaluatesRuntimeEffectsWithoutConsumingOrCachingThem(bool hosted)
    {
        AbilityDefinition ability = Base("MAGE_FIREBALL", "FIRE");
        Fight fight = Create(hosted, "MAGE", ability, Generic(ability.Id));
        Assert.Equal(65, fight.Snapshot().ResourceCost);
        Buff(fight.Player, "MAGE_HEAT_DISCOUNT", 50);
        Assert.Equal(32.5m, fight.Snapshot().ResourceCost);
        Assert.Equal(32.5m, fight.Snapshot().ResourceCost);
        Assert.Contains(fight.Player.ActiveEffects, e => e.Definition.Id == "MAGE_HEAT_DISCOUNT");
        EffectEngine.Remove(fight.Player, "MAGE_HEAT_DISCOUNT", Now);
        Assert.Equal(65, fight.Snapshot().ResourceCost);
        Assert.Equal(1000, fight.Player.CurrentResource);
    }

    [Fact]
    public void CastedTargetModifierCompositionRetainsPveStartAndArenaCompletionCaptureBoundaries()
    {
        AbilityDefinition ability = Base("MAGE_ICE_LANCE", "FROST");
        var talents = Generic(ability.Id) with { EventHooks = [Hook("I-7-2", 20)] };
        Fight pve = Create(false, "MAGE", ability, talents), arena = Create(true, "MAGE", ability, talents);
        foreach (Fight fight in new[] { pve, arena })
        {
            Buff(fight.Target, "MAGE_FREEZE", source: fight.Player.ActorId);
            Assert.Equal(3.6m, fight.TargetModifiers().DamageMultiplier);
            Assert.True(fight.Cast());
        }
        Assert.Equal(3.6m, Assert.Single(pve.Runtime.ActiveCast!.TargetModifiers!).Value.DamageMultiplier);
        Assert.Null(arena.Runtime.ActiveCast!.TargetModifiers);
    }

    [Fact]
    public void SharedSessionComposerReadsEachActivePartyParticipantsBuildInsteadOfCachingFirstOwner()
    {
        AbilityDefinition ability = Base("MAGE_FIREBALL", "FIRE");
        var abilities = new Dictionary<string, AbilityDefinition> { [ability.Id] = ability };
        var auto = new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0);
        CombatParticipantDefinition Participant(string classId) => new(
            new CombatActorState(Guid.NewGuid(), 10000, 10000, 1000, 1000, CombatStats.Default),
            CombatActorKind.Player, classId, classId, classId == "WARRIOR" ? "RAGE" : "MANA", auto,
            abilities.Keys.ToHashSet(), CanAutoAttack: false);
        var mage = Participant("MAGE");
        var warrior = Participant("WARRIOR");
        var enemy = Participant("TEST") with { Kind = CombatActorKind.Monster };
        var mageTalents = Generic(ability.Id) with { EventHooks = [Hook("F-1-4", 10)] };
        var warriorTalents = ResolvedTalentModifiers.Empty with { Abilities = new Dictionary<string, TalentAbilityModifiers>
        { [ability.Id] = new(ResourceCostFlatReduction: 40) } };
        var session = new CombatSession(Guid.NewGuid(), mage, enemy, abilities, new MonsterAiProfile("PASSIVE", []),
            mageTalents, new SeededGameRandom(1), Now,
            additionalPlayers: [new CombatPlayerDefinition(Guid.NewGuid(), warrior, warriorTalents)]);
        Assert.Equal(58.5m, Assert.Single(session.Snapshot(mage.Actor.ActorId).Player.Abilities).ResourceCost);
        Assert.Equal(60, Assert.Single(session.Snapshot(warrior.Actor.ActorId).Player.Abilities).ResourceCost);
        Assert.Equal(58.5m, Assert.Single(session.Snapshot(mage.Actor.ActorId).Player.Abilities).ResourceCost);
        Assert.True(session.Handle(warrior.Actor.ActorId,
            new UseAbilityCommand("warrior", ability.Id, enemy.Actor.ActorId), Now).Succeeded);
        Assert.Equal(940, warrior.Actor.CurrentResource);
        Assert.True(session.Handle(mage.Actor.ActorId,
            new UseAbilityCommand("mage", ability.Id, enemy.Actor.ActorId), Now).Succeeded);
        Assert.Equal(941.5m, mage.Actor.CurrentResource);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EngineFreeCostOverrideDoesNotRewriteComposedDefinitionOrSnapshotCost(bool hosted)
    {
        AbilityDefinition ability = Base("TEST", "PHYSICAL") with { FreeResourceCostWhileEffectId = "TEST_FREE_CAST" };
        Fight fight = Create(hosted, "WARRIOR", ability, Generic(ability.Id));
        Buff(fight.Player, "TEST_FREE_CAST");
        Assert.Equal(65, fight.Snapshot().ResourceCost);
        Assert.True(fight.Cast());
        Assert.Equal(65, fight.Runtime.ActiveCast!.Ability.ResourceCost);
        Assert.Equal(1000, fight.Player.CurrentResource);
    }

    [Fact]
    public void MultiEnemyCompositionDoesNotLeakSelectedTargetsFrozenModifierIntoOtherTargets()
    {
        AbilityDefinition ability = Base("MAGE_ICE_LANCE", "FROST") with { TargetType = AbilityTargetType.AllEnemiesInCombat };
        var abilities = new Dictionary<string, AbilityDefinition> { [ability.Id] = ability };
        var auto = new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0);
        CombatParticipantDefinition Participant(CombatActorKind kind) => new(
            new CombatActorState(Guid.NewGuid(), 10000, 10000, 1000, 1000, CombatStats.Default),
            kind, kind == CombatActorKind.Player ? "MAGE" : "TEST", "Test", "MANA", auto,
            abilities.Keys.ToHashSet(), CanAutoAttack: false);
        var mage = Participant(CombatActorKind.Player);
        var frozen = Participant(CombatActorKind.Monster);
        var plain = Participant(CombatActorKind.Monster);
        Buff(frozen.Actor, "MAGE_FREEZE", source: mage.Actor.ActorId);
        var session = new CombatSession(Guid.NewGuid(), mage, [frozen, plain], abilities,
            new MonsterAiProfile("PASSIVE", []), ResolvedTalentModifiers.Empty,
            new SeededGameRandom(1), Now);
        Assert.True(session.Handle(mage.Actor.ActorId,
            new UseAbilityCommand("aoe", ability.Id, frozen.Actor.ActorId), Now).Succeeded);
        var runtime = (CombatRuntimeState)typeof(CombatSession).GetProperty("_playerRuntime", PrivateInstance)!.GetValue(session)!;
        Assert.Equal(3, runtime.ActiveCast!.TargetModifiers![frozen.Actor.ActorId].DamageMultiplier);
        Assert.False(runtime.ActiveCast.TargetModifiers.ContainsKey(plain.Actor.ActorId));
        Assert.Equal(2, runtime.ActiveCast.TargetIds!.Count);
    }

    [Theory]
    [InlineData("WARRIOR")]
    [InlineData("MAGE")]
    [InlineData("ARCHER")]
    [InlineData("PALADIN")]
    public void GenericCostAndCooldownClampsArePreservedInAllClassPipelines(string classId)
    {
        AbilityDefinition ability = Base("TEST", "PHYSICAL");
        var talents = ResolvedTalentModifiers.Empty with { Abilities = new Dictionary<string, TalentAbilityModifiers>
        { [ability.Id] = new(ResourceCostFlatReduction: 200, CooldownSecondsReduction: 50) } };
        AbilityDefinition result = Both(classId, ability, talents);
        Assert.Equal(0, result.ResourceCost);
        Assert.Equal(TimeSpan.Zero, result.Cooldown);
    }

    private static AbilityDefinition Both(string classId, AbilityDefinition ability, ResolvedTalentModifiers talents,
        Action<Fight>? configure = null, Action<AbilityTargetModifier>? checkTarget = null)
    {
        Fight pve = Create(false, classId, ability, talents), arena = Create(true, classId, ability, talents);
        configure?.Invoke(pve); configure?.Invoke(arena);
        string original = JsonSerializer.Serialize(ability);
        foreach (Fight fight in new[] { pve, arena })
        {
            decimal resource = fight.Player.CurrentResource;
            int effects = fight.Player.ActiveEffects.Count;
            CombatAbilitySnapshot first = fight.Snapshot();
            Assert.Equal(first, fight.Snapshot());
            Assert.Equal(resource, fight.Player.CurrentResource);
            Assert.Equal(effects, fight.Player.ActiveEffects.Count);
            checkTarget?.Invoke(fight.TargetModifiers());
            Assert.True(fight.Cast());
            AbilityDefinition executable = fight.Runtime.ActiveCast?.Ability
                ?? fight.Runtime.PendingActions.First().Ability;
            Assert.Equal(first.ResourceCost, executable.ResourceCost);
            Assert.Equal(first.Cooldown, executable.Cooldown);
            Assert.Equal(first.TargetType, executable.TargetType);
        }
        AbilityDefinition left = pve.Runtime.ActiveCast?.Ability ?? pve.Runtime.PendingActions.First().Ability;
        AbilityDefinition right = arena.Runtime.ActiveCast?.Ability ?? arena.Runtime.PendingActions.First().Ability;
        Assert.Equal(JsonSerializer.Serialize(left), JsonSerializer.Serialize(right));
        Assert.Equal(original, JsonSerializer.Serialize(ability));
        return left;
    }

    private static AbilityDefinition Base(string id, string school) => new(id, AbilityType.Casted,
        AbilityTargetType.SingleEnemy, 100, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3),
        false, GlobalCooldownCategory.None, school != "PHYSICAL", school,
        DisplayName: "Fixture", Description: "Metadata", IconId: "fixture", Actions:
        [new(AbilityActionType.Damage, 10, school == "PHYSICAL" ? DamageType.Physical : DamageType.Magical,
            AttackPowerCoefficient: 0.4m, SpellPowerCoefficient: 0.4m, CanMiss: false, CanDodge: false, CanCrit: false),
         new(AbilityActionType.Healing, 10, SpellPowerCoefficient: 0.4m, HealingCanCrit: true),
         new(AbilityActionType.ResourceChange, 7),
         new(AbilityActionType.ApplyEffect, Effect: new("TEST_EFFECT", EffectKind.Buff,
             TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 10)),
         new(AbilityActionType.ResourceChange, 0, Delay: TimeSpan.FromHours(1))]);

    private static ResolvedTalentModifiers Generic(string abilityId)
    {
        TalentDefinition Node(string id, decimal cost, decimal damage) => new(id, "TEST", 1, 0, id, id, 1, [], "",
            Modifiers: [new(TalentModifierType.AbilityModifier, TalentModifierKeys.AbilityResourceCostPercent, [cost], abilityId),
                new(TalentModifierType.AbilityModifier, TalentModifierKeys.AbilityDamagePercent, [damage], abilityId)]);
        var tree = new TalentTreeDefinition("TEST", "TEST", 10, 1, [new("TEST", "Test", "Test", 2)],
            [Node("FIRST", 10, 10), Node("SECOND", 20, 20)]);
        var result = TalentModifierResolver.Resolve(tree, new Dictionary<string, int> { ["FIRST"] = 1, ["SECOND"] = 1 });
        return result with { Abilities = new Dictionary<string, TalentAbilityModifiers>
        { [abilityId] = result.Abilities[abilityId] with { ResourceCostFlatReduction = 5, CooldownSecondsReduction = 2,
            EffectDurationSecondsBonus = 4, EffectMagnitudePercentBonus = 20, ArmorPenetrationPercent = 15 } } };
    }

    private static ResolvedTalentEventHook Hook(string id, decimal value, string? target = null) =>
        new(id, "CLASS_FIXTURE", 1, value, target, TimeSpan.Zero, false);

    private static void Buff(CombatActorState actor, string id, decimal magnitude = 1,
        decimal? remaining = null, Guid? source = null)
    {
        EffectEngine.Apply(actor, source ?? actor.ActorId, new EffectDefinition(id, EffectKind.Buff,
            TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, magnitude), Now);
        if (remaining is not null)
            typeof(ActiveEffect).GetProperty(nameof(ActiveEffect.RemainingMagnitude))!
                .SetValue(actor.ActiveEffects.Last(), remaining.Value);
    }

    private sealed record Fight(CombatActorState Player, CombatActorState Target,
        CombatRuntimeState Runtime, Func<bool> Cast, Func<CombatAbilitySnapshot> Snapshot,
        Func<AbilityTargetModifier> TargetModifiers);

    private static Fight Create(bool hosted, string classId, AbilityDefinition ability, ResolvedTalentModifiers talents)
    {
        var actor = new CombatActorState(Guid.NewGuid(), 10000, 10000, 1000, 1000, CombatStats.Default);
        var target = new CombatActorState(Guid.NewGuid(), 10000, 10000, 1000, 1000, CombatStats.Default);
        var auto = new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0);
        var abilities = new Dictionary<string, AbilityDefinition> { [ability.Id] = ability };
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player, classId, "Player",
            classId == "WARRIOR" ? "RAGE" : classId == "ARCHER" ? "FOCUS" : "MANA", auto,
            abilities.Keys.ToHashSet(), CanAutoAttack: false,
            MainHandWeaponCategory: classId == "PALADIN" ? "TWO_HAND_SWORD" : null);
        var opponent = participant with { Actor = target, Kind = hosted ? CombatActorKind.Player : CombatActorKind.Monster,
            DefinitionId = "MAGE", Name = "Opponent" };
        Guid targetId = ability.TargetType == AbilityTargetType.Self ? actor.ActorId : target.ActorId;
        if (!hosted)
        {
            var session = new CombatSession(Guid.NewGuid(), participant, opponent, abilities,
                new MonsterAiProfile("PASSIVE", []), talents, new SeededGameRandom(1), Now);
            var runtime = (CombatRuntimeState)typeof(CombatSession).GetProperty("_playerRuntime", PrivateInstance)!.GetValue(session)!;
            return new(actor, target, runtime,
                () => session.Handle(actor.ActorId, new UseAbilityCommand("cast", ability.Id, targetId), Now).Succeeded,
                () => Assert.Single(session.Snapshot().Player.Abilities),
                () => TargetModifiersFor(session, ability, target.ActorId));
        }
        var player = new CombatPlayerDefinition(Guid.NewGuid(), participant, talents);
        var other = new CombatPlayerDefinition(Guid.NewGuid(), opponent, ResolvedTalentModifiers.Empty);
        ArenaFighter Fighter(CombatPlayerDefinition p) => new(p.AccountId, p.Participant.Actor.ActorId,
            p.Participant.Actor, abilities, auto, TalentModifiers: p.TalentModifiers, CanAutoAttack: false,
            PlayerDefinition: p, BaseAbilities: abilities);
        var arena = new ArenaCombatSession(Guid.NewGuid(), Fighter(player), Fighter(other), new SeededGameRandom(1), Now);
        var arenaRuntime = (CombatRuntimeState)typeof(ArenaCombatSession).GetMethod("RuntimeFor", PrivateInstance)!
            .Invoke(arena, [player.AccountId])!;
        var host = (CombatSession)typeof(ArenaCombatSession).GetMethod("MechanicsFor", PrivateInstance)!
            .Invoke(arena, [actor.ActorId])!;
        return new(actor, target, arenaRuntime,
            () => arena.UseAbility(player.AccountId, "cast", ability.Id, targetId, Now).Succeeded,
            () => Assert.Single(arena.AbilitySnapshotsFor(player.AccountId)),
            () => TargetModifiersFor(host, ability, target.ActorId));
    }

    private static AbilityTargetModifier TargetModifiersFor(CombatSession session, AbilityDefinition ability, Guid targetId)
    {
        var resolved = (AbilityDefinition)typeof(CombatSession).GetMethod("ResolveMechanicsAbilityForSnapshot", PrivateInstance)!
            .Invoke(session, [ability, Now])!;
        var modifiers = (IReadOnlyDictionary<Guid, AbilityTargetModifier>)typeof(CombatSession)
            .GetMethod("ResolveMechanicsTargetModifiers", PrivateInstance)!.Invoke(session, [resolved, new[] { targetId }, Now])!;
        return modifiers.GetValueOrDefault(targetId) ?? new();
    }
}
