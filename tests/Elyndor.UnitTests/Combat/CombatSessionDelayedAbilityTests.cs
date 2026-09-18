using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatSessionDelayedAbilityTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("a2000000-0000-0000-0000-000000000001");

    [Fact]
    public void MonsterDelayedHitBecomesAuthoritativeSchedulerDeadline()
    {
        AbilityDefinition bomb = new(
            "MONSTER_DELAYED_BOMB",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.FromMinutes(1),
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            true,
            "ARCANE",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    Amount: 13,
                    DamageType: DamageType.True,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false,
                    Delay: TimeSpan.FromSeconds(2))
            ]);
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            [bomb.Id] = bomb
        };
        MonsterAiProfile ai = new(
            "DELAYED_BOMB_AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(
                    bomb.Id,
                    Priority: 100,
                    OncePerCombat: true)
            ]);
        CombatSession session = CreateSession(abilities, ai, bomb.Id);

        session.AdvanceTo(Now.AddSeconds(10));

        Assert.Contains(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.AbilityUsed
            && item.SourceActorId == EnemyId
            && item.DefinitionId == bomb.Id
            && item.OccurredAtUtc == Now.AddSeconds(10));
        Assert.DoesNotContain(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.DamageDealt
            && item.DefinitionId == bomb.Id);
        Assert.Equal(500, session.Snapshot().Player.Hp);
        Assert.Equal(Now.AddSeconds(12), session.NextDueAtUtc);

        session.AdvanceTo(Now.AddSeconds(11));
        Assert.Equal(500, session.Snapshot().Player.Hp);

        session.AdvanceTo(Now.AddSeconds(12));

        Assert.Equal(487, session.Snapshot().Player.Hp);
        CombatEvent damage = Assert.Single(
            session.GetEventsAfter(0),
            item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == bomb.Id);
        Assert.Equal(13, damage.Amount);
        Assert.Equal(EnemyId, damage.SourceActorId);
        Assert.Equal(PlayerId, damage.TargetActorId);
        Assert.Equal(Now.AddSeconds(12), damage.OccurredAtUtc);
    }

    private static CombatSession CreateSession(
        Dictionary<string, AbilityDefinition> abilities,
        MonsterAiProfile ai,
        string abilityId)
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
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 500, 500, 100, 100, stats),
            CombatActorKind.Player,
            "TEST_PLAYER",
            "Test Player",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition enemy = new(
            new CombatActorState(EnemyId, 500, 500, 0, 0, stats),
            CombatActorKind.Monster,
            "TEST_DELAYED_MONSTER",
            "Delayed Monster",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(10), 0, 0, 0),
            new HashSet<string>([abilityId], StringComparer.Ordinal));

        return new CombatSession(
            Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            player,
            enemy,
            abilities,
            ai,
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.5m, 100).ToArray()),
            Now);
    }
}
