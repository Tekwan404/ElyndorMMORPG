using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class PveThreatActionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 10, 30, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("92000000-0000-0000-0000-000000000001");

    [Fact]
    public void ThreatTableSupportsExplicitSingleAndWholeTableDrops()
    {
        ThreatTable table = new();
        Guid first = Guid.Parse("93000000-0000-0000-0000-000000000001");
        Guid second = Guid.Parse("93000000-0000-0000-0000-000000000002");

        table.AddExplicitThreat(first, 100);
        table.AddExplicitThreat(second, 50);

        Assert.Equal(50, table.DropThreatPercent(first, 50));
        Assert.Equal(50, table.GetThreat(first));
        Assert.Equal(50, table.GetThreat(second));

        table.DropAllThreatPercent(20);

        Assert.Equal(40, table.GetThreat(first));
        Assert.Equal(40, table.GetThreat(second));
    }

    [Fact]
    public void MonsterSelfThreatDropReducesWholeThreatTable()
    {
        AbilityDefinition drop = ThreatAbility(
            "VOID_TELEPORT_THREAT_DROP",
            AbilityTargetType.Self,
            new AbilityActionDefinition(
                AbilityActionType.DropThreatPercent,
                Amount: 50));
        CombatSession session = CreateSession(
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
            {
                [drop.Id] = drop
            },
            new MonsterAiProfile("DROP_AI", [drop.Id]),
            new HashSet<string>([drop.Id], StringComparer.Ordinal),
            enemyActionInterval: TimeSpan.FromMilliseconds(100));

        CombatThreatSnapshot before = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now));
        decimal initialThreat = Assert.Single(
            before.Entries,
            entry => entry.ActorId == PlayerId).Threat;

        session.AdvanceTo(Now.AddMilliseconds(100));

        CombatThreatSnapshot after = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddMilliseconds(100)));
        Assert.Equal(
            initialThreat * 0.5m,
            Assert.Single(after.Entries, entry => entry.ActorId == PlayerId).Threat);
        Assert.Contains(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.ThreatDropped
            && combatEvent.SourceActorId == EnemyId
            && combatEvent.TargetActorId == EnemyId
            && combatEvent.Amount == 50);
    }

    [Fact]
    public void PlayerSelfThreatDropAndClearDoNotSuppressNextRealDamageThreat()
    {
        AbilityDefinition strike = new(
            "THREAT_STRIKE",
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
                    Amount: 100,
                    DamageType: DamageType.True,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false)
            ]);
        AbilityDefinition drop = ThreatAbility(
            "PLAYER_THREAT_DROP",
            AbilityTargetType.Self,
            new AbilityActionDefinition(
                AbilityActionType.DropThreatPercent,
                Amount: 50));
        AbilityDefinition clear = ThreatAbility(
            "PLAYER_THREAT_CLEAR",
            AbilityTargetType.Self,
            new AbilityActionDefinition(AbilityActionType.ClearThreat));
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            [strike.Id] = strike,
            [drop.Id] = drop,
            [clear.Id] = clear
        };
        CombatSession session = CreateSession(
            abilities,
            new MonsterAiProfile("IDLE_AI", []),
            new HashSet<string>(StringComparer.Ordinal),
            playerAbilityIds: new HashSet<string>(abilities.Keys, StringComparer.Ordinal));

        Assert.True(session.Handle(
            new UseAbilityCommand("strike-1", strike.Id, EnemyId),
            Now.AddMilliseconds(10)).Succeeded);
        CombatThreatSnapshot afterStrike = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddMilliseconds(10)));
        decimal threatAfterStrike = Assert.Single(
            afterStrike.Entries,
            entry => entry.ActorId == PlayerId).Threat;
        Assert.True(threatAfterStrike > 1);

        Assert.True(session.Handle(
            new UseAbilityCommand("drop", drop.Id, PlayerId),
            Now.AddMilliseconds(20)).Succeeded);
        CombatThreatSnapshot afterDrop = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddMilliseconds(20)));
        decimal threatAfterDrop = Assert.Single(
            afterDrop.Entries,
            entry => entry.ActorId == PlayerId).Threat;
        Assert.Equal(threatAfterStrike * 0.5m, threatAfterDrop);

        Assert.True(session.Handle(
            new UseAbilityCommand("strike-2", strike.Id, EnemyId),
            Now.AddMilliseconds(30)).Succeeded);
        CombatThreatSnapshot afterSecondStrike = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddMilliseconds(30)));
        Assert.Equal(
            threatAfterDrop + 100m,
            Assert.Single(afterSecondStrike.Entries, entry => entry.ActorId == PlayerId).Threat);

        Assert.True(session.Handle(
            new UseAbilityCommand("clear", clear.Id, PlayerId),
            Now.AddMilliseconds(40)).Succeeded);
        CombatThreatSnapshot afterClear = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddMilliseconds(40)));
        Assert.DoesNotContain(afterClear.Entries, entry => entry.ActorId == PlayerId);
    }

    [Fact]
    public void MonsterFixateOverridesTargetOnlyForConfiguredDuration()
    {
        AbilityDefinition fixate = ThreatAbility(
            "MONSTER_FIXATE",
            AbilityTargetType.SingleEnemy,
            new AbilityActionDefinition(
                AbilityActionType.Fixate,
                Duration: TimeSpan.FromSeconds(1))) with
        {
            Cooldown = TimeSpan.FromSeconds(10)
        };
        CombatSession session = CreateSession(
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
            {
                [fixate.Id] = fixate
            },
            new MonsterAiProfile("FIXATE_AI", [fixate.Id]),
            new HashSet<string>([fixate.Id], StringComparer.Ordinal),
            enemyActionInterval: TimeSpan.FromMilliseconds(100));

        session.AdvanceTo(Now.AddMilliseconds(100));

        CombatThreatSnapshot active = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddMilliseconds(100)));
        Assert.Equal(PlayerId, active.ForcedTargetActorId);
        Assert.Contains(session.GetEventsAfter(0), combatEvent =>
            combatEvent.Type == CombatEventType.FixateApplied
            && combatEvent.SourceActorId == EnemyId
            && combatEvent.TargetActorId == PlayerId
            && combatEvent.Amount == 1);

        CombatThreatSnapshot expired = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddSeconds(1.2)));
        Assert.Null(expired.ForcedTargetActorId);
    }

    [Fact]
    public void InvalidThreatActionValuesAreRejected()
    {
        CombatActorState actor = CombatActorState.CreateDummy(100);
        CombatRuntimeState runtime = new(actor);
        AbilityDefinition invalid = ThreatAbility(
            "INVALID_DROP",
            AbilityTargetType.Self,
            new AbilityActionDefinition(
                AbilityActionType.DropThreatPercent,
                Amount: 101));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            AbilityEngine.Execute(
                runtime,
                invalid,
                new AbilityIntent("invalid-drop", invalid.Id, actor.ActorId),
                Now));

        Assert.Contains("Threat actions", exception.Message, StringComparison.Ordinal);
    }

    private static AbilityDefinition ThreatAbility(
        string id,
        AbilityTargetType targetType,
        AbilityActionDefinition action) =>
        new(
            id,
            AbilityType.Instant,
            targetType,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL",
            Actions: [action]);

    private static CombatSession CreateSession(
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        MonsterAiProfile ai,
        IReadOnlySet<string> enemyAbilityIds,
        TimeSpan? enemyActionInterval = null,
        IReadOnlySet<string>? playerAbilityIds = null)
    {
        CombatStats stats = new(
            Level: 20,
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
            new CombatActorState(PlayerId, 1_000, 1_000, 100, 100, stats),
            CombatActorKind.Player,
            "TEST_PLAYER",
            "Test Player",
            "MANA",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            playerAbilityIds ?? new HashSet<string>(StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition enemy = new(
            new CombatActorState(EnemyId, 5_000, 5_000, 0, 0, stats),
            CombatActorKind.Monster,
            "THREAT_MONSTER",
            "Threat Monster",
            "NONE",
            new AutoAttackProfile(
                enemyActionInterval ?? TimeSpan.FromHours(1),
                BaseDamage: 1,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0),
            enemyAbilityIds);

        return new CombatSession(
            Guid.Parse("90000000-0000-0000-0000-000000000001"),
            player,
            enemy,
            abilities,
            ai,
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.5m, 100).ToArray()),
            Now);
    }
}
