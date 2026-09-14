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
    public void CastedAbilityIsProjectedIntoAuthoritativeCombatSnapshot()
    {
        TestFight fight = CreateFight(
            ResolvedTalentModifiers.Empty,
            enemyHp: 10_000);

        CombatCommandResult started = fight.Session.Handle(
            new UseAbilityCommand("fireball-cast-state", "MAGE_FIREBALL", EnemyId),
            Now.AddMilliseconds(1));

        Assert.True(started.Succeeded);
        CombatCastSnapshot cast = Assert.IsType<CombatCastSnapshot>(
            started.Snapshot.Player.ActiveCast);
        Assert.Equal("MAGE_FIREBALL", cast.AbilityId);
        Assert.Equal(Now.AddMilliseconds(1), cast.StartedAtUtc);
        Assert.Equal(Now.AddMilliseconds(1).AddSeconds(1.8), cast.ResolvesAtUtc);
    }

    [Fact]
    public void TwoDirectFireCritsGrantHotStreakAndPyroblastConsumesIt()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                "F-8-1",
                TalentModifierKeys.OnAbilityUsed,
                1,
                1,
                duration: TimeSpan.FromSeconds(10)));
        TestFight fight = CreateFight(
            talents,
            playerCriticalChance: 100,
            playerCriticalDamage: 0,
            enemyHp: 10_000);

        DateTimeOffset cursor = Now.AddMilliseconds(1);
        for (var index = 0; index < 2; index++)
        {
            CombatCommandResult started = fight.Session.Handle(
                new UseAbilityCommand($"fireball-{index}", "MAGE_FIREBALL", EnemyId),
                cursor);
            Assert.True(started.Succeeded);
            cursor += TimeSpan.FromSeconds(1.8);
            fight.Session.AdvanceTo(cursor);
            cursor += TimeSpan.FromMilliseconds(1);
        }

        Assert.Contains(
            fight.Session.Snapshot().Player.Effects,
            effect => effect.Id == "MAGE_HOT_STREAK");

        CombatCommandResult pyro = fight.Session.Handle(
            new UseAbilityCommand("hot-pyro", "MAGE_PYROBLAST", EnemyId),
            cursor);

        Assert.True(pyro.Succeeded);
        Assert.Equal(45, pyro.Snapshot.Player.Resource);
        CombatCastSnapshot cast = Assert.IsType<CombatCastSnapshot>(pyro.Snapshot.Player.ActiveCast);
        Assert.Equal(cursor, cast.ResolvesAtUtc);
        Assert.DoesNotContain(
            pyro.Snapshot.Player.Effects,
            effect => effect.Id == "MAGE_HOT_STREAK");
    }

    [Fact]
    public void CriticalFireballAppliesRollingIgniteThatTicksAsMagicalDamage()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook("F-2-1", TalentModifierKeys.OnAbilityUsed, 1, 40));
        TestFight fight = CreateFight(
            talents,
            playerSpellPower: 40,
            playerCriticalChance: 100,
            playerCriticalDamage: 0,
            enemyHp: 1_000);

        DateTimeOffset startedAt = Now.AddMilliseconds(1);
        Assert.True(fight.Session.Handle(
            new UseAbilityCommand("ignite", "MAGE_FIREBALL", EnemyId),
            startedAt).Succeeded);
        DateTimeOffset completedAt = startedAt.AddSeconds(1.8);
        CombatCommandResult completed = fight.Session.AdvanceTo(completedAt);

        Assert.Contains(
            completed.Snapshot.Enemy.Effects,
            effect => effect.Id == "MAGE_FIRE_IGNITE");

        CombatCommandResult ticked = fight.Session.AdvanceTo(completedAt.AddSeconds(1));

        Assert.Contains(ticked.Events, combatEvent =>
            combatEvent.Type == CombatEventType.DamageDealt
            && combatEvent.DefinitionId == "MAGE_FIRE_IGNITE"
            && combatEvent.DamageType == DamageType.Magical
            && combatEvent.IsPeriodic);
    }

    [Fact]
    public void PerfectCombustionResetsFireCooldownsAndEmbodimentExtendsWindow()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(
                "F-6-1",
                TalentModifierKeys.OnAbilityUsed,
                1,
                10,
                duration: TimeSpan.FromSeconds(10)),
            Hook("F-8-3", TalentModifierKeys.OnAbilityUsed, 1, 15),
            Hook("F-9-1", TalentModifierKeys.OnAbilityUsed, 1, 5));
        TestFight fight = CreateFight(talents, enemyHp: 10_000);

        DateTimeOffset cursor = Now.AddMilliseconds(1);
        Assert.True(fight.Session.Handle(
            new UseAbilityCommand("blast", "MAGE_FIRE_BLAST", EnemyId), cursor).Succeeded);
        cursor += TimeSpan.FromSeconds(1.501);
        Assert.True(fight.Session.Handle(
            new UseAbilityCommand("wave", "MAGE_BLAST_WAVE", EnemyId), cursor).Succeeded);
        Assert.Contains("MAGE_FIRE_BLAST", fight.Session.Snapshot().Player.Cooldowns.Keys);
        Assert.Contains("MAGE_BLAST_WAVE", fight.Session.Snapshot().Player.Cooldowns.Keys);

        cursor += TimeSpan.FromMilliseconds(1);
        CombatCommandResult combustion = fight.Session.Handle(
            new UseAbilityCommand("combustion", "MAGE_COMBUSTION", PlayerId),
            cursor);

        Assert.True(combustion.Succeeded);
        Assert.DoesNotContain("MAGE_FIRE_BLAST", combustion.Snapshot.Player.Cooldowns.Keys);
        Assert.DoesNotContain("MAGE_BLAST_WAVE", combustion.Snapshot.Player.Cooldowns.Keys);
        CombatEffectSnapshot effect = Assert.Single(combustion.Snapshot.Player.Effects, item =>
            item.Id == "MAGE_COMBUSTION_ACTIVE");
        Assert.Equal(TimeSpan.FromSeconds(15), effect.ExpiresAtUtc - cursor);
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
                    "MAGE_FIRE_BLAST",
                    "MAGE_PYROBLAST",
                    "MAGE_BLAST_WAVE",
                    "MAGE_COMBUSTION",
                    "MAGE_ARCANE_SPARK",
                    "MAGE_ICE_SHARD"
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
            ["MAGE_FIRE_BLAST"] = DamageAbility(
                "MAGE_FIRE_BLAST", AbilityType.Instant, "FIRE", 18, 8, 0, 0.95m),
            ["MAGE_PYROBLAST"] = DamageAbility(
                "MAGE_PYROBLAST", AbilityType.Casted, "FIRE", 30, 0, 2.5, 2.20m),
            ["MAGE_BLAST_WAVE"] = new(
                "MAGE_BLAST_WAVE",
                AbilityType.Instant,
                AbilityTargetType.AllEnemiesInCombat,
                30,
                TimeSpan.FromSeconds(12),
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
                        SpellPowerCoefficient: 0.80m)
                ]),
            ["MAGE_COMBUSTION"] = new(
                "MAGE_COMBUSTION",
                AbilityType.Instant,
                AbilityTargetType.Self,
                0,
                TimeSpan.FromSeconds(36),
                TimeSpan.Zero,
                false,
                GlobalCooldownCategory.None,
                true,
                "FIRE",
                Actions: []),
            ["MAGE_ARCANE_SPARK"] = DamageAbility(
                "MAGE_ARCANE_SPARK", AbilityType.Instant, "ARCANE", 15, 3, 0, 0.75m),
            ["MAGE_ICE_SHARD"] = DamageAbility(
                "MAGE_ICE_SHARD", AbilityType.Casted, "FROST", 18, 0, 1.5, 1.05m)
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
