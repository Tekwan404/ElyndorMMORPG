using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatSessionEffectExpirationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("c2000000-0000-0000-0000-000000000001");
    private static readonly Guid CompanionId =
        Guid.Parse("c3000000-0000-0000-0000-000000000001");

    [Fact]
    public void DoomDamageExecutesAtExactEffectExpirationTimestamp()
    {
        EffectDefinition doom = DoomEffect("TEST_DOOM", 30);
        TestCombat combat = CreateSession([], companion: false);
        EffectEngine.Apply(combat.Player, EnemyId, doom, Now);

        Assert.Equal(Now.AddSeconds(2), combat.Session.NextDueAtUtc);
        combat.Session.AdvanceTo(Now.AddSeconds(1));
        Assert.Equal(500, combat.Player.CurrentHp);

        combat.Session.AdvanceTo(Now.AddSeconds(2));

        Assert.Equal(470, combat.Player.CurrentHp);
        Assert.Contains(combat.Session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.EffectExpired
            && item.DefinitionId == doom.Id
            && item.OccurredAtUtc == Now.AddSeconds(2));
        CombatEvent damage = Assert.Single(
            combat.Session.GetEventsAfter(0),
            item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == doom.Id);
        Assert.Equal(30, damage.Amount);
        Assert.Equal(EnemyId, damage.SourceActorId);
        Assert.Equal(PlayerId, damage.TargetActorId);
        Assert.Equal(Now.AddSeconds(2), damage.OccurredAtUtc);
    }

    [Fact]
    public void DispelBeforeExpirationCancelsDoomDamage()
    {
        AbilityDefinition dispel = new(
            "REMOVE_CURSE",
            AbilityType.Instant,
            AbilityTargetType.Self,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            true,
            "HOLY",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Dispel,
                    DispelCategory: "Curse")
            ]);
        EffectDefinition doom = DoomEffect("DISPELLABLE_DOOM", 40);
        TestCombat combat = CreateSession([dispel], companion: false);
        EffectEngine.Apply(combat.Player, EnemyId, doom, Now);

        CombatCommandResult dispelled = combat.Session.Handle(
            new UseAbilityCommand("dispel-doom", dispel.Id, PlayerId),
            Now.AddSeconds(1));
        Assert.True(dispelled.Succeeded, dispelled.ErrorCode);
        Assert.Contains(dispelled.Events, item =>
            item.Type == CombatEventType.EffectRemoved
            && item.DefinitionId == doom.Id);

        combat.Session.AdvanceTo(Now.AddSeconds(3));

        Assert.Equal(500, combat.Player.CurrentHp);
        Assert.DoesNotContain(combat.Session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.DamageDealt
            && item.DefinitionId == doom.Id);
        Assert.DoesNotContain(combat.Session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.EffectExpired
            && item.DefinitionId == doom.Id);
    }

    [Fact]
    public void ExpirationSplashHitsMarkedTargetAndItsLivingAllies()
    {
        EffectDefinition bomb = new(
            "TEST_SPLASH_MARK",
            EffectKind.Debuff,
            TimeSpan.FromSeconds(2),
            1,
            EffectStackPolicy.Replace,
            0,
            DispelCategory: "Magic",
            OnExpireActions:
            [
                new EffectExpirationActionDefinition(
                    EffectExpirationActionType.Damage,
                    Amount: 10,
                    DamageType: DamageType.True,
                    TargetScope: EffectExpirationTargetScope.EffectTargetAndAllies)
            ]);
        TestCombat combat = CreateSession([], companion: true);
        EffectEngine.Apply(combat.Player, EnemyId, bomb, Now);

        combat.Session.AdvanceTo(Now.AddSeconds(2));

        Assert.Equal(490, combat.Player.CurrentHp);
        Assert.NotNull(combat.Companion);
        Assert.Equal(190, combat.Companion!.CurrentHp);
        CombatEvent[] damage = combat.Session.GetEventsAfter(0)
            .Where(item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == bomb.Id)
            .ToArray();
        Assert.Equal(2, damage.Length);
        Assert.Contains(damage, item => item.TargetActorId == PlayerId);
        Assert.Contains(damage, item => item.TargetActorId == CompanionId);
    }

    [Fact]
    public void InvalidExpirationActionsAreRejectedAtEffectApplication()
    {
        CombatActorState actor = CombatActorState.CreateDummy(100);
        EffectDefinition invalidDamage = new(
            "INVALID_EXPIRE_DAMAGE",
            EffectKind.Debuff,
            TimeSpan.FromSeconds(1),
            1,
            EffectStackPolicy.Replace,
            0,
            OnExpireActions:
            [
                new EffectExpirationActionDefinition(
                    EffectExpirationActionType.Damage,
                    Amount: 0)
            ]);
        EffectDefinition invalidApply = new(
            "INVALID_EXPIRE_EFFECT",
            EffectKind.Debuff,
            TimeSpan.FromSeconds(1),
            1,
            EffectStackPolicy.Replace,
            0,
            OnExpireActions:
            [new EffectExpirationActionDefinition(EffectExpirationActionType.ApplyEffect)]);

        Assert.Throws<ArgumentException>(() =>
            EffectEngine.Apply(actor, actor.ActorId, invalidDamage, Now));
        Assert.Throws<ArgumentException>(() =>
            EffectEngine.Apply(actor, actor.ActorId, invalidApply, Now));
    }

    private static EffectDefinition DoomEffect(string id, decimal damage) =>
        new(
            id,
            EffectKind.Debuff,
            TimeSpan.FromSeconds(2),
            1,
            EffectStackPolicy.Replace,
            0,
            DispelCategory: "Curse",
            OnExpireActions:
            [
                new EffectExpirationActionDefinition(
                    EffectExpirationActionType.Damage,
                    Amount: damage,
                    DamageType: DamageType.True)
            ]);

    private static TestCombat CreateSession(
        IReadOnlyList<AbilityDefinition> playerAbilities,
        bool companion)
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
        CombatActorState enemyActor = new(EnemyId, 500, 500, 0, 0, stats);
        CombatActorState? companionActor = companion
            ? new CombatActorState(CompanionId, 200, 200, 0, 0, stats)
            : null;
        CombatParticipantDefinition player = new(
            playerActor,
            CombatActorKind.Player,
            "TEST_PLAYER",
            "Test Player",
            "MANA",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            playerAbilities.Select(item => item.Id).ToHashSet(StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition enemy = new(
            enemyActor,
            CombatActorKind.Monster,
            "TEST_DOOM_CASTER",
            "Doom Caster",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal));
        CombatParticipantDefinition? companionDefinition = companionActor is null
            ? null
            : new CombatParticipantDefinition(
                companionActor,
                CombatActorKind.Companion,
                "TEST_COMPANION",
                "Test Companion",
                "NONE",
                new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
                new HashSet<string>(StringComparer.Ordinal),
                CanAutoAttack: false);
        Dictionary<string, AbilityDefinition> abilities = playerAbilities
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        CombatSession session = new(
            Guid.Parse("c0000000-0000-0000-0000-000000000001"),
            player,
            enemy,
            abilities,
            new MonsterAiProfile("PASSIVE", []),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.5m, 100).ToArray()),
            Now,
            companion: companionDefinition);
        return new TestCombat(session, playerActor, enemyActor, companionActor);
    }

    private sealed record TestCombat(
        CombatSession Session,
        CombatActorState Player,
        CombatActorState Enemy,
        CombatActorState? Companion);
}
