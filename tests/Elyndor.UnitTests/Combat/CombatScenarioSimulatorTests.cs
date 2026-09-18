using Elyndor.Core.Combat;
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

    private static CombatSession CreateSession(IGameRandom random, bool withTimedAdd)
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
            MagicPenetration: 0);
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
            new Dictionary<string, Elyndor.Core.Combat.Abilities.AbilityDefinition>(StringComparer.Ordinal),
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
            new Dictionary<string, Elyndor.Core.Combat.Effects.EffectDefinition>(StringComparer.Ordinal));
        return session;
    }

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
