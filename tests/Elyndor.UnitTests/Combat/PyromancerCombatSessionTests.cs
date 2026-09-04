using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class PyromancerCombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("51000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("52000000-0000-0000-0000-000000000001");

    [Fact]
    public void MagicalAbilityDamageUsesSpellPowerCoefficient()
    {
        CombatActorState source = Actor(PlayerId, hp: 100, resource: 100, spellPower: 40);
        CombatActorState target = Actor(EnemyId, hp: 100, resource: 0, spellPower: 0);
        CombatRuntimeState runtime = new(source);
        runtime.AddActor(target);
        AbilityDefinition fireball = Fireball();

        AbilityExecutionResult started = AbilityEngine.Execute(
            runtime,
            fireball,
            new AbilityIntent("fireball", fireball.Id, EnemyId),
            Now,
            Random());
        AbilityExecutionResult completed = AbilityEngine.CompleteCast(
            runtime,
            Now.AddSeconds(1.8),
            Random());

        Assert.True(started.Succeeded);
        Assert.True(completed.Succeeded);
        Assert.Equal(50, 100 - target.CurrentHp);
        Assert.Contains(completed.Events, combatEvent =>
            combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.DamageType == DamageType.Magical);
    }

    [Fact]
    public void MageRegeneratesManaDuringCombatWithoutWarriorRageRules()
    {
        TestFight fight = CreateFight(
            ResolvedTalentModifiers.Empty,
            playerResource: 0,
            resourceRegenPerSecond: 4);

        CombatCommandResult result = fight.Session.AdvanceTo(Now.AddSeconds(2));

        Assert.Equal(8, result.Snapshot.Player.Resource);
        Assert.Contains(result.Events, combatEvent =>
            combatEvent.Type == CombatEventType.ResourceChanged
            && combatEvent.DefinitionId == "COMBAT_REGEN");
        Assert.DoesNotContain(result.Events, combatEvent =>
            combatEvent.DefinitionId == "DIRECT_DAMAGE_TAKEN");
    }

    [Fact]
    public void ThreeCriticalFireballsUnlockCometAndCastingItConsumesHeatLimit()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                "F-6-1", TalentModifierKeys.OnCriticalHit, 1, 3, "MAGE_FIREBALL",
                duration: TimeSpan.FromSeconds(8)));
        TestFight fight = CreateFight(
            talents,
            playerCriticalChance: 100,
            playerCriticalDamage: 0,
            enemyHp: 10_000);

        DateTimeOffset cursor = Now.AddMilliseconds(1);
        for (var index = 0; index < 3; index++)
        {
            CombatCommandResult started = fight.Session.Handle(
                new UseAbilityCommand($"fireball-{index}", "MAGE_FIREBALL", EnemyId),
                cursor);
            Assert.True(started.Succeeded);
            cursor += TimeSpan.FromSeconds(1.8);
            fight.Session.AdvanceTo(cursor);
            cursor += TimeSpan.FromMilliseconds(1);
        }

        CombatSessionSnapshot heated = fight.Session.Snapshot();
        Assert.Contains(heated.Player.Effects, effect => effect.Id == "PYRO_HEAT_LIMIT");
        Assert.Contains("FIRE_COMET", heated.Player.KnownAbilityIds);
        Assert.Contains(heated.Player.Abilities, ability => ability.Id == "FIRE_COMET");

        CombatCommandResult comet = fight.Session.Handle(
            new UseAbilityCommand("comet", "FIRE_COMET", EnemyId),
            cursor);

        Assert.True(comet.Succeeded);
        Assert.DoesNotContain(comet.Snapshot.Player.Effects, effect => effect.Id == "PYRO_HEAT_LIMIT");
        Assert.DoesNotContain("FIRE_COMET", comet.Snapshot.Player.KnownAbilityIds);
    }

    [Fact]
    public void CriticalFireballBurnCanKillThroughNormalCombatDeathPipeline()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                "F-2-2", TalentModifierKeys.OnCriticalHit, 2, 7, "MAGE_FIREBALL",
                duration: TimeSpan.FromSeconds(4),
                tickInterval: TimeSpan.FromSeconds(1)));
        TestFight fight = CreateFight(
            talents,
            playerSpellPower: 10,
            playerCriticalChance: 100,
            playerCriticalDamage: 0,
            enemyHp: 14);

        DateTimeOffset startedAt = Now.AddMilliseconds(1);
        Assert.True(fight.Session.Handle(
            new UseAbilityCommand("ignite", "MAGE_FIREBALL", EnemyId),
            startedAt).Succeeded);
        DateTimeOffset completedAt = startedAt.AddSeconds(1.8);
        CombatCommandResult completed = fight.Session.AdvanceTo(completedAt);

        Assert.Contains(completed.Snapshot.Enemy.Effects, effect => effect.Id == "PYRO_BURN");

        CombatCommandResult ticked = fight.Session.AdvanceTo(completedAt.AddSeconds(1));

        Assert.Equal(CombatSessionStatus.Victory, ticked.Snapshot.Status);
        Assert.Contains(ticked.Events, combatEvent =>
            combatEvent.Type == CombatEventType.EnemyKilled && combatEvent.IsPeriodic);
        Assert.Contains(ticked.Events, combatEvent =>
            combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.DefinitionId == "PYRO_BURN"
            && combatEvent.DamageType == DamageType.Magical);
    }

    [Fact]
    public void AvatarExtendsCombustionAndPerfectCombustionResetsFireCooldowns()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                "F-5-1", TalentModifierKeys.OnAbilityUsed, 1, 15, "COMBUSTION",
                secondaryValue: 8,
                duration: TimeSpan.FromSeconds(10)),
            Hook("F-8-1", TalentModifierKeys.OnAbilityUsed, 1, 15, "COMBUSTION"),
            Hook(
                "F-9-1", TalentModifierKeys.OnAbilityUsed, 1, 8, "FIRE",
                internalCooldown: TimeSpan.FromSeconds(6),
                secondaryValue: 3));
        TestFight fight = CreateFight(talents, enemyHp: 10_000);

        DateTimeOffset cursor = Now.AddMilliseconds(1);
        Assert.True(fight.Session.Handle(
            new UseAbilityCommand("flash", "FLAME_FLASH", EnemyId), cursor).Succeeded);
        cursor += TimeSpan.FromSeconds(1.501);
        Assert.True(fight.Session.Handle(
            new UseAbilityCommand("wave", "FIRE_WAVE", EnemyId), cursor).Succeeded);
        Assert.Contains("FLAME_FLASH", fight.Session.Snapshot().Player.Cooldowns.Keys);
        Assert.Contains("FIRE_WAVE", fight.Session.Snapshot().Player.Cooldowns.Keys);

        cursor += TimeSpan.FromMilliseconds(1);
        CombatCommandResult combustion = fight.Session.Handle(
            new UseAbilityCommand("combustion", "COMBUSTION", PlayerId),
            cursor);

        Assert.True(combustion.Succeeded);
        Assert.DoesNotContain("FLAME_FLASH", combustion.Snapshot.Player.Cooldowns.Keys);
        Assert.DoesNotContain("FIRE_WAVE", combustion.Snapshot.Player.Cooldowns.Keys);
        CombatEffectSnapshot effect = Assert.Single(combustion.Snapshot.Player.Effects, item =>
            item.Id == "PYRO_COMBUSTION");
        Assert.Equal(TimeSpan.FromSeconds(13), effect.ExpiresAtUtc - cursor);
    }

    private static ResolvedTalentEventHook Hook(
        string talentId,
        string key,
        int rank,
        decimal value,
        string? targetId = null,
        TimeSpan? internalCooldown = null,
        decimal secondaryValue = 0,
        decimal threshold = 0,
        decimal chancePercent = 100,
        TimeSpan? duration = null,
        TimeSpan? tickInterval = null) =>
        new(
            talentId,
            key,
            rank,
            value,
            targetId,
            internalCooldown ?? TimeSpan.Zero,
            false,
            secondaryValue,
            threshold,
            chancePercent,
            duration ?? TimeSpan.Zero,
            tickInterval ?? TimeSpan.Zero);

    private static ResolvedTalentModifiers Talents(
        params ResolvedTalentEventHook[] hooks) =>
        ResolvedTalentModifiers.Empty with { EventHooks = hooks };

    private static TestFight CreateFight(
        ResolvedTalentModifiers talents,
        decimal playerResource = 100,
        decimal resourceRegenPerSecond = 0,
        decimal playerSpellPower = 40,
        decimal playerCriticalChance = 0,
        decimal playerCriticalDamage = 0,
        decimal enemyHp = 1_000)
    {
        CombatActorState playerActor = Actor(
            PlayerId,
            200,
            playerResource,
            playerSpellPower,
            playerCriticalChance,
            playerCriticalDamage);
        CombatActorState enemyActor = Actor(EnemyId, enemyHp, 0, 0);
        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            "MAGE",
            "Mage",
            "MANA",
            new AutoAttackProfile(TimeSpan.FromSeconds(2.6), 0, 0, 0),
            new HashSet<string>(
                [
                    "MAGE_FIREBALL",
                    "MAGE_ARCANE_SPARK",
                    "MAGE_ICE_SHARD",
                    "FLAME_FLASH",
                    "FIRE_WAVE",
                    "COMBUSTION"
                ],
                StringComparer.Ordinal),
            resourceRegenPerSecond);
        CombatParticipantDefinition enemy = new(
            enemyActor,
            CombatActorKind.Monster,
            "WOLF",
            "Wolf",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(60), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal));
        CombatSession session = new(
            Guid.NewGuid(),
            player,
            enemy,
            Abilities(),
            new MonsterAiProfile("PASSIVE", []),
            talents,
            Random(500),
            Now);
        return new TestFight(session, playerActor, enemyActor);
    }

    private static CombatActorState Actor(
        Guid id,
        decimal hp,
        decimal resource,
        decimal spellPower,
        decimal criticalChance = 0,
        decimal criticalDamage = 0) =>
        new(
            id,
            hp,
            hp,
            100,
            resource,
            new CombatStats(
                20,
                100,
                0,
                criticalChance,
                criticalDamage,
                0,
                0,
                0,
                0,
                0,
                spellPower));

    private static Dictionary<string, AbilityDefinition> Abilities() =>
        new(StringComparer.Ordinal)
        {
            ["MAGE_FIREBALL"] = Fireball(),
            ["MAGE_ARCANE_SPARK"] = DamageAbility(
                "MAGE_ARCANE_SPARK", AbilityType.Instant, "ARCANE", 15, 3, 0, 0.75m),
            ["MAGE_ICE_SHARD"] = DamageAbility(
                "MAGE_ICE_SHARD", AbilityType.Casted, "FROST", 18, 0, 1.5, 1.05m),
            ["FLAME_FLASH"] = DamageAbility(
                "FLAME_FLASH", AbilityType.Instant, "FIRE", 18, 8, 0, 0.95m),
            ["FIRE_WAVE"] = new(
                "FIRE_WAVE",
                AbilityType.Instant,
                AbilityTargetType.AllEnemiesInCombat,
                30,
                TimeSpan.FromSeconds(10),
                TimeSpan.Zero,
                true,
                GlobalCooldownCategory.Standard,
                true,
                "FIRE",
                Actions:
                [
                    new AbilityActionDefinition(
                        AbilityActionType.Damage,
                        DamageType: DamageType.Magical,
                        SpellPowerCoefficient: 0.75m)
                ]),
            ["COMBUSTION"] = new(
                "COMBUSTION",
                AbilityType.Instant,
                AbilityTargetType.Self,
                0,
                TimeSpan.FromSeconds(100),
                TimeSpan.Zero,
                false,
                GlobalCooldownCategory.None,
                true,
                "FIRE",
                Actions: []),
            ["FIRE_COMET"] = DamageAbility(
                "FIRE_COMET", AbilityType.Casted, "FIRE", 0, 0, 0.5, 2.40m)
        };

    private static AbilityDefinition Fireball() =>
        DamageAbility("MAGE_FIREBALL", AbilityType.Casted, "FIRE", 20, 0, 1.8, 1.25m);

    private static AbilityDefinition DamageAbility(
        string id,
        AbilityType type,
        string school,
        decimal mana,
        double cooldownSeconds,
        double castSeconds,
        decimal coefficient) =>
        new(
            id,
            type,
            AbilityTargetType.SingleEnemy,
            mana,
            TimeSpan.FromSeconds(cooldownSeconds),
            TimeSpan.FromSeconds(castSeconds),
            true,
            GlobalCooldownCategory.Standard,
            true,
            school,
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    DamageType: DamageType.Magical,
                    SpellPowerCoefficient: coefficient)
            ]);

    private static SequenceGameRandom Random(int count = 20) =>
        new(Enumerable.Repeat(0.5m, count).ToArray());

    private sealed record TestFight(
        CombatSession Session,
        CombatActorState Player,
        CombatActorState Enemy);
}
