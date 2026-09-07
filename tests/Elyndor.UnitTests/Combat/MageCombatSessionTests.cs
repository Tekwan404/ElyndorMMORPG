using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class MageCombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("72000000-0000-0000-0000-000000000001");

    [Fact]
    public void FourArcaneSparksExposeCascadeAndCascadeConsumesTwoCharges()
    {
        ResolvedTalentModifiers talents = Talents(
            unlocked: new HashSet<string>(["ARCANE_CASCADE"], StringComparer.Ordinal),
            Hook("A-2-1", TalentModifierKeys.OnAbilityUsed, 1, 1, duration: TimeSpan.FromSeconds(12)),
            Hook("A-6-1", TalentModifierKeys.OnAbilityUsed, 1, 2));
        CombatSession session = CreateSession(
            talents,
            new HashSet<string>(["MAGE_ARCANE_SPARK", "ARCANE_CASCADE"], StringComparer.Ordinal));

        Assert.DoesNotContain("ARCANE_CASCADE", session.Snapshot().Player.KnownAbilityIds);

        DateTimeOffset cursor = Now.AddMilliseconds(1);
        for (var index = 0; index < 4; index++)
        {
            CombatCommandResult spark = session.Handle(
                new UseAbilityCommand($"spark-{index}", "MAGE_ARCANE_SPARK", EnemyId),
                cursor);
            Assert.True(spark.Succeeded);
            cursor += TimeSpan.FromSeconds(3.001);
        }

        CombatSessionSnapshot charged = session.Snapshot();
        CombatEffectSnapshot charge = Assert.Single(
            charged.Player.Effects,
            effect => effect.Id == "MAGE_ARCANE_CHARGE");
        Assert.Equal(4, charge.Stacks);
        Assert.Contains("ARCANE_CASCADE", charged.Player.KnownAbilityIds);

        CombatCommandResult cascade = session.Handle(
            new UseAbilityCommand("cascade", "ARCANE_CASCADE", EnemyId),
            cursor);

        Assert.True(cascade.Succeeded);
        CombatEffectSnapshot remaining = Assert.Single(
            cascade.Snapshot.Player.Effects,
            effect => effect.Id == "MAGE_ARCANE_CHARGE");
        Assert.Equal(2, remaining.Stacks);
        Assert.DoesNotContain("ARCANE_CASCADE", cascade.Snapshot.Player.KnownAbilityIds);
    }

    [Fact]
    public void ArcaneBurstScalesWithChargesConsumesThemAndReturnsMana()
    {
        ResolvedTalentModifiers talents = Talents(
            unlocked: new HashSet<string>(StringComparer.Ordinal),
            Hook("A-2-1", TalentModifierKeys.OnAbilityUsed, 1, 1, duration: TimeSpan.FromSeconds(12)),
            Hook("A-5-3", TalentModifierKeys.OnAbilityUsed, 4, 5));
        CombatSession session = CreateSession(
            talents,
            new HashSet<string>(["MAGE_ARCANE_SPARK", "ARCANE_BURST"], StringComparer.Ordinal),
            playerResource: 200,
            maxResource: 200);

        DateTimeOffset cursor = Now.AddMilliseconds(1);
        for (var index = 0; index < 3; index++)
        {
            Assert.True(session.Handle(
                new UseAbilityCommand($"charge-{index}", "MAGE_ARCANE_SPARK", EnemyId),
                cursor).Succeeded);
            cursor += TimeSpan.FromSeconds(3.001);
        }

        decimal beforeHp = session.Snapshot().Enemy.Hp;
        decimal beforeMana = session.Snapshot().Player.Resource;
        CombatCommandResult started = session.Handle(
            new UseAbilityCommand("burst", "ARCANE_BURST", EnemyId),
            cursor);
        Assert.True(started.Succeeded);

        CombatCommandResult completed = session.AdvanceTo(cursor.AddSeconds(1.2));

        Assert.DoesNotContain(
            completed.Snapshot.Player.Effects,
            effect => effect.Id == "MAGE_ARCANE_CHARGE");
        Assert.True(beforeHp - completed.Snapshot.Enemy.Hp > 110);
        Assert.Equal(beforeMana - 25 + 15, completed.Snapshot.Player.Resource);
    }

    [Fact]
    public void FrostbiteBuildsToThreeStacksAndIceLanceConsumesOne()
    {
        ResolvedTalentModifiers talents = Talents(
            unlocked: new HashSet<string>(StringComparer.Ordinal),
            Hook(
                "I-2-1",
                TalentModifierKeys.OnAbilityUsed,
                4,
                4,
                duration: TimeSpan.FromSeconds(6)));
        CombatSession session = CreateSession(
            talents,
            new HashSet<string>(["MAGE_ICE_SHARD", "ICE_LANCE"], StringComparer.Ordinal));

        DateTimeOffset cursor = Now.AddMilliseconds(1);
        for (var index = 0; index < 3; index++)
        {
            Assert.True(session.Handle(
                new UseAbilityCommand($"shard-{index}", "MAGE_ICE_SHARD", EnemyId),
                cursor).Succeeded);
            cursor += TimeSpan.FromSeconds(1.5);
            session.AdvanceTo(cursor);
            cursor += TimeSpan.FromMilliseconds(1);
        }

        CombatEffectSnapshot frostbite = Assert.Single(
            session.Snapshot().Enemy.Effects,
            effect => effect.Id == "MAGE_FROSTBITE");
        Assert.Equal(3, frostbite.Stacks);
        Assert.Contains(
            session.Snapshot().Enemy.Effects,
            effect => effect.Id == "MAGE_FROSTBITE_ATTACK_SPEED");

        CombatCommandResult lance = session.Handle(
            new UseAbilityCommand("lance", "ICE_LANCE", EnemyId),
            cursor);

        Assert.True(lance.Succeeded);
        CombatEffectSnapshot remaining = Assert.Single(
            lance.Snapshot.Enemy.Effects,
            effect => effect.Id == "MAGE_FROSTBITE");
        Assert.Equal(2, remaining.Stacks);
    }

    [Fact]
    public void IncomingCriticalCreatesCrystalShield()
    {
        ResolvedTalentModifiers talents = Talents(
            unlocked: new HashSet<string>(StringComparer.Ordinal),
            Hook(
                "I-2-3",
                TalentModifierKeys.OnDamageTaken,
                2,
                4,
                internalCooldown: TimeSpan.FromSeconds(12),
                duration: TimeSpan.FromSeconds(5)));
        CombatSession session = CreateSession(
            talents,
            new HashSet<string>(["MAGE_ICE_SHARD"], StringComparer.Ordinal),
            enemyCriticalChance: 100,
            enemyAutoAttackDamage: 10,
            enemyAutoAttackInterval: TimeSpan.FromSeconds(1));

        CombatCommandResult result = session.AdvanceTo(Now.AddSeconds(1));

        Assert.Contains(
            result.Snapshot.Player.Effects,
            effect => effect.Id == "MAGE_CRYSTAL_SHIELD");
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
        TimeSpan? tickInterval = null,
        int triggerCount = 0,
        decimal castTimeSeconds = 0,
        decimal resourceCostReductionPercent = 0) =>
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
            tickInterval ?? TimeSpan.Zero,
            triggerCount,
            castTimeSeconds,
            resourceCostReductionPercent);

    private static ResolvedTalentModifiers Talents(
        IReadOnlySet<string> unlocked,
        params ResolvedTalentEventHook[] hooks) =>
        ResolvedTalentModifiers.Empty with
        {
            UnlockedAbilityIds = unlocked,
            EventHooks = hooks
        };

    private static CombatSession CreateSession(
        ResolvedTalentModifiers talents,
        IReadOnlySet<string> knownAbilityIds,
        decimal playerResource = 100,
        decimal maxResource = 100,
        decimal enemyCriticalChance = 0,
        decimal enemyAutoAttackDamage = 0,
        TimeSpan? enemyAutoAttackInterval = null)
    {
        CombatActorState playerActor = Actor(
            PlayerId,
            hp: 500,
            maxResource,
            playerResource,
            spellPower: 100,
            criticalChance: 0);
        CombatActorState enemyActor = Actor(
            EnemyId,
            hp: 100_000,
            maxResource: 0,
            resource: 0,
            spellPower: 0,
            criticalChance: enemyCriticalChance);

        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            "MAGE",
            "Mage",
            "MANA",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            knownAbilityIds,
            CanAutoAttack: false);
        CombatParticipantDefinition enemy = new(
            enemyActor,
            CombatActorKind.Monster,
            "TEST_ENEMY",
            "Enemy",
            "NONE",
            new AutoAttackProfile(
                enemyAutoAttackInterval ?? TimeSpan.FromHours(1),
                enemyAutoAttackDamage,
                0,
                0),
            new HashSet<string>(StringComparer.Ordinal));

        return new CombatSession(
            Guid.NewGuid(),
            player,
            enemy,
            Abilities(),
            new MonsterAiProfile("PASSIVE", []),
            talents,
            new SequenceGameRandom(Enumerable.Repeat(0.5m, 200).ToArray()),
            Now);
    }

    private static CombatActorState Actor(
        Guid id,
        decimal hp,
        decimal maxResource,
        decimal resource,
        decimal spellPower,
        decimal criticalChance) =>
        new(
            id,
            hp,
            hp,
            maxResource,
            resource,
            new CombatStats(
                Level: 20,
                Accuracy: 100,
                Dodge: 0,
                CriticalChance: criticalChance,
                CriticalDamage: 0,
                Armor: 0,
                MagicResistance: 0,
                ArmorPenetration: 0,
                MagicPenetration: 0,
                AttackPower: 0,
                SpellPower: spellPower));

    private static Dictionary<string, AbilityDefinition> Abilities() =>
        new(StringComparer.Ordinal)
        {
            ["MAGE_ARCANE_SPARK"] = Damage(
                "MAGE_ARCANE_SPARK", AbilityType.Instant, "ARCANE", 15, 3, 0, 0.75m),
            ["ARCANE_BURST"] = Damage(
                "ARCANE_BURST", AbilityType.Casted, "ARCANE", 25, 6, 1.2, 1.10m),
            ["ARCANE_CASCADE"] = Damage(
                "ARCANE_CASCADE", AbilityType.Instant, "ARCANE", 20, 8, 0, 1.50m),
            ["MAGE_ICE_SHARD"] = Damage(
                "MAGE_ICE_SHARD", AbilityType.Casted, "FROST", 18, 0, 1.5, 1.05m),
            ["ICE_LANCE"] = Damage(
                "ICE_LANCE", AbilityType.Instant, "FROST", 18, 6, 0, 0.95m)
        };

    private static AbilityDefinition Damage(
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
}
