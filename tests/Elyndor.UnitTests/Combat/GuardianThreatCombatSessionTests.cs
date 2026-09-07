using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class GuardianThreatCombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 7, 15, 20, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid CompanionId =
        Guid.Parse("72000000-0000-0000-0000-000000000001");
    private static readonly Guid EnemyId =
        Guid.Parse("73000000-0000-0000-0000-000000000001");

    [Fact]
    public void HeavyPresenceIncreasesOnlyAutoAttackThreatAndChangesEnemyTarget()
    {
        CombatSession baseline = CreateSession(ResolvedTalentModifiers.Empty);
        CombatSession guardian = CreateSession(
            ResolvedTalentModifiers.Empty with
            {
                EventHooks =
                [
                    new ResolvedTalentEventHook(
                        "G-1-4",
                        TalentModifierKeys.OnAutoAttack,
                        4,
                        15,
                        null,
                        TimeSpan.Zero,
                        false)
                ]
            });

        baseline.AdvanceTo(Now.AddSeconds(2));
        guardian.AdvanceTo(Now.AddSeconds(2));

        CombatEvent baselineEnemySwing = Assert.Single(
            baseline.GetEventsAfter(0),
            item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == "AUTO_ATTACK"
                && item.SourceActorId == EnemyId);
        CombatEvent guardianEnemySwing = Assert.Single(
            guardian.GetEventsAfter(0),
            item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == "AUTO_ATTACK"
                && item.SourceActorId == EnemyId);

        Assert.Equal(CompanionId, baselineEnemySwing.TargetActorId);
        Assert.Equal(PlayerId, guardianEnemySwing.TargetActorId);

        decimal baselinePlayerDamage = Assert.Single(
            baseline.GetEventsAfter(0),
            item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == "AUTO_ATTACK"
                && item.SourceActorId == PlayerId).Amount;
        decimal guardianPlayerDamage = Assert.Single(
            guardian.GetEventsAfter(0),
            item => item.Type == CombatEventType.DamageDealt
                && item.DefinitionId == "AUTO_ATTACK"
                && item.SourceActorId == PlayerId).Amount;

        Assert.Equal(baselinePlayerDamage, guardianPlayerDamage);
    }

    private static CombatSession CreateSession(ResolvedTalentModifiers talents)
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
            new CombatActorState(PlayerId, 1_000, 1_000, 100, 0, stats),
            CombatActorKind.Player,
            "WARRIOR",
            "Guardian",
            "RAGE",
            new AutoAttackProfile(
                TimeSpan.FromSeconds(10),
                BaseDamage: 100,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0),
            new HashSet<string>(StringComparer.Ordinal));

        CombatParticipantDefinition companion = new(
            new CombatActorState(CompanionId, 1_000, 1_000, 0, 0, stats),
            CombatActorKind.Companion,
            "TEST_COMPANION",
            "Companion",
            "NONE",
            new AutoAttackProfile(
                TimeSpan.FromSeconds(1.5),
                BaseDamage: 105,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0),
            new HashSet<string>(StringComparer.Ordinal));

        CombatParticipantDefinition enemy = new(
            new CombatActorState(EnemyId, 10_000, 10_000, 0, 0, stats),
            CombatActorKind.Monster,
            "THREAT_DUMMY",
            "Threat Dummy",
            "NONE",
            new AutoAttackProfile(
                TimeSpan.FromSeconds(2),
                BaseDamage: 10,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0),
            new HashSet<string>(StringComparer.Ordinal));

        return new CombatSession(
            Guid.CreateVersion7(),
            player,
            enemy,
            new Dictionary<string, Elyndor.Core.Combat.Abilities.AbilityDefinition>(
                StringComparer.Ordinal),
            new MonsterAiProfile("THREAT_AI", []),
            talents,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 100).ToArray()),
            Now,
            companion: companion);
    }
}
