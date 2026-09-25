using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Monsters;

namespace Elyndor.UnitTests.Combat;

public sealed class MonsterAiDecisionEngineTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 9, 18, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EncounterOrderSingleEnemyPreservesCurrentThreatTarget()
    {
        CombatActorState monster = Monster();
        AbilityDefinition strike = Ability("STRIKE", AbilityTargetType.SingleEnemy);
        AbilityTargetCandidate first = Enemy(threat: 100);
        AbilityTargetCandidate current = Enemy(threat: 10);
        MonsterAiProfile profile = new("AI", [strike.Id]);

        MonsterAiDecision? decision = MonsterAiDecisionEngine.Select(
            profile,
            monster,
            new HashSet<string>([strike.Id], StringComparer.Ordinal),
            Abilities(strike),
            [first, current],
            StartedAt,
            StartedAt,
            new MonsterAbilitySchedulerState(),
            new SequenceGameRandom(),
            currentThreatTargetId: current.ActorId);

        Assert.NotNull(decision);
        Assert.Equal(current.ActorId, decision!.PrimaryTargetActorId);
    }

    [Fact]
    public void EncounterOrderMultiTargetPreservesCandidateOrder()
    {
        CombatActorState monster = Monster();
        AbilityDefinition cleave = Ability(
            "LEGACY_CLEAVE",
            AbilityTargetType.NEnemiesInCombat,
            targetCount: 2);
        AbilityTargetCandidate first = Enemy(threat: 1);
        AbilityTargetCandidate second = Enemy(threat: 100);
        AbilityTargetCandidate third = Enemy(threat: 50);
        MonsterAiProfile profile = new("AI", [cleave.Id]);

        MonsterAiDecision? decision = MonsterAiDecisionEngine.Select(
            profile,
            monster,
            new HashSet<string>([cleave.Id], StringComparer.Ordinal),
            Abilities(cleave),
            [first, second, third],
            StartedAt,
            StartedAt,
            new MonsterAbilitySchedulerState(),
            new SequenceGameRandom(),
            currentThreatTargetId: second.ActorId);

        Assert.NotNull(decision);
        Assert.Equal([first.ActorId, second.ActorId], decision!.TargetActorIds);
    }

    [Fact]
    public void RandomMultiTargetSelectionIsDeterministicAndUnique()
    {
        CombatActorState monster = Monster();
        AbilityDefinition cleave = Ability(
            "RANDOM_TWO",
            AbilityTargetType.NEnemiesInCombat,
            targetCount: 2);
        MonsterAiProfile profile = new(
            "AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(
                    cleave.Id,
                    Priority: 10,
                    TargetSelector: AbilityTargetSelectorProfile.RandomEnemy)
            ]);
        AbilityTargetCandidate first = Enemy();
        AbilityTargetCandidate second = Enemy();
        AbilityTargetCandidate third = Enemy();

        MonsterAiDecision? decision = MonsterAiDecisionEngine.Select(
            profile,
            monster,
            new HashSet<string>([cleave.Id], StringComparer.Ordinal),
            Abilities(cleave),
            [first, second, third],
            StartedAt,
            StartedAt,
            new MonsterAbilitySchedulerState(),
            new SequenceGameRandom(0.75m, 0m));

        Assert.NotNull(decision);
        Assert.Equal(2, decision!.TargetActorIds.Count);
        Assert.Equal(third.ActorId, decision.TargetActorIds[0]);
        Assert.Equal(second.ActorId, decision.TargetActorIds[1]);
        Assert.Equal(2, decision.TargetActorIds.Distinct().Count());
    }

    [Fact]
    public void HigherPriorityRuleWithoutValidTargetFallsThroughToNextRule()
    {
        CombatActorState monster = Monster();
        AbilityDefinition invalidForSelector = Ability(
            "ALLY_SELECTOR_ON_ENEMY_ABILITY",
            AbilityTargetType.SingleEnemy);
        AbilityDefinition fallback = Ability(
            "FALLBACK",
            AbilityTargetType.SingleEnemy);
        MonsterAiProfile profile = new(
            "AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(
                    invalidForSelector.Id,
                    Priority: 100,
                    TargetSelector: AbilityTargetSelectorProfile.LowestHpAlly),
                new MonsterAbilityRule(fallback.Id, Priority: 10)
            ]);
        AbilityTargetCandidate enemy = Enemy();

        MonsterAiDecision? decision = MonsterAiDecisionEngine.Select(
            profile,
            monster,
            new HashSet<string>([invalidForSelector.Id, fallback.Id], StringComparer.Ordinal),
            Abilities(invalidForSelector, fallback),
            [enemy],
            StartedAt,
            StartedAt,
            new MonsterAbilitySchedulerState(),
            new SequenceGameRandom(),
            currentThreatTargetId: enemy.ActorId);

        Assert.NotNull(decision);
        Assert.Equal(fallback.Id, decision!.Ability.Id);
        Assert.Equal(enemy.ActorId, decision.PrimaryTargetActorId);
    }

    [Fact]
    public void LowestHpAllyCanTargetAnotherMonsterInsteadOfSelf()
    {
        CombatActorState monster = Monster();
        AbilityDefinition heal = Ability(
            "HEAL",
            AbilityTargetType.SingleAlly,
            allowSelfTarget: true);
        MonsterAiProfile profile = new(
            "AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(
                    heal.Id,
                    Priority: 10,
                    TargetSelector: AbilityTargetSelectorProfile.LowestHpAlly)
            ]);
        AbilityTargetCandidate hurtAlly = new(
            Guid.NewGuid(),
            IsEnemy: false,
            IsTank: false,
            UsesMana: false,
            CurrentHp: 20,
            MaxHp: 100);

        MonsterAiDecision? decision = MonsterAiDecisionEngine.Select(
            profile,
            monster,
            new HashSet<string>([heal.Id], StringComparer.Ordinal),
            Abilities(heal),
            [hurtAlly],
            StartedAt,
            StartedAt,
            new MonsterAbilitySchedulerState(),
            new SequenceGameRandom());

        Assert.NotNull(decision);
        Assert.Equal(hurtAlly.ActorId, decision!.PrimaryTargetActorId);
    }

    [Fact]
    public void OncePerCombatRuleDisappearsAfterSuccessfulExecutionIsMarked()
    {
        CombatActorState monster = Monster();
        AbilityDefinition burst = Ability("BURST", AbilityTargetType.SingleEnemy);
        MonsterAbilityRule rule = new(
            burst.Id,
            Priority: 10,
            OncePerCombat: true);
        MonsterAiProfile profile = new("AI", [], AbilityRules: [rule]);
        MonsterAbilitySchedulerState state = new();
        AbilityTargetCandidate target = Enemy();

        MonsterAiDecision? first = MonsterAiDecisionEngine.Select(
            profile,
            monster,
            new HashSet<string>([burst.Id], StringComparer.Ordinal),
            Abilities(burst),
            [target],
            StartedAt,
            StartedAt,
            state,
            new SequenceGameRandom(),
            currentThreatTargetId: target.ActorId);
        Assert.NotNull(first);

        state.MarkExecuted(first!.Rule, first.Ability, StartedAt);
        MonsterAiDecision? second = MonsterAiDecisionEngine.Select(
            profile,
            monster,
            new HashSet<string>([burst.Id], StringComparer.Ordinal),
            Abilities(burst),
            [target],
            StartedAt,
            StartedAt.AddSeconds(30),
            state,
            new SequenceGameRandom(),
            currentThreatTargetId: target.ActorId);

        Assert.Null(second);
    }

    [Fact]
    public void TargetHpRuleUsesExecuteOnlyAgainstWoundedTarget()
    {
        CombatActorState monster = Monster();
        AbilityDefinition execute = Ability("EXECUTE", AbilityTargetType.SingleEnemy);
        AbilityDefinition fallback = Ability("FALLBACK", AbilityTargetType.SingleEnemy);
        MonsterAiProfile profile = new(
            "AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(
                    execute.Id,
                    Priority: 100,
                    TargetSelector: AbilityTargetSelectorProfile.CurrentThreatTarget,
                    TargetMaxHpPercent: 30),
                new MonsterAbilityRule(fallback.Id, Priority: 10)
            ]);
        AbilityTargetCandidate healthy = Enemy() with { CurrentHp = 80, MaxHp = 100 };
        AbilityTargetCandidate wounded = healthy with { CurrentHp = 30 };

        MonsterAiDecision? healthyDecision = MonsterAiDecisionEngine.Select(
            profile, monster, new HashSet<string>([execute.Id, fallback.Id]), Abilities(execute, fallback),
            [healthy], StartedAt, StartedAt, new MonsterAbilitySchedulerState(), new SequenceGameRandom(),
            currentThreatTargetId: healthy.ActorId);
        MonsterAiDecision? woundedDecision = MonsterAiDecisionEngine.Select(
            profile, monster, new HashSet<string>([execute.Id, fallback.Id]), Abilities(execute, fallback),
            [wounded], StartedAt, StartedAt, new MonsterAbilitySchedulerState(), new SequenceGameRandom(),
            currentThreatTargetId: wounded.ActorId);

        Assert.Equal(fallback.Id, healthyDecision!.Ability.Id);
        Assert.Equal(execute.Id, woundedDecision!.Ability.Id);
    }

    private static CombatActorState Monster() =>
        CombatActorState.CreateDummy(
            100,
            stats: CombatStats.Default with { Level = 10 });

    private static AbilityTargetCandidate Enemy(decimal threat = 0) =>
        new(
            Guid.NewGuid(),
            IsEnemy: true,
            IsTank: false,
            UsesMana: false,
            CurrentHp: 100,
            MaxHp: 100,
            Threat: threat);

    private static Dictionary<string, AbilityDefinition> Abilities(
        params AbilityDefinition[] abilities) =>
        abilities.ToDictionary(ability => ability.Id, StringComparer.Ordinal);

    private static AbilityDefinition Ability(
        string id,
        AbilityTargetType targetType,
        int targetCount = 0,
        bool allowSelfTarget = true) =>
        new(
            id,
            AbilityType.Instant,
            targetType,
            0,
            TimeSpan.FromSeconds(5),
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL",
            AllowSelfTarget: allowSelfTarget,
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    Amount: 1,
                    DamageType: DamageType.True,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false)
            ],
            TargetCount: targetCount);
}
