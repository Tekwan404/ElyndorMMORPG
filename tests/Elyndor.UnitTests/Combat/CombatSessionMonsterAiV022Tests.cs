using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatSessionMonsterAiV022Tests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 6, 30, 0, TimeSpan.Zero);
    private static readonly Guid SessionId =
        Guid.Parse("81000000-0000-0000-0000-000000000001");
    private static readonly Guid PlayerId =
        Guid.Parse("82000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("83000000-0000-0000-0000-000000000001");
    private static readonly Guid CompanionId =
        Guid.Parse("84000000-0000-0000-0000-000000000001");

    [Fact]
    public void RandomEnemyRuleSelectsAuthoritativeSessionTargetDeterministically()
    {
        AbilityDefinition bolt = DamageAbility("RANDOM_BOLT", 11);
        MonsterAiProfile ai = new(
            "RANDOM_TARGET_AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(
                    bolt.Id,
                    Priority: 100,
                    TargetSelector: AbilityTargetSelectorProfile.RandomEnemy)
            ]);
        CombatSession session = CreateSession(
            ai,
            Abilities(bolt),
            [bolt.Id],
            randomValues: Enumerable.Repeat(0.99m, 30).ToArray(),
            companion: Companion());

        session.AdvanceTo(Now.AddSeconds(1));

        CombatEvent damage = Assert.Single(
            session.GetEventsAfter(0),
            item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == bolt.Id);
        Assert.Equal(EnemyId, damage.SourceActorId);
        Assert.Equal(CompanionId, damage.TargetActorId);
    }

    [Fact]
    public void OncePerCombatRuleFallsBackOnNextEnemyAction()
    {
        AbilityDefinition opener = DamageAbility("ONCE_OPENER", 7);
        AbilityDefinition fallback = DamageAbility("FALLBACK_HIT", 3);
        MonsterAiProfile ai = new(
            "ONCE_AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(
                    opener.Id,
                    Priority: 100,
                    OncePerCombat: true),
                new MonsterAbilityRule(
                    fallback.Id,
                    Priority: 10)
            ]);
        CombatSession session = CreateSession(
            ai,
            Abilities(opener, fallback),
            [opener.Id, fallback.Id]);

        session.AdvanceTo(Now.AddSeconds(2));

        CombatEvent[] used = session.GetEventsAfter(0)
            .Where(item => item.Type == CombatEventType.AbilityUsed)
            .ToArray();
        Assert.Single(used, item => item.DefinitionId == opener.Id);
        Assert.Single(used, item => item.DefinitionId == fallback.Id);
        Assert.Equal(
            [opener.Id, fallback.Id],
            used.Select(item => item.DefinitionId));
    }

    [Fact]
    public void FailedHigherPriorityAbilityFallsThroughWithinSameEnemyAction()
    {
        AbilityDefinition expensive = DamageAbility(
            "EXPENSIVE_HIT",
            50,
            resourceCost: 10);
        AbilityDefinition fallback = DamageAbility("FREE_HIT", 5);
        MonsterAiProfile ai = new(
            "RESOURCE_FALLBACK_AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(expensive.Id, Priority: 100),
                new MonsterAbilityRule(fallback.Id, Priority: 10)
            ]);
        CombatSession session = CreateSession(
            ai,
            Abilities(expensive, fallback),
            [expensive.Id, fallback.Id],
            enemyMaxResource: 0,
            enemyCurrentResource: 0);

        session.AdvanceTo(Now.AddSeconds(1));

        CombatEvent used = Assert.Single(
            session.GetEventsAfter(0),
            item => item.Type == CombatEventType.AbilityUsed);
        Assert.Equal(fallback.Id, used.DefinitionId);
        Assert.DoesNotContain(
            session.GetEventsAfter(0),
            item => item.DefinitionId == expensive.Id);
    }

    private static CombatSession CreateSession(
        MonsterAiProfile ai,
        Dictionary<string, AbilityDefinition> abilities,
        IReadOnlySet<string> enemyAbilityIds,
        decimal enemyMaxResource = 0,
        decimal enemyCurrentResource = 0,
        decimal[]? randomValues = null,
        CombatParticipantDefinition? companion = null)
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
            MagicPenetration: 0,
            AttackPower: 0,
            SpellPower: 0);
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
            new CombatActorState(
                EnemyId,
                500,
                500,
                enemyMaxResource,
                enemyCurrentResource,
                stats),
            CombatActorKind.Monster,
            "TEST_MONSTER",
            "Test Monster",
            enemyMaxResource > 0 ? "MANA" : "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(1), 0, 0, 0),
            enemyAbilityIds);

        return new CombatSession(
            SessionId,
            player,
            enemy,
            abilities,
            ai,
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(
                randomValues ?? Enumerable.Repeat(0.5m, 50).ToArray()),
            Now,
            companion: companion);
    }

    private static CombatParticipantDefinition Companion() =>
        new(
            new CombatActorState(
                CompanionId,
                200,
                200,
                0,
                0,
                CombatStats.Default with { Level = 10 }),
            CombatActorKind.Companion,
            "TEST_COMPANION",
            "Test Companion",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
            CanAutoAttack: false);

    private static Dictionary<string, AbilityDefinition> Abilities(
        params AbilityDefinition[] abilities) =>
        abilities.ToDictionary(ability => ability.Id, StringComparer.Ordinal);

    private static AbilityDefinition DamageAbility(
        string id,
        decimal damage,
        decimal resourceCost = 0) =>
        new(
            id,
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            resourceCost,
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
                    Amount: damage,
                    DamageType: DamageType.True,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false)
            ]);
}
