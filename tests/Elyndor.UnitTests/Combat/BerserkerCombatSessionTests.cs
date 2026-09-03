using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class BerserkerCombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SessionId =
        Guid.Parse("11000000-0000-0000-0000-000000000001");
    private static readonly Guid PlayerId =
        Guid.Parse("22000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("33000000-0000-0000-0000-000000000001");

    [Fact]
    public void LowHealthBerserkerTalentsBecomeLiveConditionalEffects()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook("B-2-1", TalentModifierKeys.OnHpThreshold, 2, 12),
            Hook("B-4-4", TalentModifierKeys.OnHpThreshold, 2, 4),
            Hook("B-7-2", TalentModifierKeys.OnHpThreshold, 4, 12),
            Hook("B-7-4", TalentModifierKeys.OnHpThreshold, 2, 10));
        TestFight fight = CreateFight(
            talents,
            playerHp: 20,
            playerMaxHp: 100,
            enemyHp: 1_000,
            enemyMaxHp: 10_000);

        fight.Session.AdvanceTo(Now);

        CombatSessionSnapshot snapshot = fight.Session.Snapshot();
        Assert.Contains(snapshot.Player.Effects, effect =>
            effect.Id == "BERSERKER_BLOOD_RAGE_ATTACK_POWER");
        Assert.Contains(snapshot.Player.Effects, effect =>
            effect.Id == "BERSERKER_RECKLESSNESS_OUTGOING");
        Assert.Contains(snapshot.Player.Effects, effect =>
            effect.Id == "BERSERKER_DEATH_STRENGTH_CRITICAL");
        Assert.Contains(snapshot.Player.Effects, effect =>
            effect.Id == "BERSERKER_EXECUTIONER");
    }

    [Fact]
    public void BerserkEnablesFrenzyAndAvatarCleansesControl()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook("B-2-4", TalentModifierKeys.OnAbilityUsed, 3, 6),
            Hook("B-5-4", TalentModifierKeys.OnAbilityUsed, 2, 20, "BERSERK"),
            Hook("B-9-1", TalentModifierKeys.OnCriticalHit, 1, 10));
        TestFight fight = CreateFight(
            talents,
            enemyHp: 10_000,
            playerResource: 100);
        fight.Session.AdvanceTo(Now);

        EffectEngine.Apply(
            fight.Player,
            EnemyId,
            new EffectDefinition(
                "TEST_STUN",
                EffectKind.Stun,
                TimeSpan.FromSeconds(30),
                1,
                EffectStackPolicy.Replace,
                0),
            Now);
        EffectEngine.Apply(
            fight.Player,
            EnemyId,
            new EffectDefinition(
                "TEST_SILENCE",
                EffectKind.Silence,
                TimeSpan.FromSeconds(30),
                1,
                EffectStackPolicy.Replace,
                0),
            Now);

        CombatCommandResult result = fight.Session.Handle(
            new UseAbilityCommand(
                "berserk",
                "BERSERK",
                PlayerId),
            Now.AddMilliseconds(1));

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(result.Snapshot.Player.Effects, effect =>
            effect.Id is "TEST_STUN" or "TEST_SILENCE");
        Assert.Contains(result.Snapshot.Player.Effects, effect =>
            effect.Id == "BERSERKER_MOMENTUM_ATTACK_SPEED");
        CombatAbilitySnapshot wildStrike =
            Assert.Single(result.Snapshot.Player.Abilities, ability =>
                ability.Id == "WILD_STRIKE");
        Assert.Equal(20, wildStrike.ResourceCost);
    }

    [Fact]
    public void DoubleStrikeUsesDeterministicProcAndDoesNotCritSecondaryHit()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                "B-4-1",
                TalentModifierKeys.OnAutoAttack,
                1,
                45,
                internalCooldown: TimeSpan.FromSeconds(2)));
        TestFight fight = CreateFight(
            talents,
            enemyHp: 10_000,
            randomValues: [0.99m, 0.99m, 0m]);

        fight.Session.AdvanceTo(Now);

        IReadOnlyList<CombatEvent> events = fight.Session.GetEventsAfter(0);
        Assert.Contains(events, item =>
            item.Type == CombatEventType.DamageDealt
            && item.DefinitionId == "AUTO_ATTACK");
        Assert.Contains(events, item =>
            item.Type == CombatEventType.DamageDealt
            && item.DefinitionId == "B-4-1");
        Assert.DoesNotContain(events, item =>
            item.Type == CombatEventType.CriticalHit
            && item.DefinitionId == "B-4-1");
    }


    [Fact]
    public void UnstoppableForceOverridesDoubleStrikeDuringBerserk()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                "B-4-1",
                TalentModifierKeys.OnAutoAttack,
                1,
                45,
                internalCooldown: TimeSpan.FromSeconds(2)),
            Hook("B-7-1", TalentModifierKeys.OnAutoAttack, 1, 30, "BERSERK"));
        TestFight fight = CreateFight(
            talents,
            enemyHp: 10_000,
            playerResource: 100);
        fight.Session.AdvanceTo(Now);
        Assert.True(fight.Session.Handle(
            new UseAbilityCommand(
                "berserk-unstoppable",
                "BERSERK",
                PlayerId),
            Now.AddMilliseconds(1)).Succeeded);

        CombatCommandResult result =
            fight.Session.AdvanceTo(Now.AddSeconds(2));

        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.DamageDealt
            && item.DefinitionId == "B-7-1");
        Assert.DoesNotContain(result.Events, item =>
            item.Type == CombatEventType.DamageDealt
            && item.DefinitionId == "B-4-1");
    }

    [Fact]
    public void WhirlwindAddsRendingBleedAndDeathWhirlwindTrueComponent()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook("B-7-3", TalentModifierKeys.OnAbilityUsed, 2, 8, "WHIRLWIND"),
            Hook("B-8-1", TalentModifierKeys.OnAbilityUsed, 1, 15, "WHIRLWIND"));
        TestFight fight = CreateFight(
            talents,
            enemyHp: 10_000,
            playerResource: 100);
        fight.Session.AdvanceTo(Now);

        CombatCommandResult result = fight.Session.Handle(
            new UseAbilityCommand(
                "whirlwind",
                "WHIRLWIND",
                EnemyId),
            Now.AddMilliseconds(1));

        Assert.True(result.Succeeded);
        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.DamageDealt
            && item.DefinitionId == "B-8-1");
        Assert.Contains(result.Snapshot.Enemy.Effects, effect =>
            effect.Id == "BERSERKER_RENDING_RAMPAGE");
    }


    [Fact]
    public void CriticalWildStrikeAppliesBloodTrailAndCriticalAutoAppliesVulnerability()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook("B-3-4", TalentModifierKeys.OnCriticalHit, 2, 7, "WILD_STRIKE"),
            Hook("B-6-2", TalentModifierKeys.OnCriticalHit, 1, 5, "AUTO_ATTACK"));
        TestFight fight = CreateFight(
            talents,
            enemyHp: 10_000,
            playerResource: 100,
            playerCriticalChance: 100);
        fight.Session.AdvanceTo(Now);

        Assert.Contains(fight.Session.Snapshot().Enemy.Effects, effect =>
            effect.Id == "BERSERKER_DEVASTATING_VULNERABILITY");

        CombatCommandResult result = fight.Session.Handle(
            new UseAbilityCommand(
                "wild-critical",
                "WILD_STRIKE",
                EnemyId),
            Now.AddMilliseconds(1));

        Assert.True(result.Succeeded);
        Assert.Contains(result.Snapshot.Enemy.Effects, effect =>
            effect.Id == "BERSERKER_BLOOD_TRAIL");
    }


    [Fact]
    public void BattleTranceAddsRageWhenDamageIsTakenDuringBerserk()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                "B-6-3",
                TalentModifierKeys.OnDamageTaken,
                2,
                5,
                "BERSERK"));
        TestFight fight = CreateFight(
            talents,
            enemyHp: 10_000,
            playerResource: 100,
            enemyAutoDamage: 20,
            enemyAutoAttackInterval: TimeSpan.FromSeconds(2));
        fight.Session.AdvanceTo(Now);
        Assert.True(fight.Session.Handle(
            new UseAbilityCommand(
                "berserk-trance",
                "BERSERK",
                PlayerId),
            Now.AddMilliseconds(1)).Succeeded);

        CombatCommandResult result =
            fight.Session.AdvanceTo(Now.AddSeconds(2));

        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.ResourceChanged
            && item.DefinitionId == "B-6-3"
            && item.Amount == 5);
    }

    [Fact]
    public void BloodMomentumReducesRunningBerserkCooldownAfterBerserkExpires()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                "B-6-4",
                TalentModifierKeys.OnCriticalHit,
                3,
                3,
                "BERSERK",
                TimeSpan.FromSeconds(3)));
        TestFight fight = CreateFight(
            talents,
            enemyHp: 10_000,
            playerResource: 100,
            playerCriticalChance: 100);
        fight.Session.AdvanceTo(Now);

        CombatCommandResult berserk = fight.Session.Handle(
            new UseAbilityCommand(
                "berserk-cooldown",
                "BERSERK",
                PlayerId),
            Now.AddMilliseconds(1));
        DateTimeOffset originalReady =
            berserk.Snapshot.Player.Cooldowns["BERSERK"];

        CombatCommandResult afterExpiry =
            fight.Session.AdvanceTo(Now.AddSeconds(8.5));
        DateTimeOffset reducedReady =
            afterExpiry.Snapshot.Player.Cooldowns["BERSERK"];

        Assert.Equal(originalReady - TimeSpan.FromSeconds(3), reducedReady);
    }

    [Fact]
    public void BerserkerAgonyResetsWildStrikeCooldownOnKillDuringBerserk()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                "B-8-2",
                TalentModifierKeys.OnEnemyKilled,
                2,
                0,
                "WILD_STRIKE"));
        TestFight fight = CreateFight(
            talents,
            enemyHp: 1_000,
            playerResource: 100);
        fight.Session.AdvanceTo(Now);
        Assert.True(fight.Session.Handle(
            new UseAbilityCommand(
                "berserk-reset",
                "BERSERK",
                PlayerId),
            Now.AddMilliseconds(1)).Succeeded);
        CombatCommandResult wild = fight.Session.Handle(
            new UseAbilityCommand(
                "wild-before-kill",
                "WILD_STRIKE",
                EnemyId),
            Now.AddMilliseconds(2));
        Assert.True(wild.Succeeded);
        Assert.Contains("WILD_STRIKE", wild.Snapshot.Player.Cooldowns.Keys);

        EffectEngine.Apply(
            fight.Enemy,
            PlayerId,
            new EffectDefinition(
                "TEST_KILL_BLEED",
                EffectKind.DamageOverTime,
                TimeSpan.FromSeconds(2),
                1,
                EffectStackPolicy.Replace,
                2_000,
                TimeSpan.FromSeconds(1)),
            Now.AddMilliseconds(2));

        CombatCommandResult killed =
            fight.Session.AdvanceTo(Now.AddSeconds(1.1));

        Assert.Equal(CombatSessionStatus.Victory, killed.Snapshot.Status);
        Assert.DoesNotContain("WILD_STRIKE", killed.Snapshot.Player.Cooldowns.Keys);
    }

    [Fact]
    public void DeathsEmbraceForcesExactlyOneCriticalAutoAttack()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook("B-8-3", TalentModifierKeys.OnHpThreshold, 1, 200));
        TestFight fight = CreateFight(
            talents,
            playerHp: 5,
            playerMaxHp: 100,
            enemyHp: 10_000,
            playerCriticalChance: 0,
            randomValues: [0.99m, 0.99m, 0.99m]);

        fight.Session.AdvanceTo(Now);
        fight.Session.AdvanceTo(Now.AddSeconds(2));

        CombatEvent[] criticals = fight.Session.GetEventsAfter(0)
            .Where(item =>
                item.Type == CombatEventType.CriticalHit
                && item.DefinitionId == "AUTO_ATTACK")
            .ToArray();
        Assert.Single(criticals);
    }

    [Fact]
    public void PeriodicBerserkerDamageCanKillAndFinishCombat()
    {
        ResolvedTalentModifiers talents = Talents(
            new ResolvedTalentEventHook(
                "B-1-2",
                TalentModifierKeys.OnEnemyKilled,
                1,
                8,
                null,
                TimeSpan.Zero,
                false));
        TestFight fight = CreateFight(
            talents,
            enemyHp: 20,
            playerResource: 0,
            randomValues: [0.99m, 0.99m]);
        EffectEngine.Apply(
            fight.Enemy,
            PlayerId,
            new EffectDefinition(
                "TEST_BLEED",
                EffectKind.DamageOverTime,
                TimeSpan.FromSeconds(2),
                1,
                EffectStackPolicy.Replace,
                25,
                TimeSpan.FromSeconds(1)),
            Now);

        CombatCommandResult result = fight.Session.AdvanceTo(Now.AddSeconds(1));

        Assert.Equal(CombatSessionStatus.Victory, result.Snapshot.Status);
        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.EnemyKilled
            && item.IsPeriodic);
        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.ResourceChanged
            && item.DefinitionId == "B-1-2");
    }

    private static ResolvedTalentEventHook Hook(
        string talentId,
        string key,
        int rank,
        decimal value,
        string? targetId = null,
        TimeSpan? internalCooldown = null) =>
        new(
            talentId,
            key,
            rank,
            value,
            targetId,
            internalCooldown ?? TimeSpan.Zero,
            false);

    private static ResolvedTalentModifiers Talents(
        params ResolvedTalentEventHook[] hooks) =>
        ResolvedTalentModifiers.Empty with { EventHooks = hooks };

    private static TestFight CreateFight(
        ResolvedTalentModifiers talents,
        decimal playerHp = 200,
        decimal playerMaxHp = 200,
        decimal enemyHp = 500,
        decimal? enemyMaxHp = null,
        decimal playerResource = 100,
        decimal playerCriticalChance = 0,
        decimal enemyAutoDamage = 0,
        TimeSpan? enemyAutoAttackInterval = null,
        decimal[]? randomValues = null)
    {
        CombatStats playerStats = new(
            Level: 20,
            Accuracy: 100,
            Dodge: 0,
            CriticalChance: playerCriticalChance,
            CriticalDamage: 1,
            Armor: 10,
            MagicResistance: 5,
            ArmorPenetration: 0,
            MagicPenetration: 0,
            AttackPower: 30,
            SpellPower: 0);
        CombatStats enemyStats = new(
            Level: 20,
            Accuracy: 100,
            Dodge: 0,
            CriticalChance: 0,
            CriticalDamage: 1,
            Armor: 5,
            MagicResistance: 5,
            ArmorPenetration: 0,
            MagicPenetration: 0,
            AttackPower: 8,
            SpellPower: 0);
        CombatActorState playerActor = new(
            PlayerId,
            playerMaxHp,
            playerHp,
            100,
            playerResource,
            playerStats);
        CombatActorState enemyActor = new(
            EnemyId,
            enemyMaxHp ?? enemyHp,
            enemyHp,
            0,
            0,
            enemyStats);
        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            "WARRIOR",
            "Warrior",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromSeconds(2), 0, 0.65m, 10),
            new HashSet<string>(
                ["STRIKE", "WILD_STRIKE", "WHIRLWIND", "BERSERK"],
                StringComparer.Ordinal));
        CombatParticipantDefinition enemy = new(
            enemyActor,
            CombatActorKind.Monster,
            "WOLF",
            "Forest Wolf",
            "NONE",
            new AutoAttackProfile(
                enemyAutoAttackInterval ?? TimeSpan.FromSeconds(10),
                enemyAutoDamage,
                0,
                0),
            new HashSet<string>(StringComparer.Ordinal));
        Dictionary<string, AbilityDefinition> abilities = CreateAbilities();
        MonsterAiProfile ai = new("PASSIVE_TEST_AI", []);
        decimal[] rng = randomValues
            ?? Enumerable.Repeat(0.99m, 200).ToArray();

        CombatSession session = new(
            SessionId,
            player,
            enemy,
            abilities,
            ai,
            talents,
            new SequenceGameRandom(rng),
            Now);
        return new TestFight(session, playerActor, enemyActor);
    }

    private static Dictionary<string, AbilityDefinition> CreateAbilities() =>
        new(StringComparer.Ordinal)
        {
            ["STRIKE"] = new(
                "STRIKE",
                AbilityType.Instant,
                AbilityTargetType.SingleEnemy,
                0,
                TimeSpan.Zero,
                TimeSpan.Zero,
                true,
                GlobalCooldownCategory.Standard,
                false,
                "PHYSICAL",
                Actions:
                [
                    new AbilityActionDefinition(
                        AbilityActionType.Damage,
                        DamageType: DamageType.Physical,
                        AttackPowerCoefficient: 0.8m)
                ]),
            ["WILD_STRIKE"] = new(
                "WILD_STRIKE",
                AbilityType.Instant,
                AbilityTargetType.SingleEnemy,
                25,
                TimeSpan.FromSeconds(6),
                TimeSpan.Zero,
                true,
                GlobalCooldownCategory.Standard,
                false,
                "PHYSICAL",
                Actions:
                [
                    new AbilityActionDefinition(
                        AbilityActionType.Damage,
                        DamageType: DamageType.Physical,
                        AttackPowerCoefficient: 1.35m)
                ]),
            ["WHIRLWIND"] = new(
                "WHIRLWIND",
                AbilityType.Instant,
                AbilityTargetType.AllEnemiesInCombat,
                35,
                TimeSpan.FromSeconds(10),
                TimeSpan.Zero,
                true,
                GlobalCooldownCategory.Standard,
                false,
                "PHYSICAL",
                Actions:
                [
                    new AbilityActionDefinition(
                        AbilityActionType.Damage,
                        DamageType: DamageType.Physical,
                        AttackPowerCoefficient: 0.7m)
                ]),
            ["BERSERK"] = new(
                "BERSERK",
                AbilityType.Instant,
                AbilityTargetType.Self,
                50,
                TimeSpan.FromMinutes(2),
                TimeSpan.Zero,
                false,
                GlobalCooldownCategory.None,
                false,
                "PHYSICAL",
                Actions:
                [
                    new AbilityActionDefinition(
                        AbilityActionType.ApplyEffect,
                        Effect: new EffectDefinition(
                            "BERSERK_ATTACK_POWER",
                            EffectKind.StatModifier,
                            TimeSpan.FromSeconds(8),
                            1,
                            EffectStackPolicy.Refresh,
                            0.15m,
                            ModifiedStat: EffectStat.AttackPower,
                            ModifierMode: EffectModifierMode.Percent)),
                    new AbilityActionDefinition(
                        AbilityActionType.ApplyEffect,
                        Effect: new EffectDefinition(
                            "BERSERK_CRITICAL_CHANCE",
                            EffectKind.StatModifier,
                            TimeSpan.FromSeconds(8),
                            1,
                            EffectStackPolicy.Refresh,
                            8,
                            ModifiedStat: EffectStat.CriticalChance,
                            ModifierMode: EffectModifierMode.Flat)),
                    new AbilityActionDefinition(
                        AbilityActionType.ApplyEffect,
                        Effect: new EffectDefinition(
                            "BERSERK_ATTACK_SPEED",
                            EffectKind.StatModifier,
                            TimeSpan.FromSeconds(8),
                            1,
                            EffectStackPolicy.Refresh,
                            0.25m,
                            ModifiedStat: EffectStat.AttackSpeed,
                            ModifierMode: EffectModifierMode.Percent))
                ])
        };

    private sealed record TestFight(
        CombatSession Session,
        CombatActorState Player,
        CombatActorState Enemy);
}
