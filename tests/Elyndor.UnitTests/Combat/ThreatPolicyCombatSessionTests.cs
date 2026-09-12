using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class ThreatPolicyCombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 12, 16, 45, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("81000000-0000-0000-0000-000000000001");
    private static readonly Guid CompanionId =
        Guid.Parse("82000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("83000000-0000-0000-0000-000000000001");

    [Fact]
    public void CriticalHitGeneratesThreatOnlyOnceFromDamageDealt()
    {
        CombatStats stats = CreateStats(criticalChance: 100);
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 1_000, 1_000, 100, 0, stats),
            CombatActorKind.Player,
            "ARCHER",
            "Critical Archer",
            "FOCUS",
            new AutoAttackProfile(
                TimeSpan.FromSeconds(10),
                BaseDamage: 100,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0),
            new HashSet<string>(StringComparer.Ordinal));
        CombatParticipantDefinition enemy = CreateEnemy(stats);
        CombatSession session = CreateSession(
            player,
            enemy,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal));

        session.AdvanceTo(Now.AddMilliseconds(1));

        CombatEvent damage = Assert.Single(
            session.GetEventsAfter(0),
            item => item.Type == CombatEventType.DamageDealt
                && item.SourceActorId == PlayerId
                && item.TargetActorId == EnemyId);
        Assert.Single(
            session.GetEventsAfter(0),
            item => item.Type == CombatEventType.CriticalHit
                && item.SourceActorId == PlayerId
                && item.TargetActorId == EnemyId);

        CombatThreatSnapshot snapshot = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddMilliseconds(1)));
        CombatThreatEntrySnapshot entry = Assert.Single(
            snapshot.Entries,
            item => item.ActorId == PlayerId);

        Assert.Equal(1m + damage.Amount, entry.Threat);
    }

    [Fact]
    public void TauntRaisesThreatToLeaderPlusOneWithoutCountingDurationAsThreat()
    {
        CombatStats stats = CreateStats();
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 1_000, 1_000, 100, 0, stats),
            CombatActorKind.Player,
            "WARRIOR",
            "Guardian",
            "RAGE",
            new AutoAttackProfile(
                TimeSpan.FromSeconds(10),
                BaseDamage: 0,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0),
            new HashSet<string>(["PROVOKE"], StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition companion = new(
            new CombatActorState(CompanionId, 1_000, 1_000, 0, 0, stats),
            CombatActorKind.Companion,
            "TEST_COMPANION",
            "Companion",
            "NONE",
            new AutoAttackProfile(
                TimeSpan.FromSeconds(1),
                BaseDamage: 100,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0),
            new HashSet<string>(StringComparer.Ordinal));
        CombatParticipantDefinition enemy = CreateEnemy(stats);
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["PROVOKE"] = new(
                "PROVOKE",
                AbilityType.Taunt,
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
                        AbilityActionType.Taunt,
                        Duration: TimeSpan.FromSeconds(3))
                ])
        };
        CombatSession session = CreateSession(
            player,
            enemy,
            abilities,
            companion);

        session.AdvanceTo(Now.AddSeconds(1.1));
        CombatThreatSnapshot before = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddSeconds(1.1)));
        decimal companionThreat = Assert.Single(
            before.Entries,
            item => item.ActorId == CompanionId).Threat;

        CombatCommandResult provoke = session.Handle(
            new UseAbilityCommand("provoke", "PROVOKE", EnemyId),
            Now.AddSeconds(1.2));
        CombatThreatSnapshot after = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddSeconds(1.2)));

        Assert.True(provoke.Succeeded);
        Assert.Equal(PlayerId, after.ForcedTargetActorId);
        Assert.Equal(
            companionThreat + 1m,
            Assert.Single(after.Entries, item => item.ActorId == PlayerId).Threat);
    }

    [Fact]
    public void EffectiveHealingGeneratesHalfThreatAgainstLivingEnemies()
    {
        CombatStats stats = CreateStats();
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 1_000, 500, 100, 0, stats),
            CombatActorKind.Player,
            "MAGE",
            "Healer",
            "MANA",
            new AutoAttackProfile(
                TimeSpan.FromSeconds(10),
                BaseDamage: 0,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0),
            new HashSet<string>(["HEAL"], StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition enemy = CreateEnemy(stats);
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["HEAL"] = new(
                "HEAL",
                AbilityType.Instant,
                AbilityTargetType.Self,
                0,
                TimeSpan.Zero,
                TimeSpan.Zero,
                false,
                GlobalCooldownCategory.None,
                true,
                "ARCANE",
                Actions:
                [
                    new AbilityActionDefinition(
                        AbilityActionType.Healing,
                        Amount: 100)
                ])
        };
        CombatSession session = CreateSession(
            player,
            enemy,
            abilities);

        CombatCommandResult heal = session.Handle(
            new UseAbilityCommand("heal", "HEAL", PlayerId),
            Now.AddMilliseconds(100));
        CombatThreatSnapshot snapshot = Assert.IsType<CombatThreatSnapshot>(
            session.GetThreatSnapshot(PlayerId, Now.AddMilliseconds(100)));

        Assert.True(heal.Succeeded);
        Assert.Equal(
            51m,
            Assert.Single(snapshot.Entries, item => item.ActorId == PlayerId).Threat);
    }

    private static CombatSession CreateSession(
        CombatParticipantDefinition player,
        CombatParticipantDefinition enemy,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        CombatParticipantDefinition? companion = null) =>
        new(
            Guid.CreateVersion7(),
            player,
            enemy,
            abilities,
            new MonsterAiProfile("THREAT_AI", []),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.5m, 100).ToArray()),
            Now,
            companion: companion);

    private static CombatParticipantDefinition CreateEnemy(CombatStats stats) =>
        new(
            new CombatActorState(EnemyId, 10_000, 10_000, 0, 0, stats),
            CombatActorKind.Monster,
            "THREAT_DUMMY",
            "Threat Dummy",
            "NONE",
            new AutoAttackProfile(
                TimeSpan.FromSeconds(100),
                BaseDamage: 10,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0),
            new HashSet<string>(StringComparer.Ordinal));

    private static CombatStats CreateStats(decimal criticalChance = 0) =>
        new(
            Level: 20,
            Accuracy: 100,
            Dodge: 0,
            CriticalChance: criticalChance,
            CriticalDamage: 1,
            Armor: 0,
            MagicResistance: 0,
            ArmorPenetration: 0,
            MagicPenetration: 0,
            AttackPower: 0,
            SpellPower: 0);
}
