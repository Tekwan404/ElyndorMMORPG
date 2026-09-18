using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Encounters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Combat.Simulation;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatReplaySimulatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 11, 0, 0, TimeSpan.Zero);
    private static readonly Guid PlayerId =
        Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid BossId =
        Guid.Parse("a2000000-0000-0000-0000-000000000001");

    [Fact]
    public void FixedSeedReplaysSameAuthoritativeCombatResult()
    {
        CombatReplayScenario scenario = new(
            "FIXED_SEED_REPLAY",
            Seed: 1337,
            SessionFactory: random => CreateSession(random, withTimedAdd: false),
            MaximumDuration: TimeSpan.FromSeconds(30));

        CombatReplayResult first = CombatReplaySimulator.Run(scenario);
        CombatReplayResult second = CombatReplaySimulator.Run(scenario);

        Assert.Equal(CombatReplayEndReason.Victory, first.EndReason);
        Assert.Equal(first.EndReason, second.EndReason);
        Assert.Equal(first.Metrics, second.Metrics);
        Assert.Equal(
            first.Events.Select(EventFingerprint).ToArray(),
            second.Events.Select(EventFingerprint).ToArray());
        Assert.NotNull(first.Metrics.TimeToKill);
        Assert.True(first.Metrics.IncomingDamage > 0);
        Assert.True(first.Metrics.IncomingDamagePerSecond > 0);
    }

    [Fact]
    public void SimulatorTracksExactSummonedAddUptimeFromCombatEvents()
    {
        CombatReplayScenario scenario = new(
            "ADD_UPTIME",
            Seed: 17,
            SessionFactory: random => CreateSession(random, withTimedAdd: true),
            MaximumDuration: TimeSpan.FromSeconds(30));

        CombatReplayResult result = CombatReplaySimulator.Run(scenario);

        Assert.Equal(CombatReplayEndReason.Victory, result.EndReason);
        Assert.Equal(TimeSpan.FromSeconds(2), result.Metrics.AddUptime);
        Assert.Equal(1, result.Metrics.PeakActiveAdds);
        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.ActorSummoned
            && item.DefinitionId == "SIM_ADD");
        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.ActorDied
            && item.DefinitionId == "SIM_ADD"
            && item.OccurredAtUtc == Now.AddSeconds(2));
    }

    [Fact]
    public void SimulatorCountsRealEnemyCastInterruptFromDecisionPolicy()
    {
        CombatReplayScenario scenario = new(
            "INTERRUPT_ACCEPTANCE",
            Seed: 23,
            SessionFactory: CreateInterruptSession,
            MaximumDuration: TimeSpan.FromSeconds(2),
            DecisionPolicy: context =>
                context.Snapshot.Enemy.ActiveCast is null
                    ? null
                    : new UseAbilityCommand(
                        $"interrupt-{context.Step}",
                        "CONCUSSION_BLOW",
                        BossId));

        CombatReplayResult result = CombatReplaySimulator.Run(scenario);

        Assert.Equal(CombatReplayEndReason.Timeout, result.EndReason);
        Assert.Equal(1, result.Metrics.InterruptCount);
        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.AbilityInterrupted
            && item.ActorId == BossId
            && item.TargetActorId == BossId);
    }

    [Fact]
    public void SimulatorCountsRealDispelAbilityAndRemovesEffect()
    {
        CombatReplayScenario scenario = new(
            "DISPEL_ACCEPTANCE",
            Seed: 29,
            SessionFactory: CreateDispelSession,
            MaximumDuration: TimeSpan.FromSeconds(2),
            DecisionPolicy: context =>
                context.Snapshot.Player.Effects.Any(effect => effect.Id == "SIM_CURSE")
                    ? new UseAbilityCommand(
                        $"cleanse-{context.Step}",
                        "SIM_CLEANSE",
                        PlayerId)
                    : null,
            DispelAbilityIds: new HashSet<string>(StringComparer.Ordinal)
            {
                "SIM_CLEANSE"
            });

        CombatReplayResult result = CombatReplaySimulator.Run(scenario);

        Assert.Equal(CombatReplayEndReason.Timeout, result.EndReason);
        Assert.Equal(1, result.Metrics.DispelCount);
        Assert.DoesNotContain(
            result.FinalSnapshot.Player.Effects,
            effect => effect.Id == "SIM_CURSE");
        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.EffectRemoved
            && item.DefinitionId == "SIM_CURSE"
            && item.TargetActorId == PlayerId);
    }

    private static CombatSession CreateSession(IGameRandom random, bool withTimedAdd)
    {
        CombatStats stats = Stats();
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 500, 500, 100, 100, stats),
            CombatActorKind.Player,
            "SIM_PLAYER",
            "Simulation Player",
            "MANA",
            new AutoAttackProfile(
                TimeSpan.FromSeconds(1),
                BaseDamage: 20,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0,
                BaseDamageMin: 18,
                BaseDamageMax: 22),
            new HashSet<string>(StringComparer.Ordinal));
        CombatParticipantDefinition boss = new(
            new CombatActorState(BossId, 100, 100, 0, 0, stats),
            CombatActorKind.Monster,
            "SIM_BOSS",
            "Simulation Boss",
            "NONE",
            new AutoAttackProfile(
                TimeSpan.FromSeconds(1),
                BaseDamage: 10,
                AttackPowerCoefficient: 0,
                ResourceOnHit: 0),
            new HashSet<string>(StringComparer.Ordinal),
            MonsterRank: MonsterRank.Boss);
        CombatSession session = new(
            Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            player,
            boss,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal),
            new MonsterAiProfile("SIM_BOSS_AI", []),
            ResolvedTalentModifiers.Empty,
            random,
            Now);

        if (!withTimedAdd)
            return session;

        MonsterDefinition add = new(
            Id: "SIM_ADD",
            Name: "Simulation Add",
            Rank: MonsterRank.Normal,
            Level: 20,
            MaxHp: 20,
            Stats: stats,
            AutoAttackInterval: TimeSpan.FromHours(1),
            AutoAttackBaseDamage: 0,
            AbilityIds: [],
            AiProfileId: "SIM_ADD_AI",
            AutoAttackAttackPowerCoefficient: 0,
            XpReward: 0,
            LootTableId: null,
            GoldRewardMin: 0,
            GoldRewardMax: 0);
        EncounterDefinition encounter = new(
            "SIM_ENCOUNTER",
            "SIM_BOSS",
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
                                Lifetime: TimeSpan.FromSeconds(2),
                                LinkToCaster: true,
                                NoReward: true))
                    ])
            ]);
        session.ConfigureGenericEncounter(
            encounter,
            new Dictionary<string, EncounterEnemyProfile>(StringComparer.Ordinal)
            {
                [add.Id] = new(add, new MonsterAiProfile("SIM_ADD_AI", []))
            },
            new Dictionary<string, EffectDefinition>(StringComparer.Ordinal));
        return session;
    }

    private static CombatSession CreateInterruptSession(IGameRandom random)
    {
        AbilityDefinition bossCast = new(
            "BOSS_CAST",
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(4),
            false,
            GlobalCooldownCategory.None,
            true,
            "ARCANE",
            Interruptible: true,
            Actions: []);
        AbilityDefinition interrupt = new(
            "CONCUSSION_BLOW",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.FromSeconds(30),
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL",
            Actions: []);
        Dictionary<string, AbilityDefinition> abilities = new(StringComparer.Ordinal)
        {
            [bossCast.Id] = bossCast,
            [interrupt.Id] = interrupt
        };
        CombatParticipantDefinition player = new(
            new CombatActorState(PlayerId, 1_000, 1_000, 100, 100, Stats()),
            CombatActorKind.Player,
            "WARRIOR",
            "Simulation Warrior",
            "RAGE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>([interrupt.Id], StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition boss = new(
            new CombatActorState(BossId, 2_000, 2_000, 0, 0, Stats()),
            CombatActorKind.Monster,
            "INTERRUPT_TEST_BOSS",
            "Interrupt Test Boss",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromMilliseconds(100), 0, 0, 0),
            new HashSet<string>([bossCast.Id], StringComparer.Ordinal),
            MonsterRank: MonsterRank.Boss);
        return new CombatSession(
            Guid.Parse("a0000000-0000-0000-0000-000000000002"),
            player,
            boss,
            abilities,
            new MonsterAiProfile("INTERRUPT_TEST_AI", [bossCast.Id]),
            ResolvedTalentModifiers.Empty,
            random,
            Now);
    }

    private static CombatSession CreateDispelSession(IGameRandom random)
    {
        CombatActorState playerState = new(PlayerId, 500, 500, 100, 100, Stats());
        AbilityDefinition cleanse = new(
            "SIM_CLEANSE",
            AbilityType.Instant,
            AbilityTargetType.Self,
            0,
            TimeSpan.FromSeconds(30),
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
        CombatParticipantDefinition player = new(
            playerState,
            CombatActorKind.Player,
            "PALADIN",
            "Simulation Paladin",
            "MANA",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>([cleanse.Id], StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition boss = new(
            new CombatActorState(BossId, 2_000, 2_000, 0, 0, Stats()),
            CombatActorKind.Monster,
            "DISPEL_TEST_BOSS",
            "Dispel Test Boss",
            "NONE",
            new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal),
            MonsterRank: MonsterRank.Boss,
            CanAutoAttack: false);
        CombatSession session = new(
            Guid.Parse("a0000000-0000-0000-0000-000000000003"),
            player,
            boss,
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
            {
                [cleanse.Id] = cleanse
            },
            new MonsterAiProfile("DISPEL_TEST_AI", []),
            ResolvedTalentModifiers.Empty,
            random,
            Now);
        EffectEngine.Apply(
            playerState,
            BossId,
            new EffectDefinition(
                "SIM_CURSE",
                EffectKind.Debuff,
                TimeSpan.FromSeconds(10),
                1,
                EffectStackPolicy.Replace,
                0,
                DispelCategory: "Curse"),
            Now);
        return session;
    }

    private static CombatStats Stats() =>
        new(
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

    private static string EventFingerprint(CombatEvent combatEvent) =>
        string.Join(
            '|',
            combatEvent.Sequence,
            combatEvent.Type,
            combatEvent.OccurredAtUtc.ToUnixTimeMilliseconds(),
            combatEvent.ActorId,
            combatEvent.DefinitionId,
            combatEvent.Amount,
            combatEvent.SourceActorId,
            combatEvent.TargetActorId,
            combatEvent.IsPeriodic);
}
