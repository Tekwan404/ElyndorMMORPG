using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Targeting;
using Elyndor.Core.Monsters;

namespace Elyndor.UnitTests.Combat;

public sealed class PveAbilityFoundationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RandomEnemySelectorUsesInjectedDeterministicRng()
    {
        AbilityTargetCandidate ally = Candidate(isEnemy: false);
        AbilityTargetCandidate first = Candidate(isEnemy: true);
        AbilityTargetCandidate second = Candidate(isEnemy: true);

        Guid? selected = AbilityTargetSelector.SelectSingle(
            AbilityTargetSelectorProfile.RandomEnemy,
            [ally, first, second],
            new SequenceGameRandom(0.75m));

        Assert.Equal(second.ActorId, selected);
    }

    [Fact]
    public void LowestHpAllyAndHighestThreatSelectorsUseServerState()
    {
        AbilityTargetCandidate healthyAlly = Candidate(isEnemy: false, hp: 90);
        AbilityTargetCandidate hurtAlly = Candidate(isEnemy: false, hp: 25);
        AbilityTargetCandidate lowThreatEnemy = Candidate(isEnemy: true, threat: 20);
        AbilityTargetCandidate highThreatEnemy = Candidate(isEnemy: true, threat: 100);
        AbilityTargetCandidate[] candidates =
            [healthyAlly, hurtAlly, lowThreatEnemy, highThreatEnemy];

        Assert.Equal(
            hurtAlly.ActorId,
            AbilityTargetSelector.SelectSingle(
                AbilityTargetSelectorProfile.LowestHpAlly,
                candidates));
        Assert.Equal(
            highThreatEnemy.ActorId,
            AbilityTargetSelector.SelectSingle(
                AbilityTargetSelectorProfile.HighestThreat,
                candidates));
    }

    [Fact]
    public void SchedulerHonorsHpWindowInitialDelayAndOncePerCombat()
    {
        CombatActorState monster = CombatActorState.CreateDummy(100);
        monster.SetCurrentHp(25);
        MonsterAbilityRule finisher = new(
            "ENRAGE",
            Priority: 100,
            MaxHpPercent: 30,
            OncePerCombat: true,
            InitialDelay: TimeSpan.FromSeconds(3));
        MonsterAiProfile profile = new(
            "TEST_AI",
            [],
            AbilityRules: [finisher]);
        MonsterAbilitySchedulerState state = new();

        MonsterAbilityRule? tooEarly = MonsterAbilityScheduler.SelectRule(
            profile, monster, Now, Now.AddSeconds(2), state);
        MonsterAbilityRule? ready = MonsterAbilityScheduler.SelectRule(
            profile, monster, Now, Now.AddSeconds(3), state);

        Assert.Null(tooEarly);
        Assert.Equal(finisher, ready);

        AbilityDefinition ability = Ability(
            finisher.AbilityId,
            TimeSpan.FromSeconds(20));
        state.MarkExecuted(finisher, ability, Now.AddSeconds(3));

        Assert.Null(MonsterAbilityScheduler.SelectRule(
            profile, monster, Now, Now.AddSeconds(30), state));
    }

    [Fact]
    public void SchedulerFallsBackToLegacyPriorityAbilityIds()
    {
        CombatActorState monster = CombatActorState.CreateDummy(100);
        MonsterAiProfile profile = new("LEGACY", ["FIRST", "SECOND"]);

        MonsterAbilityRule? selected = MonsterAbilityScheduler.SelectRule(
            profile,
            monster,
            Now,
            Now,
            new MonsterAbilitySchedulerState());

        Assert.NotNull(selected);
        Assert.Equal("FIRST", selected!.AbilityId);
    }

    [Fact]
    public void ResourceChangeCanModifySelectedTargetInsteadOfCaster()
    {
        CombatRuntimeState runtime = new(CombatActorState.CreateDummy(100, 100, 100));
        CombatActorState target = CombatActorState.CreateDummy(100, 100, 80);
        runtime.AddActor(target);
        AbilityDefinition ability = Ability("MANA_BURN", TimeSpan.Zero) with
        {
            Actions =
            [
                new AbilityActionDefinition(
                    AbilityActionType.ResourceChange,
                    Amount: -30,
                    ResourceTarget: AbilityResourceTarget.Target)
            ]
        };

        AbilityExecutionResult result = AbilityEngine.Execute(
            runtime,
            ability,
            new AbilityIntent("mana-burn", ability.Id, target.ActorId),
            Now);

        Assert.True(result.Succeeded);
        Assert.Equal(100, runtime.Actor.CurrentResource);
        Assert.Equal(50, target.CurrentResource);
        Assert.Contains(result.Events, item =>
            item.Type == CombatEventType.ResourceChanged
            && item.TargetActorId == target.ActorId
            && item.Amount == -30);
    }

    [Theory]
    [InlineData(EffectKind.Root)]
    [InlineData(EffectKind.Fear)]
    [InlineData(EffectKind.Disarm)]
    public void NewControlKindsCanBeAppliedAndObserved(EffectKind kind)
    {
        CombatActorState actor = CombatActorState.CreateDummy(100);
        EffectDefinition effect = new(
            $"TEST_{kind}",
            kind,
            TimeSpan.FromSeconds(3),
            1,
            EffectStackPolicy.Replace,
            0);

        EffectEngine.Apply(actor, actor.ActorId, effect, Now);

        Assert.True(EffectEngine.HasControl(actor, kind, Now.AddSeconds(1)));
    }

    [Fact]
    public void RootBlocksOnlyAbilitiesMarkedAsRequiringMobility()
    {
        CombatRuntimeState runtime = ControlledRuntime(EffectKind.Root);
        AbilityDefinition mobileAbility = SelfAbility("MOBILE") with { RequiresMobility = true };
        AbilityDefinition stationaryAbility = SelfAbility("STATIONARY");

        AbilityExecutionResult blocked = AbilityEngine.Execute(
            runtime,
            mobileAbility,
            new AbilityIntent("mobile", mobileAbility.Id, runtime.Actor.ActorId),
            Now);
        AbilityExecutionResult allowed = AbilityEngine.Execute(
            runtime,
            stationaryAbility,
            new AbilityIntent("stationary", stationaryAbility.Id, runtime.Actor.ActorId),
            Now);

        Assert.Equal(AbilityErrorCode.ActorRooted, blocked.ErrorCode);
        Assert.True(allowed.Succeeded);
    }

    [Fact]
    public void DisarmBlocksWeaponTaggedAbility()
    {
        CombatRuntimeState runtime = ControlledRuntime(EffectKind.Disarm);
        AbilityDefinition weaponAbility = SelfAbility("WEAPON") with { RequiresWeapon = true };

        AbilityExecutionResult result = AbilityEngine.Execute(
            runtime,
            weaponAbility,
            new AbilityIntent("weapon", weaponAbility.Id, runtime.Actor.ActorId),
            Now);

        Assert.Equal(AbilityErrorCode.ActorDisarmed, result.ErrorCode);
    }

    [Fact]
    public void FearBlocksNormalAbilityUnlessExplicitlyAllowed()
    {
        CombatRuntimeState runtime = ControlledRuntime(EffectKind.Fear);
        AbilityDefinition normal = SelfAbility("NORMAL");
        AbilityDefinition escape = SelfAbility("ESCAPE") with { CanUseWhileFeared = true };

        AbilityExecutionResult blocked = AbilityEngine.Execute(
            runtime,
            normal,
            new AbilityIntent("normal", normal.Id, runtime.Actor.ActorId),
            Now);
        AbilityExecutionResult allowed = AbilityEngine.Execute(
            runtime,
            escape,
            new AbilityIntent("escape", escape.Id, runtime.Actor.ActorId),
            Now);

        Assert.Equal(AbilityErrorCode.ActorFeared, blocked.ErrorCode);
        Assert.True(allowed.Succeeded);
    }

    private static CombatRuntimeState ControlledRuntime(EffectKind kind)
    {
        CombatRuntimeState runtime = new(CombatActorState.CreateDummy(100));
        EffectEngine.Apply(
            runtime.Actor,
            runtime.Actor.ActorId,
            new EffectDefinition(
                $"TEST_{kind}",
                kind,
                TimeSpan.FromSeconds(3),
                1,
                EffectStackPolicy.Replace,
                0),
            Now);
        return runtime;
    }

    private static AbilityTargetCandidate Candidate(
        bool isEnemy,
        decimal hp = 100,
        decimal threat = 0) =>
        new(
            Guid.NewGuid(),
            isEnemy,
            IsTank: false,
            UsesMana: false,
            CurrentHp: hp,
            MaxHp: 100,
            Threat: threat);

    private static AbilityDefinition Ability(string id, TimeSpan cooldown) =>
        new(
            id,
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            cooldown,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL");

    private static AbilityDefinition SelfAbility(string id) =>
        new(
            id,
            AbilityType.Instant,
            AbilityTargetType.Self,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            false,
            "PHYSICAL");
}