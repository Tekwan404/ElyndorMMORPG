using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatSessionDamageReflectionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 8, 30, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("b2000000-0000-0000-0000-000000000001");

    [Fact]
    public void DirectDamageReflectsActualDamageWithConfiguredRatio()
    {
        AbilityDefinition strike = DamageAbility("TEST_STRIKE", 20);
        (CombatSession session, CombatActorState player, CombatActorState enemy) =
            CreateSession(strike);
        ApplyReflection(enemy, "TEST_REFLECT", 0.35m);

        CombatCommandResult result = session.Handle(
            new UseAbilityCommand("strike", strike.Id, EnemyId),
            Now);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(80, enemy.CurrentHp);
        Assert.Equal(493, player.CurrentHp);
        CombatEvent reflected = Assert.Single(
            result.Events,
            item => item.Type == CombatEventType.DamageDealt && item.IsReflected);
        Assert.Equal(7, reflected.Amount);
        Assert.Equal("TEST_REFLECT", reflected.DefinitionId);
        Assert.Equal(EnemyId, reflected.SourceActorId);
        Assert.Equal(PlayerId, reflected.TargetActorId);
    }

    [Fact]
    public void ReflectionCapLimitsReturnedDamage()
    {
        AbilityDefinition strike = DamageAbility("HEAVY_STRIKE", 80);
        (CombatSession session, CombatActorState player, CombatActorState enemy) =
            CreateSession(strike);
        ApplyReflection(enemy, "CAPPED_REFLECT", 0.50m, cap: 12);

        Assert.True(session.Handle(
            new UseAbilityCommand("heavy", strike.Id, EnemyId),
            Now).Succeeded);

        Assert.Equal(20, enemy.CurrentHp);
        Assert.Equal(488, player.CurrentHp);
        CombatEvent reflected = Assert.Single(
            session.GetEventsAfter(0),
            item => item.Type == CombatEventType.DamageDealt && item.IsReflected);
        Assert.Equal(12, reflected.Amount);
    }

    [Fact]
    public void ReflectedDamageNeverReflectsAgain()
    {
        AbilityDefinition strike = DamageAbility("LOOP_TEST", 20);
        (CombatSession session, CombatActorState player, CombatActorState enemy) =
            CreateSession(strike);
        ApplyReflection(enemy, "ENEMY_REFLECT", 0.50m);
        ApplyReflection(player, "PLAYER_REFLECT", 0.50m);

        Assert.True(session.Handle(
            new UseAbilityCommand("loop", strike.Id, EnemyId),
            Now).Succeeded);

        Assert.Equal(80, enemy.CurrentHp);
        Assert.Equal(490, player.CurrentHp);
        CombatEvent[] reflected = session.GetEventsAfter(0)
            .Where(item => item.Type == CombatEventType.DamageDealt && item.IsReflected)
            .ToArray();
        Assert.Single(reflected);
        Assert.Equal("ENEMY_REFLECT", reflected[0].DefinitionId);
    }

    [Fact]
    public void PeriodicDamageDoesNotTriggerReflection()
    {
        EffectDefinition dot = new(
            "TEST_DOT",
            EffectKind.DamageOverTime,
            TimeSpan.FromSeconds(3),
            1,
            EffectStackPolicy.Replace,
            10,
            TickInterval: TimeSpan.FromSeconds(1),
            PeriodicDamageType: DamageType.True);
        AbilityDefinition applyDot = new(
            "APPLY_TEST_DOT",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            true,
            "SHADOW",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.ApplyEffect,
                    Effect: dot)
            ]);
        (CombatSession session, CombatActorState player, CombatActorState enemy) =
            CreateSession(applyDot);
        ApplyReflection(enemy, "PERIODIC_REFLECT", 0.50m);

        Assert.True(session.Handle(
            new UseAbilityCommand("dot", applyDot.Id, EnemyId),
            Now).Succeeded);
        session.AdvanceTo(Now.AddSeconds(1));

        Assert.Equal(90, enemy.CurrentHp);
        Assert.Equal(500, player.CurrentHp);
        Assert.DoesNotContain(
            session.GetEventsAfter(0),
            item => item.Type == CombatEventType.DamageDealt && item.IsReflected);
    }

    [Fact]
    public void ReflectionEffectRequiresPositiveRatioAndCap()
    {
        CombatActorState actor = CombatActorState.CreateDummy(100);
        EffectDefinition zeroRatio = Reflection("ZERO", 0);
        EffectDefinition zeroCap = Reflection("ZERO_CAP", 0.25m, cap: 0);

        Assert.Throws<ArgumentException>(() =>
            EffectEngine.Apply(actor, actor.ActorId, zeroRatio, Now));
        Assert.Throws<ArgumentException>(() =>
            EffectEngine.Apply(actor, actor.ActorId, zeroCap, Now));
    }

    private static (CombatSession Session, CombatActorState Player, CombatActorState Enemy)
        CreateSession(AbilityDefinition playerAbility)
    {
        CombatStats stats = new(
            Level: 10,
            Accuracy: 100,
            Dodge: 0,
            CriticalChance: 0,
            CriticalDamage: 1,
            Armor: 0,
            MagicResistance: 0,
            ArmorPenetration: 0,
            MagicPenetration: 0);
        CombatActorState playerActor = new(PlayerId, 500, 500, 100, 100, stats);
        CombatActorState enemyActor = new(EnemyId, 100, 100, 0, 0, stats);
        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            "TEST_PLAYER",
            "Test Player",
            "MANA",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal) { playerAbility.Id },
            CanAutoAttack: false);
        CombatParticipantDefinition enemy = new(
            enemyActor,
            CombatActorKind.Monster,
            "TEST_REFLECTOR",
            "Test Reflector",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal));
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            [playerAbility.Id] = playerAbility
        };
        CombatSession session = new(
            Guid.Parse("b0000000-0000-0000-0000-000000000001"),
            player,
            enemy,
            abilities,
            new MonsterAiProfile("PASSIVE", []),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.5m, 100).ToArray()),
            Now);
        return (session, playerActor, enemyActor);
    }

    private static AbilityDefinition DamageAbility(string id, decimal amount) =>
        new(
            id,
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    Amount: amount,
                    DamageType: DamageType.True,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false)
            ]);

    private static void ApplyReflection(
        CombatActorState actor,
        string id,
        decimal ratio,
        decimal? cap = null) =>
        EffectEngine.Apply(actor, actor.ActorId, Reflection(id, ratio, cap), Now);

    private static EffectDefinition Reflection(
        string id,
        decimal ratio,
        decimal? cap = null) =>
        new(
            id,
            EffectKind.DamageReflection,
            TimeSpan.FromSeconds(4),
            1,
            EffectStackPolicy.Replace,
            ratio,
            ReflectedDamageCap: cap);
}
