using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class MirrorBarrierCombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid BossId =
        Guid.Parse("92000000-0000-0000-0000-000000000001");

    [Fact]
    public void ThresholdSpawnsThreeLinkedAddsAndBarrierReflectsSeventyPercent()
    {
        CombatSession session = CreateSession();

        CombatCommandResult threshold = session.Handle(
            new UseAbilityCommand("threshold-hit", "THRESHOLD_HIT", BossId),
            Now);

        Assert.True(threshold.Succeeded, threshold.ErrorCode);
        Assert.Equal(4, threshold.Snapshot.Enemies!.Count);
        Assert.Equal(3, threshold.Events.Count(item =>
            item.Type == CombatEventType.ActorSummoned));
        Assert.DoesNotContain(threshold.Events, item =>
            string.Equals(
                item.DefinitionId,
                "MIRROR_BARRIER_REFLECTION",
                StringComparison.Ordinal));
        CombatActorSnapshot boss = Enemy(threshold.Snapshot, "MIRROR_BOSS");
        Assert.Equal(700, boss.Hp);
        Assert.Contains(boss.Effects, effect => effect.Id == "MIRROR_BARRIER_70");

        CombatCommandResult reflected = session.Handle(
            new UseAbilityCommand("barrier-hit", "PING", BossId),
            Now.AddMilliseconds(1));

        Assert.True(reflected.Succeeded, reflected.ErrorCode);
        Assert.Equal(1_930, reflected.Snapshot.Player.Hp);
        CombatEvent reflection = Assert.Single(reflected.Events, item =>
            item.Type == CombatEventType.DamageDealt
            && item.DefinitionId == "MIRROR_BARRIER_REFLECTION");
        Assert.Equal(70, reflection.Amount);
        Assert.Equal(BossId, reflection.SourceActorId);
        Assert.Equal(PlayerId, reflection.TargetActorId);
    }

    [Fact]
    public void GuardianDeathDropsReflectionAndLastAddOpensBrokenMirrorWindow()
    {
        CombatSession session = CreateSession();
        CombatCommandResult threshold = session.Handle(
            new UseAbilityCommand("threshold-hit", "THRESHOLD_HIT", BossId),
            Now);
        Assert.True(threshold.Succeeded, threshold.ErrorCode);

        Guid guardianId = Enemy(threshold.Snapshot, "MIRROR_GUARDIAN").ActorId;
        CombatCommandResult guardianKill = session.Handle(
            new UseAbilityCommand("kill-guardian", "THRESHOLD_HIT", guardianId),
            Now.AddMilliseconds(1));
        Assert.True(guardianKill.Succeeded, guardianKill.ErrorCode);
        CombatActorSnapshot bossAfterGuardian = Enemy(guardianKill.Snapshot, "MIRROR_BOSS");
        Assert.Contains(bossAfterGuardian.Effects, effect => effect.Id == "MIRROR_BARRIER_50");
        Assert.DoesNotContain(bossAfterGuardian.Effects, effect => effect.Id == "MIRROR_BARRIER_70");

        CombatCommandResult reflected = session.Handle(
            new UseAbilityCommand("fifty-reflection", "PING", BossId),
            Now.AddMilliseconds(2));
        Assert.True(reflected.Succeeded, reflected.ErrorCode);
        CombatEvent reflection = Assert.Single(reflected.Events, item =>
            item.Type == CombatEventType.DamageDealt
            && item.DefinitionId == "MIRROR_BARRIER_REFLECTION");
        Assert.Equal(50, reflection.Amount);

        Guid priestId = Enemy(reflected.Snapshot, "MIRROR_PRIEST").ActorId;
        CombatCommandResult priestKill = session.Handle(
            new UseAbilityCommand("kill-priest", "THRESHOLD_HIT", priestId),
            Now.AddMilliseconds(3));
        Assert.True(priestKill.Succeeded, priestKill.ErrorCode);
        Guid executionerId = Enemy(priestKill.Snapshot, "MIRROR_EXECUTIONER").ActorId;
        CombatCommandResult executionerKill = session.Handle(
            new UseAbilityCommand("kill-executioner", "THRESHOLD_HIT", executionerId),
            Now.AddMilliseconds(4));
        Assert.True(executionerKill.Succeeded, executionerKill.ErrorCode);

        CombatActorSnapshot brokenBoss = Enemy(executionerKill.Snapshot, "MIRROR_BOSS");
        Assert.Contains(brokenBoss.Effects, effect => effect.Id == "MIRROR_BROKEN");
        Assert.DoesNotContain(brokenBoss.Effects, effect => effect.Id == "MIRROR_BARRIER_50");
        Assert.DoesNotContain(brokenBoss.Effects, effect => effect.Id == "MIRROR_BARRIER_70");

        decimal hpBeforeBurstHit = brokenBoss.Hp;
        CombatCommandResult burst = session.Handle(
            new UseAbilityCommand("broken-mirror-hit", "PING", BossId),
            Now.AddMilliseconds(5));
        Assert.True(burst.Succeeded, burst.ErrorCode);
        CombatEvent burstDamage = Assert.Single(burst.Events, item =>
            item.Type == CombatEventType.DamageDealt
            && item.DefinitionId == "PING"
            && item.TargetActorId == BossId);
        Assert.Equal(125, burstDamage.Amount);
        Assert.Equal(hpBeforeBurstHit - 125, Enemy(burst.Snapshot, "MIRROR_BOSS").Hp);
        Assert.DoesNotContain(burst.Events, item =>
            item.DefinitionId == "MIRROR_BARRIER_REFLECTION");
    }

    [Fact]
    public void LethalHitsCannotBypassUnresolvedMirrorWave()
    {
        CombatSession session = CreateSession();

        CombatCommandResult firstNuke = session.Handle(
            new UseAbilityCommand("first-nuke", "NUKE", BossId),
            Now);

        Assert.True(firstNuke.Succeeded, firstNuke.ErrorCode);
        Assert.Equal(CombatSessionStatus.Active, firstNuke.Snapshot.Status);
        Assert.Equal(1, Enemy(firstNuke.Snapshot, "MIRROR_BOSS").Hp);
        Assert.Equal(4, firstNuke.Snapshot.Enemies!.Count);
        Assert.DoesNotContain(firstNuke.Events, item =>
            item.Type == CombatEventType.ActorDied
            && item.ActorId == BossId);
        Assert.Contains(
            Enemy(firstNuke.Snapshot, "MIRROR_BOSS").Effects,
            effect => effect.Id == "MIRROR_PHASE_GUARD");

        CombatCommandResult secondNuke = session.Handle(
            new UseAbilityCommand("second-nuke", "NUKE", BossId),
            Now.AddMilliseconds(1));

        Assert.True(secondNuke.Succeeded, secondNuke.ErrorCode);
        Assert.Equal(CombatSessionStatus.Active, secondNuke.Snapshot.Status);
        Assert.Equal(1, Enemy(secondNuke.Snapshot, "MIRROR_BOSS").Hp);
        Assert.DoesNotContain(secondNuke.Events, item =>
            item.Type == CombatEventType.ActorDied
            && item.ActorId == BossId);
        Assert.Contains(
            Enemy(secondNuke.Snapshot, "MIRROR_BOSS").Effects,
            effect => effect.Id == "MIRROR_PHASE_GUARD");
    }

    private static CombatSession CreateSession()
    {
        CombatStats playerStats = new(
            Level: 20,
            Accuracy: 100,
            Dodge: 0,
            CriticalChance: 0,
            CriticalDamage: 1,
            Armor: 0,
            MagicResistance: 0,
            ArmorPenetration: 0,
            MagicPenetration: 0);
        CombatStats bossStats = playerStats with { Accuracy = 0 };
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 2_000, 2_000, 100, 100, playerStats),
            CombatActorKind.Player,
            "WARRIOR",
            "Warrior",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(
                ["THRESHOLD_HIT", "PING", "NUKE"],
                StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition boss = new(
            new CombatActorState(BossId, 1_000, 1_000, 0, 0, bossStats),
            CombatActorKind.Monster,
            "MIRROR_BOSS",
            "Зеркальный Кастелян",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
            MonsterRank: MonsterRank.Boss);

        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            ["THRESHOLD_HIT"] = DamageAbility("THRESHOLD_HIT", 300),
            ["PING"] = DamageAbility("PING", 100),
            ["NUKE"] = DamageAbility("NUKE", 5_000)
        };
        CombatSession session = new(
            Guid.Parse("90000000-0000-0000-0000-000000000001"),
            player,
            boss,
            abilities,
            new MonsterAiProfile("MIRROR_BOSS_PASSIVE", []),
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 200).ToArray()),
            Now);
        session.ConfigureMirrorEncounter(CreateMirrorProfile(bossStats));
        return session;
    }

    private static MirrorCombatEncounterProfile CreateMirrorProfile(CombatStats stats) =>
        new(
        [
            AddProfile(MirrorEncounterAddRole.Guardian, "MIRROR_GUARDIAN", stats),
            AddProfile(MirrorEncounterAddRole.Priest, "MIRROR_PRIEST", stats),
            AddProfile(MirrorEncounterAddRole.Executioner, "MIRROR_EXECUTIONER", stats)
        ]);

    private static MirrorEncounterAddProfile AddProfile(
        MirrorEncounterAddRole role,
        string id,
        CombatStats stats)
    {
        MonsterDefinition monster = new(
            id,
            id,
            MonsterRank.Elite,
            20,
            100,
            stats,
            TimeSpan.FromHours(1),
            0,
            [],
            $"{id}_AI");
        return new MirrorEncounterAddProfile(
            role,
            monster,
            new MonsterAiProfile($"{id}_AI", []));
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

    private static CombatActorSnapshot Enemy(
        CombatSessionSnapshot snapshot,
        string definitionId) =>
        snapshot.Enemies!.Single(enemy =>
            string.Equals(enemy.DefinitionId, definitionId, StringComparison.Ordinal));
}
