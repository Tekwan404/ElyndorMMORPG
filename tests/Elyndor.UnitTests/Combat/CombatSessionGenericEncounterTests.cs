using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatSessionGenericEncounterTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 7, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid BossId =
        Guid.Parse("92000000-0000-0000-0000-000000000001");

    [Fact]
    public void CombatStartSummonUsesExactLifetimeAsSchedulerDeadline()
    {
        MonsterDefinition add = Monster(
            "TEST_LINKED_ADD",
            abilityIds: [],
            aiProfileId: "ADD_PASSIVE",
            autoAttackInterval: TimeSpan.FromHours(1));
        EncounterDefinition encounter = new(
            "TEST_SUMMON_ENCOUNTER",
            "TEST_BOSS",
            [
                new EncounterPhaseDefinition(
                    "OPEN",
                    new EncounterTriggerDefinition(EncounterTriggerType.CombatStart),
                    [
                        new EncounterActionDefinition(
                            EncounterActionType.Summon,
                            Summon: new SummonDefinition(
                                add.Id,
                                Count: 1,
                                MaxActive: 1,
                                Lifetime: TimeSpan.FromSeconds(5),
                                LinkToCaster: true))
                    ])
            ]);
        CombatSession session = Session(
            bossAbilityIds: new HashSet<string>(StringComparer.Ordinal),
            abilities: new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            bossAi: new MonsterAiProfile("BOSS_PASSIVE", []),
            bossAutoAttackInterval: TimeSpan.FromHours(1));

        session.ConfigureGenericEncounter(
            encounter,
            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal)
            {
                [add.Id] = new(add, new MonsterAiProfile("ADD_PASSIVE", []))
            },
            new Dictionary<string, Elyndor.Core.Combat.Effects.EffectDefinition>(StringComparer.Ordinal));

        CombatActorSnapshot summoned = Assert.Single(
            session.Snapshot().Enemies!,
            enemy => enemy.DefinitionId == add.Id);
        Assert.False(summoned.RewardEligible);
        Assert.Equal(Now.AddSeconds(5), session.NextDueAtUtc);

        session.AdvanceTo(Now.AddSeconds(5));

        Assert.Equal(0, session.Snapshot().Enemies!
            .Single(enemy => enemy.ActorId == summoned.ActorId).Hp);
        Assert.Contains(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.ActorDied
            && item.ActorId == summoned.ActorId
            && item.OccurredAtUtc == Now.AddSeconds(5));
    }

    [Fact]
    public void SummonCanStartAtConfiguredHealthPercentage()
    {
        MonsterDefinition add = Monster(
            "TEST_WOUNDED_ADD",
            abilityIds: [],
            aiProfileId: "ADD_PASSIVE",
            autoAttackInterval: TimeSpan.FromHours(1));
        EncounterDefinition encounter = new(
            "TEST_WOUNDED_SUMMON_ENCOUNTER",
            "TEST_BOSS",
            [
                new EncounterPhaseDefinition(
                    "OPEN",
                    new EncounterTriggerDefinition(EncounterTriggerType.CombatStart),
                    [
                        new EncounterActionDefinition(
                            EncounterActionType.Summon,
                            Summon: new SummonDefinition(
                                add.Id,
                                Count: 1,
                                MaxActive: 1,
                                InitialHpPercent: 60))
                    ])
            ]);
        CombatSession session = Session(
            bossAbilityIds: new HashSet<string>(StringComparer.Ordinal),
            abilities: new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            bossAi: new MonsterAiProfile("BOSS_PASSIVE", []),
            bossAutoAttackInterval: TimeSpan.FromHours(1));

        session.ConfigureGenericEncounter(
            encounter,
            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal)
            {
                [add.Id] = new(add, new MonsterAiProfile("ADD_PASSIVE", []))
            },
            new Dictionary<string, EffectDefinition>(StringComparer.Ordinal));

        CombatActorSnapshot summoned = Assert.Single(
            session.Snapshot().Enemies!,
            enemy => enemy.DefinitionId == add.Id);
        Assert.Equal(summoned.MaxHp * 0.6m, summoned.Hp);
    }

    [Fact]
    public void LinkedCombatObjectAppliesAuraUntilDestroyedAndIsRewardIneligible()
    {
        AbilityDefinition strike = DamageAbility("OBJECT_STRIKE", 50);
        EffectDefinition aura = new(
            "TEST_BANNER_AURA",
            EffectKind.StatModifier,
            TimeSpan.FromMinutes(10),
            1,
            EffectStackPolicy.Replace,
            25,
            ModifiedStat: EffectStat.Armor,
            ModifierMode: EffectModifierMode.Percent);
        MonsterDefinition banner = Monster(
            "TEST_BANNER",
            abilityIds: [],
            aiProfileId: "BANNER_PASSIVE",
            autoAttackInterval: TimeSpan.FromSeconds(1));
        EncounterDefinition encounter = new(
            "TEST_LINKED_OBJECT",
            "TEST_BOSS",
            [
                new EncounterPhaseDefinition(
                    "OPEN",
                    new EncounterTriggerDefinition(EncounterTriggerType.CombatStart),
                    [
                        new EncounterActionDefinition(
                            EncounterActionType.Summon,
                            Summon: new SummonDefinition(
                                banner.Id,
                                Count: 1,
                                MaxActive: 1,
                                LinkToCaster: true,
                                NoReward: true,
                                IsCombatObject: true,
                                AuraEffectId: aura.Id,
                                AuraTargetSelector: EncounterTargetSelectors.Boss))
                    ])
            ]);
        CombatSession session = Session(
            bossAbilityIds: new HashSet<string>(StringComparer.Ordinal),
            abilities: Abilities(strike),
            bossAi: new MonsterAiProfile("BOSS_PASSIVE", []),
            bossAutoAttackInterval: TimeSpan.FromHours(1),
            playerAbilityIds: new HashSet<string>([strike.Id], StringComparer.Ordinal));

        session.ConfigureGenericEncounter(
            encounter,
            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal)
            {
                [banner.Id] = new(banner, new MonsterAiProfile("BANNER_PASSIVE", []))
            },
            new Dictionary<string, EffectDefinition>(StringComparer.Ordinal)
            {
                [aura.Id] = aura
            });

        CombatActorSnapshot objectSnapshot = Assert.Single(
            session.Snapshot().Enemies!,
            enemy => enemy.DefinitionId == banner.Id);
        Assert.True(objectSnapshot.IsCombatObject);
        Assert.False(objectSnapshot.RewardEligible);
        Assert.False(objectSnapshot.AutoAttackEnabled);
        Assert.Empty(objectSnapshot.KnownAbilityIds);
        Assert.Contains(session.Snapshot().Enemy.Effects, effect => effect.Id == aura.Id);

        CombatCommandResult destroyed = session.Handle(
            new UseAbilityCommand("destroy-banner", strike.Id, objectSnapshot.ActorId),
            Now.AddMilliseconds(10));

        Assert.True(destroyed.Succeeded, destroyed.ErrorCode);
        Assert.Equal(0, session.Snapshot().Enemies!
            .Single(enemy => enemy.ActorId == objectSnapshot.ActorId).Hp);
        Assert.DoesNotContain(session.Snapshot().Enemy.Effects, effect => effect.Id == aura.Id);
        Assert.DoesNotContain(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.EnemyKilled
            && item.TargetActorId == objectSnapshot.ActorId);
    }

    [Fact]
    public void HpPhaseChangesBossAbilitySetBeforeItsNextAction()
    {
        AbilityDefinition strike = DamageAbility("PLAYER_STRIKE", 60);
        AbilityDefinition oldHit = DamageAbility("OLD_HIT", 2);
        AbilityDefinition phaseHit = DamageAbility("PHASE_HIT", 7);
        Dictionary<string, AbilityDefinition> abilities = Abilities(strike, oldHit, phaseHit);
        MonsterAiProfile bossAi = new(
            "BOSS_AI",
            [phaseHit.Id, oldHit.Id]);
        CombatSession session = Session(
            new HashSet<string>([oldHit.Id], StringComparer.Ordinal),
            abilities,
            bossAi,
            bossAutoAttackInterval: TimeSpan.FromSeconds(1),
            playerAbilityIds: new HashSet<string>([strike.Id], StringComparer.Ordinal));
        EncounterDefinition encounter = new(
            "TEST_HP_PHASE",
            "TEST_BOSS",
            [
                new EncounterPhaseDefinition(
                    "PHASE_TWO",
                    new EncounterTriggerDefinition(
                        EncounterTriggerType.HpAtOrBelow,
                        Threshold: 50),
                    [],
                    AbilityIds: [phaseHit.Id])
            ]);
        session.ConfigureGenericEncounter(
            encounter,
            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal),
            new Dictionary<string, Elyndor.Core.Combat.Effects.EffectDefinition>(StringComparer.Ordinal));

        CombatCommandResult strikeResult = session.Handle(
            new UseAbilityCommand("phase-trigger", strike.Id, BossId),
            Now);
        Assert.True(strikeResult.Succeeded, strikeResult.ErrorCode);
        Assert.Equal(
            [phaseHit.Id],
            session.Snapshot().Enemy.KnownAbilityIds.OrderBy(id => id, StringComparer.Ordinal));

        session.AdvanceTo(Now.AddSeconds(1));

        Assert.Contains(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.AbilityUsed
            && item.SourceActorId == BossId
            && item.DefinitionId == phaseHit.Id);
        Assert.DoesNotContain(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.AbilityUsed
            && item.SourceActorId == BossId
            && item.DefinitionId == oldHit.Id);
    }

    [Fact]
    public void DelayedResourceChangeExecutesAtExactEncounterDeadline()
    {
        CombatSession session = Session(
            bossAbilityIds: new HashSet<string>(StringComparer.Ordinal),
            abilities: new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            bossAi: new MonsterAiProfile("BOSS_PASSIVE", []),
            bossAutoAttackInterval: TimeSpan.FromHours(1),
            bossMaxResource: 100,
            bossCurrentResource: 100);
        EncounterDefinition encounter = new(
            "TEST_DELAYED_RESOURCE",
            "TEST_BOSS",
            [
                new EncounterPhaseDefinition(
                    "OPEN",
                    new EncounterTriggerDefinition(EncounterTriggerType.CombatStart),
                    [
                        new EncounterActionDefinition(
                            EncounterActionType.ResourceChange,
                            ResourceAmount: -40,
                            TargetSelector: EncounterTargetSelectors.Boss,
                            Delay: TimeSpan.FromSeconds(3))
                    ])
            ]);
        session.ConfigureGenericEncounter(
            encounter,
            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal),
            new Dictionary<string, Elyndor.Core.Combat.Effects.EffectDefinition>(StringComparer.Ordinal));

        Assert.Equal(Now.AddSeconds(3), session.NextDueAtUtc);
        session.AdvanceTo(Now.AddSeconds(2));
        Assert.Equal(100, session.Snapshot().Enemy.Resource);

        session.AdvanceTo(Now.AddSeconds(3));

        Assert.Equal(60, session.Snapshot().Enemy.Resource);
        Assert.Contains(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.ResourceChanged
            && item.ActorId == BossId
            && item.Amount == -40
            && item.OccurredAtUtc == Now.AddSeconds(3));
    }

    [Fact]
    public void OwnerLinkedSummonCanTargetAndHealItsBoss()
    {
        AbilityDefinition strike = DamageAbility("PLAYER_POKE", 20);
        AbilityDefinition ownerHeal = new(
            "OWNER_HEAL",
            AbilityType.Instant,
            AbilityTargetType.Owner,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "NATURE",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Healing,
                    Amount: 10)
            ]);
        Dictionary<string, AbilityDefinition> abilities = Abilities(strike, ownerHeal);
        MonsterDefinition healer = Monster(
            "OWNER_HEALER",
            [ownerHeal.Id],
            "OWNER_HEALER_AI",
            TimeSpan.FromSeconds(1));
        MonsterAiProfile healerAi = new(
            "OWNER_HEALER_AI",
            [ownerHeal.Id]);
        EncounterDefinition encounter = new(
            "TEST_OWNER_LINK",
            "TEST_BOSS",
            [
                new EncounterPhaseDefinition(
                    "OPEN",
                    new EncounterTriggerDefinition(EncounterTriggerType.CombatStart),
                    [
                        new EncounterActionDefinition(
                            EncounterActionType.Summon,
                            Summon: new SummonDefinition(
                                healer.Id,
                                Count: 1,
                                MaxActive: 1,
                                LinkToCaster: true))
                    ])
            ]);
        CombatSession session = Session(
            bossAbilityIds: new HashSet<string>(StringComparer.Ordinal),
            abilities,
            new MonsterAiProfile("BOSS_PASSIVE", []),
            bossAutoAttackInterval: TimeSpan.FromHours(1),
            playerAbilityIds: new HashSet<string>([strike.Id], StringComparer.Ordinal));
        session.ConfigureGenericEncounter(
            encounter,
            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal)
            {
                [healer.Id] = new(healer, healerAi)
            },
            new Dictionary<string, Elyndor.Core.Combat.Effects.EffectDefinition>(StringComparer.Ordinal));

        Assert.True(session.Handle(
            new UseAbilityCommand("poke-boss", strike.Id, BossId),
            Now).Succeeded);
        Assert.Equal(80, session.Snapshot().Enemy.Hp);

        session.AdvanceTo(Now.AddSeconds(1));

        Assert.Equal(90, session.Snapshot().Enemy.Hp);
        Assert.Contains(session.GetEventsAfter(0), item =>
            item.Type == CombatEventType.HealingApplied
            && item.SourceActorId != BossId
            && item.TargetActorId == BossId
            && item.DefinitionId == ownerHeal.Id);
    }

    private static CombatSession Session(
        IReadOnlySet<string> bossAbilityIds,
        Dictionary<string, AbilityDefinition> abilities,
        MonsterAiProfile bossAi,
        TimeSpan bossAutoAttackInterval,
        IReadOnlySet<string>? playerAbilityIds = null,
        decimal bossMaxResource = 0,
        decimal bossCurrentResource = 0)
    {
        CombatStats stats = CombatStats.Default with
        {
            Level = 10,
            Accuracy = 100,
            CriticalDamage = 1
        };
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 500, 500, 100, 100, stats),
            CombatActorKind.Player,
            "TEST_PLAYER",
            "Test Player",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            playerAbilityIds ?? new HashSet<string>(StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition boss = new(
            new CombatActorState(
                BossId,
                100,
                100,
                bossMaxResource,
                bossCurrentResource,
                stats),
            CombatActorKind.Monster,
            "TEST_BOSS",
            "Test Boss",
            bossMaxResource > 0 ? "MANA" : "NONE",
            new AutoAttackProfile(bossAutoAttackInterval, 0, 0, 0),
            bossAbilityIds,
            MonsterRank: MonsterRank.Boss);

        return new CombatSession(
            Guid.Parse("90000000-0000-0000-0000-000000000001"),
            player,
            boss,
            abilities,
            bossAi,
            ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.5m, 100).ToArray()),
            Now);
    }

    private static MonsterDefinition Monster(
        string id,
        IReadOnlyList<string> abilityIds,
        string aiProfileId,
        TimeSpan autoAttackInterval) =>
        new(
            Id: id,
            Name: id,
            Rank: MonsterRank.Normal,
            Level: 10,
            MaxHp: 50,
            Stats: CombatStats.Default with { Level = 10, Accuracy = 100 },
            AutoAttackInterval: autoAttackInterval,
            AutoAttackBaseDamage: 0,
            AbilityIds: abilityIds,
            AiProfileId: aiProfileId,
            AutoAttackAttackPowerCoefficient: 0,
            XpReward: 0,
            LootTableId: null,
            GoldRewardMin: 0,
            GoldRewardMax: 0);

    private static Dictionary<string, AbilityDefinition> Abilities(
        params AbilityDefinition[] abilities) =>
        abilities.ToDictionary(ability => ability.Id, StringComparer.Ordinal);

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
}
