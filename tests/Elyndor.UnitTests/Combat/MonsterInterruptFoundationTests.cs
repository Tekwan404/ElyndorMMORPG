using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Monsters;

namespace Elyndor.UnitTests.Combat;

public sealed class MonsterInterruptFoundationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CastInProgressSelectorOnlyReturnsCastingEnemy()
    {
        AbilityTargetCandidate idle = Candidate(isCasting: false);
        AbilityTargetCandidate casting = Candidate(isCasting: true);

        Guid? selected = AbilityTargetSelector.SelectSingle(
            AbilityTargetSelectorProfile.CastInProgressEnemy,
            [idle, casting]);

        Assert.Equal(casting.ActorId, selected);
    }

    [Fact]
    public void MonsterAiSkipsInterruptRuleWhenNobodyIsCasting()
    {
        AbilityDefinition interrupt = InterruptAbility("SPELL_LOCK");
        AbilityDefinition fallback = Ability("FALLBACK");
        CombatActorState monster = CombatActorState.CreateDummy(100);
        MonsterAiProfile profile = new(
            "INTERRUPT_AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(
                    interrupt.Id,
                    Priority: 100,
                    TargetSelector: AbilityTargetSelectorProfile.CastInProgressEnemy),
                new MonsterAbilityRule(fallback.Id, Priority: 10)
            ]);
        AbilityTargetCandidate idle = Candidate(isCasting: false);

        MonsterAiDecision? decision = MonsterAiDecisionEngine.Select(
            profile,
            monster,
            new HashSet<string>([interrupt.Id, fallback.Id], StringComparer.Ordinal),
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
            {
                [interrupt.Id] = interrupt,
                [fallback.Id] = fallback
            },
            [idle],
            Now,
            Now,
            new MonsterAbilitySchedulerState(),
            new SequenceGameRandom(0.5m));

        Assert.NotNull(decision);
        Assert.Equal(fallback.Id, decision!.Ability.Id);
        Assert.Equal(idle.ActorId, decision.PrimaryTargetActorId);
    }

    [Fact]
    public void MonsterAiPrefersInterruptRuleWhenEnemyIsCasting()
    {
        AbilityDefinition interrupt = InterruptAbility("SPELL_LOCK");
        AbilityDefinition fallback = Ability("FALLBACK");
        CombatActorState monster = CombatActorState.CreateDummy(100);
        MonsterAiProfile profile = new(
            "INTERRUPT_AI",
            [],
            AbilityRules:
            [
                new MonsterAbilityRule(
                    interrupt.Id,
                    Priority: 100,
                    TargetSelector: AbilityTargetSelectorProfile.CastInProgressEnemy),
                new MonsterAbilityRule(fallback.Id, Priority: 10)
            ]);
        AbilityTargetCandidate casting = Candidate(isCasting: true);

        MonsterAiDecision? decision = MonsterAiDecisionEngine.Select(
            profile,
            monster,
            new HashSet<string>([interrupt.Id, fallback.Id], StringComparer.Ordinal),
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
            {
                [interrupt.Id] = interrupt,
                [fallback.Id] = fallback
            },
            [casting],
            Now,
            Now,
            new MonsterAbilitySchedulerState(),
            new SequenceGameRandom(0.5m));

        Assert.NotNull(decision);
        Assert.Equal(interrupt.Id, decision!.Ability.Id);
        Assert.Equal(casting.ActorId, decision.PrimaryTargetActorId);
    }

    [Fact]
    public void InterruptEngineAppliesSchoolLockoutToInterruptedCaster()
    {
        CombatRuntimeState runtime = new(CombatActorState.CreateDummy(100, 100, 100));
        CombatActorState target = CombatActorState.CreateDummy(100);
        runtime.AddActor(target);
        AbilityDefinition cast = new(
            "ARCANE_CAST",
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(4),
            false,
            GlobalCooldownCategory.None,
            true,
            "ARCANE",
            Actions: []);

        AbilityExecutionResult started = AbilityEngine.Execute(
            runtime,
            cast,
            new AbilityIntent("cast-1", cast.Id, target.ActorId),
            Now);
        AbilityExecutionResult interrupted = AbilityEngine.Interrupt(
            runtime,
            Now.AddSeconds(1),
            TimeSpan.FromSeconds(2));
        AbilityExecutionResult locked = AbilityEngine.Execute(
            runtime,
            cast,
            new AbilityIntent("cast-2", cast.Id, target.ActorId),
            Now.AddSeconds(1.5));

        Assert.True(started.Succeeded);
        Assert.True(interrupted.Succeeded);
        Assert.Null(runtime.ActiveCast);
        Assert.Equal(AbilityErrorCode.SchoolLocked, locked.ErrorCode);
    }

    private static AbilityTargetCandidate Candidate(bool isCasting) =>
        new(
            Guid.NewGuid(),
            IsEnemy: true,
            IsTank: false,
            UsesMana: true,
            CurrentHp: 100,
            MaxHp: 100,
            IsCasting: isCasting);

    private static AbilityDefinition InterruptAbility(string id) =>
        Ability(id) with
        {
            TargetSelectorProfile = AbilityTargetSelectorProfile.CastInProgressEnemy,
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Interrupt,
                    InterruptLockout: TimeSpan.FromSeconds(2))
            ]
        };

    private static AbilityDefinition Ability(string id) =>
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
            Actions: []);
}
