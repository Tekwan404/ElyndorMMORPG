using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatSessionMonsterInterruptTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 8, 15, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("92000000-0000-0000-0000-000000000001");

    [Fact]
    public void MonsterInterruptRuleStopsAuthoritativePlayerCast()
    {
        AbilityDefinition playerCast = new(
            "PLAYER_ARCANE_CAST",
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(4),
            false,
            GlobalCooldownCategory.None,
            true,
            "ARCANE",
            Interruptible: true,
            Actions: []);
        AbilityDefinition monsterInterrupt = new(
            "MONSTER_SPELL_LOCK",
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
                    AbilityActionType.Interrupt,
                    InterruptLockout: TimeSpan.FromSeconds(2))
            ],
            TargetSelectorProfile: AbilityTargetSelectorProfile.CastInProgressEnemy);
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            [playerCast.Id] = playerCast,
            [monsterInterrupt.Id] = monsterInterrupt
        };

        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 1_000, 1_000, 100, 100, Stats()),
            CombatActorKind.Player,
            "MAGE",
            "Mage",
            "MANA",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>([playerCast.Id], StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition enemy = new(
            new CombatActorState(EnemyId, 2_000, 2_000, 0, 0, Stats()),
            CombatActorKind.Monster,
            "SPELLBREAKER",
            "Spellbreaker",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromMilliseconds(100), 0, 0, 0),
            new HashSet<string>([monsterInterrupt.Id], StringComparer.Ordinal));
        MonsterAiProfile ai = new(
            "SPELLBREAKER_AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(
                    monsterInterrupt.Id,
                    Priority: 100,
                    TargetSelector: AbilityTargetSelectorProfile.CastInProgressEnemy)
            ]);
        CombatSession session = new(
            Guid.Parse("90000000-0000-0000-0000-000000000001"),
            player,
            enemy,
            abilities,
            ai,
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.5m, 64).ToArray()),
            Now);

        CombatCommandResult started = session.Handle(
            new UseAbilityCommand("player-cast", playerCast.Id, EnemyId),
            Now);
        CombatCommandResult advanced = session.AdvanceTo(Now.AddMilliseconds(100));

        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.Contains(advanced.Events, combatEvent =>
            combatEvent.Type == CombatEventType.AbilityUsed
            && combatEvent.DefinitionId == monsterInterrupt.Id
            && combatEvent.SourceActorId == EnemyId
            && combatEvent.TargetActorId == PlayerId);
        Assert.Contains(advanced.Events, combatEvent =>
            combatEvent.Type == CombatEventType.AbilityInterrupted
            && combatEvent.ActorId == PlayerId
            && combatEvent.DefinitionId == playerCast.Id
            && combatEvent.SourceActorId == EnemyId
            && combatEvent.TargetActorId == PlayerId);

        CombatCommandResult locked = session.Handle(
            new UseAbilityCommand("player-retry", playerCast.Id, EnemyId),
            Now.AddMilliseconds(200));
        Assert.False(locked.Succeeded);
    }

    [Fact]
    public void MonsterInterruptRuleFallsBackWhenPlayerIsNotCasting()
    {
        AbilityDefinition monsterInterrupt = new(
            "MONSTER_SPELL_LOCK",
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
                    AbilityActionType.Interrupt,
                    InterruptLockout: TimeSpan.FromSeconds(2))
            ],
            TargetSelectorProfile: AbilityTargetSelectorProfile.CastInProgressEnemy);
        AbilityDefinition fallback = new(
            "MONSTER_FALLBACK",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL",
            Actions: []);
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            [monsterInterrupt.Id] = monsterInterrupt,
            [fallback.Id] = fallback
        };

        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 1_000, 1_000, 100, 100, Stats()),
            CombatActorKind.Player,
            "MAGE",
            "Mage",
            "MANA",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition enemy = new(
            new CombatActorState(EnemyId, 2_000, 2_000, 0, 0, Stats()),
            CombatActorKind.Monster,
            "SPELLBREAKER",
            "Spellbreaker",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromMilliseconds(100), 0, 0, 0),
            new HashSet<string>([monsterInterrupt.Id, fallback.Id], StringComparer.Ordinal));
        MonsterAiProfile ai = new(
            "SPELLBREAKER_AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(
                    monsterInterrupt.Id,
                    Priority: 100,
                    TargetSelector: AbilityTargetSelectorProfile.CastInProgressEnemy),
                new MonsterAbilityRule(fallback.Id, Priority: 10)
            ]);
        CombatSession session = new(
            Guid.Parse("90000000-0000-0000-0000-000000000002"),
            player,
            enemy,
            abilities,
            ai,
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.5m, 64).ToArray()),
            Now);

        CombatCommandResult result = session.AdvanceTo(Now.AddMilliseconds(100));

        CombatEvent used = Assert.Single(
            result.Events,
            combatEvent => combatEvent.Type == CombatEventType.AbilityUsed);
        Assert.Equal(fallback.Id, used.DefinitionId);
    }

    private static CombatStats Stats() =>
        new(
            Level: 25,
            Accuracy: 100,
            Dodge: 0,
            CriticalChance: 0,
            CriticalDamage: 1,
            Armor: 0,
            MagicResistance: 0,
            ArmorPenetration: 0,
            MagicPenetration: 0,
            AttackPower: 0,
            SpellPower: 0);
}
