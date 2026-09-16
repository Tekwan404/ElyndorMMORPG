using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class ShatteredOrderCombatSessionTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid BossId =
        Guid.Parse("a2000000-0000-0000-0000-000000000001");

    [Fact]
    public void VelariusShowsManaInterruptKeepsSpentCostAndTenPercentConvertsRemainderToShield()
    {
        Dictionary<string, AbilityDefinition> abilities = CommonPlayerAbilities();
        abilities["VELARIUS_TEST_CAST"] = new AbilityDefinition(
            "VELARIUS_TEST_CAST",
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            5,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(5),
            false,
            GlobalCooldownCategory.None,
            true,
            "SHADOW",
            Interruptible: true,
            Actions: []);
        abilities["VELARIUS_MANA_FEED"] = new AbilityDefinition(
            "VELARIUS_MANA_FEED",
            AbilityType.Casted,
            AbilityTargetType.Self,
            0,
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(2),
            false,
            GlobalCooldownCategory.None,
            true,
            "ARCANE",
            Interruptible: true,
            Actions: []);

        CombatParticipantDefinition player = CreatePlayer(abilities.Keys);
        CombatParticipantDefinition boss = CreateBoss(
            "VELARIUS_BOSS",
            1_000,
            ["VELARIUS_TEST_CAST"],
            TimeSpan.FromMilliseconds(100));
        CombatSession session = CreateSession(
            player,
            boss,
            abilities,
            new MonsterAiProfile("VELARIUS_AI", ["VELARIUS_TEST_CAST"]));
        session.ConfigureVelariusEncounter(new VelariusCombatEncounterProfile(
            Profile("VELARIUS_FEEDER", ["VELARIUS_MANA_FEED"], abilities)));

        CombatActorSnapshot initialBoss = Enemy(session.Snapshot(), "VELARIUS_BOSS");
        Assert.Equal("MANA", initialBoss.ResourceType);
        Assert.Equal(100, initialBoss.Resource);
        Assert.Equal(100, initialBoss.MaxResource);

        CombatCommandResult started = session.AdvanceTo(Now.AddMilliseconds(100));
        CombatActorSnapshot castingBoss = Enemy(started.Snapshot, "VELARIUS_BOSS");
        Assert.Equal(95, castingBoss.Resource);
        Assert.NotNull(castingBoss.ActiveCast);

        CombatCommandResult interrupted = session.Handle(
            new UseAbilityCommand("interrupt", "TEST_INTERRUPT", BossId),
            Now.AddMilliseconds(200));
        Assert.True(interrupted.Succeeded, interrupted.ErrorCode);
        Assert.Equal(95, Enemy(interrupted.Snapshot, "VELARIUS_BOSS").Resource);
        Assert.Null(Enemy(interrupted.Snapshot, "VELARIUS_BOSS").ActiveCast);

        CombatCommandResult finalPhase = session.Handle(
            new UseAbilityCommand("threshold", "HIT_900", BossId),
            Now.AddMilliseconds(300));
        CombatActorSnapshot barrierBoss = Enemy(finalPhase.Snapshot, "VELARIUS_BOSS");
        Assert.Equal(100, barrierBoss.Hp);
        Assert.Equal(0, barrierBoss.Resource);
        Assert.Contains(barrierBoss.Effects, effect => effect.Id == "VELARIUS_LAST_BARRIER");
        Assert.DoesNotContain(barrierBoss.Effects, effect => effect.Id == "VELARIUS_PHASE_GUARD");

        CombatCommandResult shieldHit = session.Handle(
            new UseAbilityCommand("shield-hit", "PING", BossId),
            Now.AddMilliseconds(400));
        Assert.Equal(100, Enemy(shieldHit.Snapshot, "VELARIUS_BOSS").Hp);
        Assert.Contains(shieldHit.Events, item =>
            item.Type == CombatEventType.ShieldAbsorbed
            && item.DefinitionId == "VELARIUS_LAST_BARRIER");
    }

    [Fact]
    public void MorEtSoulSuccessRewardsOwnerAndTimeoutHealsAndEmpowersBoss()
    {
        Dictionary<string, AbilityDefinition> abilities = CommonPlayerAbilities();
        CombatParticipantDefinition player = CreatePlayer(abilities.Keys);
        CombatParticipantDefinition boss = CreateBoss("MOR_ET_BOSS", 1_000, [], TimeSpan.FromHours(1));
        CombatSession session = CreateSession(
            player,
            boss,
            abilities,
            new MonsterAiProfile("MOR_ET_AI", []));
        EncounterEnemyProfile soulProfile = Profile("MOR_ET_SOUL_WARRIOR", [], abilities);
        session.ConfigureMorEtEncounter(new MorEtCombatEncounterProfile(
            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal)
            {
                ["WARRIOR"] = soulProfile,
                ["MAGE"] = soulProfile,
                ["ARCHER"] = soulProfile,
                ["PALADIN"] = soulProfile
            }));

        CombatCommandResult firstWave = session.Handle(
            new UseAbilityCommand("first-wave", "HIT_350", BossId),
            Now);
        CombatActorSnapshot firstSoul = firstWave.Snapshot.Enemies!.Single(enemy =>
            enemy.DefinitionId == "MOR_ET_SOUL_WARRIOR" && enemy.Hp > 0);
        Assert.Contains(firstWave.Snapshot.Player.Effects, effect => effect.Id == "MOR_ET_EMPTIED_DAMAGE");
        Assert.Contains(firstWave.Snapshot.Player.Effects, effect => effect.Id == "MOR_ET_EMPTIED_DOT");

        CombatCommandResult soulKilled = session.Handle(
            new UseAbilityCommand("kill-soul", "NUKE", firstSoul.ActorId),
            Now.AddMilliseconds(1));
        Assert.Contains(soulKilled.Snapshot.Player.Effects, effect => effect.Id == "MOR_ET_RETURNED_SOUL");
        Assert.DoesNotContain(soulKilled.Snapshot.Player.Effects, effect => effect.Id == "MOR_ET_EMPTIED_DAMAGE");

        CombatCommandResult secondWave = session.Handle(
            new UseAbilityCommand("second-wave", "HIT_350", BossId),
            Now.AddMilliseconds(2));
        decimal beforeTimeoutHp = Enemy(secondWave.Snapshot, "MOR_ET_BOSS").Hp;
        CombatActorSnapshot secondSoul = secondWave.Snapshot.Enemies!.Single(enemy =>
            enemy.DefinitionId == "MOR_ET_SOUL_WARRIOR" && enemy.Hp > 0);

        CombatCommandResult timeout = session.AdvanceTo(Now.AddMilliseconds(2).AddSeconds(18));
        Assert.True(Enemy(timeout.Snapshot, "MOR_ET_BOSS").Hp > beforeTimeoutHp);
        Assert.Contains(Enemy(timeout.Snapshot, "MOR_ET_BOSS").Effects, effect => effect.Id == "MOR_ET_FAILURE_STACK");
        Assert.True(timeout.Snapshot.Enemies!.Single(enemy => enemy.ActorId == secondSoul.ActorId).Hp <= 0);
        Assert.DoesNotContain(timeout.Snapshot.Player.Effects, effect => effect.Id == "MOR_ET_EMPTIED_DAMAGE");
        Assert.DoesNotContain(Enemy(timeout.Snapshot, "MOR_ET_BOSS").Effects, effect => effect.Id == "MOR_ET_PHASE_GUARD");
    }

    [Fact]
    public void AzraelReviveWindowRestoresFailedCloneAndSynchronizedKillsEndSplit()
    {
        Dictionary<string, AbilityDefinition> abilities = CommonPlayerAbilities();
        CombatParticipantDefinition player = CreatePlayer(abilities.Keys);
        CombatParticipantDefinition boss = CreateBoss("AZRAEL_BOSS", 1_000, [], TimeSpan.FromHours(1));
        CombatSession session = CreateSession(
            player,
            boss,
            abilities,
            new MonsterAiProfile("AZRAEL_AI", []));
        session.ConfigureAzraelEncounter(new AzraelCombatEncounterProfile(
            new Dictionary<AzraelCloneRole, EncounterEnemyProfile>
            {
                [AzraelCloneRole.Fire] = Profile("AZRAEL_FIRE", [], abilities, 100),
                [AzraelCloneRole.Frost] = Profile("AZRAEL_FROST", [], abilities, 100),
                [AzraelCloneRole.Void] = Profile("AZRAEL_VOID", [], abilities, 100)
            }));

        CombatCommandResult split = session.Handle(
            new UseAbilityCommand("split", "HIT_350", BossId),
            Now);
        Assert.Equal(4, split.Snapshot.Enemies!.Count);
        Assert.Contains(Enemy(split.Snapshot, "AZRAEL_BOSS").Effects, effect => effect.Id == "AZRAEL_SPLIT_IMMUNITY");

        Guid fireId = Enemy(split.Snapshot, "AZRAEL_FIRE").ActorId;
        CombatCommandResult fireKilled = session.Handle(
            new UseAbilityCommand("fire-kill", "PING", fireId),
            Now.AddMilliseconds(1));
        Assert.True(Enemy(fireKilled.Snapshot, "AZRAEL_FIRE").Hp <= 0);
        Assert.Contains(Enemy(fireKilled.Snapshot, "AZRAEL_BOSS").Effects, effect => effect.Id == "AZRAEL_REVIVE_WINDOW");

        CombatCommandResult revived = session.AdvanceTo(Now.AddSeconds(10).AddMilliseconds(1));
        Assert.True(Enemy(revived.Snapshot, "AZRAEL_FIRE").Hp > 0);
        Assert.Equal(35, Enemy(revived.Snapshot, "AZRAEL_FIRE").Hp);
        Assert.Contains(Enemy(revived.Snapshot, "AZRAEL_BOSS").Effects, effect => effect.Id == "AZRAEL_REVIVAL_POWER");

        Guid frostId = Enemy(revived.Snapshot, "AZRAEL_FROST").ActorId;
        Guid voidId = Enemy(revived.Snapshot, "AZRAEL_VOID").ActorId;
        CombatCommandResult killFireAgain = session.Handle(
            new UseAbilityCommand("fire-kill-2", "PING", fireId),
            Now.AddSeconds(10).AddMilliseconds(2));
        Assert.True(killFireAgain.Succeeded, killFireAgain.ErrorCode);
        CombatCommandResult killFrost = session.Handle(
            new UseAbilityCommand("frost-kill", "PING", frostId),
            Now.AddSeconds(10).AddMilliseconds(3));
        Assert.True(killFrost.Succeeded, killFrost.ErrorCode);
        CombatCommandResult killVoid = session.Handle(
            new UseAbilityCommand("void-kill", "PING", voidId),
            Now.AddSeconds(10).AddMilliseconds(4));
        Assert.True(killVoid.Succeeded, killVoid.ErrorCode);

        CombatActorSnapshot finalBoss = Enemy(killVoid.Snapshot, "AZRAEL_BOSS");
        Assert.Contains(finalBoss.Effects, effect => effect.Id == "AZRAEL_FINAL_PHASE");
        Assert.DoesNotContain(finalBoss.Effects, effect => effect.Id == "AZRAEL_SPLIT_IMMUNITY");
        Assert.DoesNotContain(finalBoss.Effects, effect => effect.Id == "AZRAEL_PHASE_GUARD");
    }

    private static CombatSession CreateSession(
        CombatParticipantDefinition player,
        CombatParticipantDefinition boss,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        MonsterAiProfile bossAi) =>
        new(
            Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            player,
            boss,
            abilities,
            bossAi,
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 2_000).ToArray()),
            Now);

    private static CombatParticipantDefinition CreatePlayer(IEnumerable<string> abilityIds)
    {
        CombatStats stats = Stats() with { Accuracy = 100 };
        return new CombatParticipantDefinition(
            new CombatActorState(PlayerId, 2_000, 2_000, 100, 100, stats),
            CombatActorKind.Player,
            "WARRIOR",
            "Warrior",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            abilityIds.ToHashSet(StringComparer.Ordinal),
            CanAutoAttack: false);
    }

    private static CombatParticipantDefinition CreateBoss(
        string definitionId,
        decimal maxHp,
        IReadOnlyList<string> abilityIds,
        TimeSpan autoAttackInterval) =>
        new(
            new CombatActorState(BossId, maxHp, maxHp, 0, 0, Stats() with { Accuracy = 0 }),
            CombatActorKind.Monster,
            definitionId,
            definitionId,
            "NONE",
            new AutoAttackProfile(autoAttackInterval, 0, 0, 0),
            abilityIds.ToHashSet(StringComparer.Ordinal),
            MonsterRank: MonsterRank.Boss);

    private static EncounterEnemyProfile Profile(
        string definitionId,
        IReadOnlyList<string> abilityIds,
        Dictionary<string, AbilityDefinition> abilities,
        decimal hp = 100)
    {
        foreach (string abilityId in abilityIds)
            Assert.True(abilities.ContainsKey(abilityId));
        MonsterDefinition monster = new(
            definitionId,
            definitionId,
            MonsterRank.Elite,
            25,
            hp,
            Stats() with { Accuracy = 0 },
            TimeSpan.FromHours(1),
            0,
            abilityIds,
            $"{definitionId}_AI");
        return new EncounterEnemyProfile(
            monster,
            new MonsterAiProfile($"{definitionId}_AI", abilityIds));
    }

    private static Dictionary<string, AbilityDefinition> CommonPlayerAbilities() =>
        new(StringComparer.Ordinal)
        {
            ["HIT_350"] = DamageAbility("HIT_350", 350),
            ["HIT_900"] = DamageAbility("HIT_900", 900),
            ["PING"] = DamageAbility("PING", 100),
            ["NUKE"] = DamageAbility("NUKE", 5_000),
            ["TEST_INTERRUPT"] = new AbilityDefinition(
                "TEST_INTERRUPT",
                AbilityType.Instant,
                AbilityTargetType.SingleEnemy,
                0,
                TimeSpan.Zero,
                TimeSpan.Zero,
                false,
                GlobalCooldownCategory.None,
                false,
                "PHYSICAL",
                Actions: [],
                RuntimeParameters: new Dictionary<string, decimal>(StringComparer.Ordinal)
                {
                    [CombatSession.InterruptTargetCastParameter] = 1,
                    [CombatSession.InterruptLockoutSecondsParameter] = 2
                })
        };

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

    private static CombatActorSnapshot Enemy(CombatSessionSnapshot snapshot, string definitionId) =>
        snapshot.Enemies!.Single(enemy => string.Equals(
            enemy.DefinitionId,
            definitionId,
            StringComparison.Ordinal));
}
