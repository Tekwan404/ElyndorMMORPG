using System.Reflection;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatResourceCharacterizationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false, "WARRIOR", 0)]
    [InlineData(true, "WARRIOR", 0)]
    [InlineData(false, "MAGE", 4)]
    [InlineData(true, "MAGE", 4)]
    [InlineData(false, "ARCHER", 8)]
    [InlineData(true, "ARCHER", 8)]
    [InlineData(false, "PALADIN", 4)]
    [InlineData(true, "PALADIN", 4)]
    public void SpendPrecedesAbilityAndRegenUsesOnlyNewElapsedTime(bool hosted, string classId, decimal regen)
    {
        Fight fight = Create(hosted, classId, regen);
        Assert.True(fight.Cast("first", Now));
        Assert.Equal(80, fight.Player.CurrentResource);
        Assert.Equal(-20, Assert.Single(fight.Events(), e => e.Type == CombatEventType.ResourceChanged && e.Amount < 0).Amount);
        Assert.True(Array.FindIndex(fight.Events().ToArray(), e => e.Type == CombatEventType.ResourceChanged)
            < Array.FindIndex(fight.Events().ToArray(), e => e.Type == CombatEventType.AbilityStarted));
        Assert.False(fight.Cast("first", Now));
        Assert.Equal(80, fight.Player.CurrentResource);
        fight.Advance(Now.AddSeconds(1));
        Assert.Equal(80 + regen, fight.Player.CurrentResource);
        fight.Advance(Now.AddSeconds(2));
        fight.Advance(Now.AddSeconds(2));
        Assert.Equal(80 + 2 * regen, fight.Player.CurrentResource);
        Assert.Equal(2 * regen, fight.Events().Where(e => e.DefinitionId == "COMBAT_REGEN").Sum(e => e.Amount));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManaRegenIntegratesAfterglowAndMeditationBoundaries(bool hosted)
    {
        Fight fight = Create(hosted, "MAGE", 4,
            [Hook("A-2-1", 25), Hook("A-6-3", 100) with { Duration = TimeSpan.FromSeconds(2) }]);
        Assert.True(fight.Player.TrySpendResource(80));
        EffectEngine.Apply(fight.Player, fight.Player.ActorId,
            new EffectDefinition("MAGE_CLEARCASTING_REGEN", EffectKind.Buff, TimeSpan.FromSeconds(1),
                1, EffectStackPolicy.Replace, 100), Now);
        fight.Advance(Now.AddSeconds(3));
        // 0..1: 4*1.25*2; 1..2: 4*1.25; 2..3: 4*1.25*2.
        Assert.Equal(45, fight.Player.CurrentResource);
        Assert.Equal(25, fight.Events().Where(e => e.DefinitionId == "COMBAT_REGEN").Sum(e => e.Amount));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManaShieldResultSchedulesOneDelayedRefundWithoutReapplyingTheSpend(bool hosted)
    {
        Fight fight = Create(hosted, "MAGE", 0,
            [Hook("A-5-4", 50) with { Duration = TimeSpan.FromSeconds(2) }]);
        Assert.True(fight.Player.TrySpendResource(40));
        fight.Publish(new CombatEvent(CombatEventType.ResourceChanged, Now, fight.Player.ActorId,
            "MAGE_MANA_SHIELD_EFFECT", -40, SourceActorId: fight.Player.ActorId, TargetActorId: fight.Player.ActorId));
        Assert.Equal(60, fight.Player.CurrentResource);
        fight.Advance(Now.AddSeconds(1));
        Assert.Equal(60, fight.Player.CurrentResource);
        fight.Advance(Now.AddSeconds(2));
        // PvE drains at class sync (enemy AI/cast completion); hosted Arena also
        // advertises a mechanics due-time. Keep scheduler ownership unchanged.
        fight.Sync(Now.AddSeconds(2));
        fight.Advance(Now.AddSeconds(3));
        Assert.Equal(80, fight.Player.CurrentResource);
        CombatEvent refund = Assert.Single(fight.Events(), e => e.DefinitionId == "A-5-4");
        Assert.Equal(20, refund.Amount);
        Assert.True(refund.IsProc);
        Assert.Equal(1, refund.ProcDepth);
    }

    [Theory]
    [InlineData(false, false, 5)]
    [InlineData(true, false, 5)]
    [InlineData(false, true, 0)]
    [InlineData(true, true, 0)]
    public void DirectDamageGeneratesRageButPeriodicDamageDoesNot(bool hosted, bool periodic, decimal expected)
    {
        Fight fight = Create(hosted, "WARRIOR", 0);
        Assert.True(fight.Player.TrySpendResource(100));
        fight.Publish(new CombatEvent(CombatEventType.DamageDealt, Now, fight.Player.ActorId,
            "INCOMING", 10, SourceActorId: fight.Opponent.ActorId, TargetActorId: fight.Player.ActorId,
            IsPeriodic: periodic));
        Assert.Equal(expected, fight.Player.CurrentResource);
        Assert.Equal(expected, fight.Events().Where(e => e.DefinitionId == "DIRECT_DAMAGE_TAKEN").Sum(e => e.Amount));
        Assert.Equal(1000, fight.Player.CurrentHp); // Result notification is not a damage command either.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbilityResourceActionAndGenericProcMutateExactlyOnceAndPreserveClamp(bool hosted)
    {
        Fight fight = Create(hosted, "ARCHER", 0,
            [Hook("TEST_CAST", 7) with { Key = TalentModifierKeys.OnAbilityUsed }], resourceAction: 200);
        Assert.True(fight.Cast("cast", Now));
        Assert.Equal(100, fight.Player.CurrentResource);
        CombatEvent[] events = fight.Events().Where(e => e.Type == CombatEventType.ResourceChanged).ToArray();
        Assert.Equal(new decimal[] { -20, 20 }, events.Select(e => e.Amount));
        // The generic +7 is clamped, so publication-only grants suppress its zero result.
        Assert.DoesNotContain(events, e => e.DefinitionId == "TEST_CAST");
    }

    private static ResolvedTalentEventHook Hook(string id, decimal value) =>
        new(id, "RESOURCE_FIXTURE", 1, value, null, TimeSpan.Zero, false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenericResourceProcFollowsAbilityCompletionAndDoesNotReplayTheSpend(bool hosted)
    {
        Fight fight = Create(hosted, "ARCHER", 0,
            [Hook("TEST_CAST", 7) with { Key = TalentModifierKeys.OnAbilityUsed }]);
        Assert.True(fight.Cast("cast", Now));
        Assert.Equal(87, fight.Player.CurrentResource);
        CombatEvent[] events = fight.Events().ToArray();
        CombatEvent proc = Assert.Single(events, e => e.DefinitionId == "TEST_CAST");
        Assert.Equal(7, proc.Amount);
        Assert.True(proc.IsProc);
        Assert.Equal("TEST_CAST", proc.ProcOriginId);
        Assert.True(Array.FindIndex(events, e => e.Type == CombatEventType.AbilityCompleted)
            < Array.IndexOf(events, proc));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PyromancerCriticalRefundUsesExecutedCostExactlyOnce(bool hosted)
    {
        AbilityDefinition ability = Attack("MAGE_FIREBALL", true);
        Fight fight = Create(hosted, "MAGE", 0, [Hook("F-3-3", 50)],
            abilityOverride: ability, playerStats: CombatStats.Default with { CriticalChance = 100 });
        Assert.True(fight.Cast("crit", Now));
        Assert.Equal(90, fight.Player.CurrentResource);
        Assert.Equal(10, Assert.Single(fight.Events(), e => e.DefinitionId == "F-3-3").Amount);
        Assert.False(fight.Cast("crit", Now));
        Assert.Equal(90, fight.Player.CurrentResource);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArcherMissRefundUsesSpentFocusExactlyOnce(bool hosted)
    {
        AbilityDefinition ability = Attack("PIERCING_ARROW", false) with
        { Actions = [new(AbilityActionType.Damage, 10, DamageType.Physical, CanMiss: false, CanDodge: true)] };
        Fight fight = Create(hosted, "ARCHER", 0,
            [Hook("M-7-3", 50) with { TargetId = "PHYSICAL_MISS_REFUND" }], abilityOverride: ability,
            opponentStats: CombatStats.Default with { Dodge = 100 });
        Assert.True(fight.Cast("miss", Now));
        Assert.Equal(90, fight.Player.CurrentResource);
        Assert.Equal(10, Assert.Single(fight.Events(), e => e.DefinitionId == "M-7-3").Amount);
        Assert.Equal(1000, fight.Opponent.CurrentHp);
    }

    private static AbilityDefinition Attack(string id, bool spell) => new(id, AbilityType.Instant,
        AbilityTargetType.SingleEnemy, 20, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None,
        spell, spell ? "FIRE" : "PHYSICAL", Actions:
        [new(AbilityActionType.Damage, 10, spell ? DamageType.Magical : DamageType.Physical,
            CanMiss: false, CanDodge: false, CanCrit: true)]);

    [Fact]
    public void PartyRegenKeepsSeparateCursorsAndRulesAndDoesNotRegenerateUnattachedPlayers()
    {
        var auto = new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0);
        CombatParticipantDefinition Participant(string classId, decimal regen) => new(
            new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 0, CombatStats.Default),
            CombatActorKind.Player, classId, classId, "MANA", auto, new HashSet<string>(),
            ResourceRegenPerSecond: regen, CanAutoAttack: false);
        var mage = Participant("MAGE", 4);
        var archer = Participant("ARCHER", 8) with { ResourceType = "FOCUS" };
        var absent = Participant("MAGE", 50);
        var enemy = Participant("ENEMY", 0) with { Kind = CombatActorKind.Monster };
        var session = new CombatSession(Guid.NewGuid(), mage, enemy, new Dictionary<string, AbilityDefinition>(),
            new MonsterAiProfile("PASSIVE", []), ResolvedTalentModifiers.Empty with { EventHooks = [Hook("A-2-1", 25)] },
            new SeededGameRandom(1), Now, additionalPlayers:
            [new(Guid.NewGuid(), archer, ResolvedTalentModifiers.Empty),
             new(Guid.NewGuid(), absent, ResolvedTalentModifiers.Empty, InitiallyAttached: false)]);
        session.AdvanceTo(Now.AddSeconds(1));
        session.Snapshot(mage.Actor.ActorId);
        session.AdvanceTo(Now.AddSeconds(2));
        Assert.Equal(10, mage.Actor.CurrentResource);
        Assert.Equal(16, archer.Actor.CurrentResource);
        Assert.Equal(0, absent.Actor.CurrentResource);
        Assert.Equal(10, session.GetEventsAfter(0).Where(e => e.Type == CombatEventType.ResourceChanged
            && e.ActorId == mage.Actor.ActorId).Sum(e => e.Amount));
        Assert.Equal(16, session.GetEventsAfter(0).Where(e => e.Type == CombatEventType.ResourceChanged
            && e.ActorId == archer.Actor.ActorId).Sum(e => e.Amount));
    }

    [Fact]
    public void PartyMeditationUsesEachMagesLastSpendDespiteActivePlayerSwitching()
    {
        var (session, first, second) = CreateParty("MAGE",
            [Hook("A-6-3", 100) with { Duration = TimeSpan.FromSeconds(2) }]);
        Assert.True(second.TrySpendResource(50));
        Assert.True(session.Handle(first.ActorId, new UseAbilityCommand("first", "TEST", first.ActorId), Now).Succeeded);
        Assert.True(session.Handle(second.ActorId, new UseAbilityCommand("second", "TEST", second.ActorId), Now.AddSeconds(1)).Succeeded);
        session.Snapshot(first.ActorId);
        session.AdvanceTo(Now.AddSeconds(3));
        Assert.Equal(96, first.CurrentResource); // 80 + 2*4 + 1*8
        Assert.Equal(42, second.CurrentResource); // 50 + 1*4 - 20 + 2*4
    }

    [Fact]
    public void MultipleManaShieldRefundsStayWithTheirOwnerAndOriginalDueTimes()
    {
        var (session, first, second) = CreateParty("MAGE",
            [Hook("A-5-4", 50) with { Duration = TimeSpan.FromSeconds(2) }], regen: 0);
        QueueRefund(session, first, 20, Now);
        QueueRefund(session, first, 40, Now.AddSeconds(1));
        QueueRefund(session, second, 10, Now.AddSeconds(1));
        SyncMage(session, second, Now.AddSeconds(2));
        Assert.Equal(90, second.CurrentResource);
        Assert.Equal(40, first.CurrentResource);
        SyncMage(session, first, Now.AddSeconds(2));
        Assert.Equal(50, first.CurrentResource);
        SyncMage(session, second, Now.AddSeconds(3));
        SyncMage(session, first, Now.AddSeconds(3));
        SyncMage(session, first, Now.AddSeconds(4));
        Assert.Equal(70, first.CurrentResource);
        Assert.Equal(95, second.CurrentResource);
        CombatEvent[] refunds = session.GetEventsAfter(0).Where(e => e.DefinitionId == "A-5-4").ToArray();
        Assert.Equal(3, refunds.Length);
        Assert.Equal(30, refunds.Where(e => e.ActorId == first.ActorId).Sum(e => e.Amount));
        Assert.Equal(5, refunds.Where(e => e.ActorId == second.ActorId).Sum(e => e.Amount));
        Assert.All(refunds, e => { Assert.True(e.IsProc); Assert.Equal(1, e.ProcDepth); });
    }

    [Fact]
    public void ArcanePowerThirdPaidCastIsCountedPerMage()
    {
        var (session, first, second) = CreateParty("MAGE", [Hook("A-9-1", 1)], regen: 0);
        foreach (var actor in new[] { first, second })
            EffectEngine.Apply(actor, actor.ActorId, new EffectDefinition("MAGE_ARCANE_POWER_ACTIVE",
                EffectKind.Buff, TimeSpan.FromSeconds(20), 1, EffectStackPolicy.Replace, 0), Now);
        void Cast(CombatActorState actor, string id) => Assert.True(session.Handle(actor.ActorId,
            new UseAbilityCommand(id, "TEST", actor.ActorId), Now).Succeeded);
        Cast(first, "a1"); Cast(first, "a2"); Cast(second, "b1");
        Assert.DoesNotContain(second.ActiveEffects, e => e.Definition.Id == "MAGE_CLEARCASTING");
        Cast(first, "a3");
        Assert.Contains(first.ActiveEffects, e => e.Definition.Id == "MAGE_CLEARCASTING");
        Assert.DoesNotContain(second.ActiveEffects, e => e.Definition.Id == "MAGE_CLEARCASTING");
        Cast(second, "b2"); Cast(second, "b3");
        Assert.Contains(second.ActiveEffects, e => e.Definition.Id == "MAGE_CLEARCASTING");
    }

    [Fact]
    public void ArcherCombatRhythmCountsEachArchersShotsSeparately()
    {
        var (session, first, second) = CreateParty("ARCHER",
            [Hook("M-4-4", 7) with { TargetId = "PHYSICAL_SHOT_RHYTHM", TriggerCount = 3 }], regen: 0);
        void Shot(CombatActorState actor, string id) => Assert.True(session.Handle(actor.ActorId,
            new UseAbilityCommand(id, "TEST", session.Snapshot(actor.ActorId).Enemy.ActorId), Now).Succeeded);
        Shot(first, "a1"); Shot(first, "a2"); Shot(second, "b1");
        Assert.Equal(80, second.CurrentResource);
        Shot(first, "a3");
        Assert.Equal(47, first.CurrentResource);
        Shot(second, "b2"); Shot(second, "b3");
        Assert.Equal(47, second.CurrentResource);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InactiveOwnersRefundCannotBeDrainedByLivingMage(bool flee)
    {
        var (session, first, second) = CreateParty("MAGE",
            [Hook("A-5-4", 50) with { Duration = TimeSpan.FromSeconds(2) }], regen: 0);
        QueueRefund(session, first, 40, Now);
        Assert.True(second.TrySpendResource(50));
        if (flee)
            Assert.True(session.Handle(first.ActorId, new FleeCommand("flee"), Now).Succeeded);
        else
        {
            first.SetCurrentHp(0);
            typeof(CombatSession).GetMethod("ApplyKernelEvents", Private)!.Invoke(session,
                [new[] { new CombatEvent(CombatEventType.ActorDied, Now, first.ActorId,
                    SourceActorId: session.Snapshot().Enemy.ActorId, TargetActorId: first.ActorId) },
                    null, first.ActorId, null, null, null]);
        }
        session.AdvanceTo(Now.AddSeconds(3));
        SyncMage(session, second, Now.AddSeconds(3));
        Assert.Equal(50, second.CurrentResource);
        Assert.Equal(60, first.CurrentResource);
        Assert.DoesNotContain(session.GetEventsAfter(0), e => e.DefinitionId == "A-5-4");
        Assert.False(session.TryAttachParticipant(first.ActorId, Now.AddSeconds(3), out _));
    }

    [Fact]
    public void CombustionCritLimitCannotEndAnotherMagesManaRecoveryWindow()
    {
        var (session, first, second) = CreateParty("MAGE", [Hook("F-6-4", 5)], regen: 0,
            abilityOverride: Attack("MAGE_FIREBALL", true), critical: true);
        foreach (var actor in new[] { first, second })
            EffectEngine.Apply(actor, actor.ActorId, new EffectDefinition("MAGE_COMBUSTION_ACTIVE",
                EffectKind.Buff, TimeSpan.FromSeconds(20), 1, EffectStackPolicy.Replace, 0), Now);
        void Cast(CombatActorState actor, string id) => Assert.True(session.Handle(actor.ActorId,
            new UseAbilityCommand(id, "MAGE_FIREBALL", session.Snapshot(actor.ActorId).Enemy.ActorId), Now).Succeeded);
        Cast(first, "a1"); Cast(first, "a2"); Cast(second, "b1");
        Assert.Contains(second.ActiveEffects, e => e.Definition.Id == "MAGE_COMBUSTION_ACTIVE");
        Cast(first, "a3"); Cast(second, "b2"); Cast(second, "b3");
        Assert.Equal(55, first.CurrentResource);
        Assert.Equal(55, second.CurrentResource);
        Assert.Equal(6, session.GetEventsAfter(0).Count(e => e.DefinitionId == "F-6-4"));
    }

    [Fact]
    public void ReconnectSnapshotAndRepeatedAttachDoNotResetRefundOrTransferItToAnotherMage()
    {
        var (session, first, second) = CreateParty("MAGE",
            [Hook("A-5-4", 50) with { Duration = TimeSpan.FromSeconds(2) }], regen: 0);
        QueueRefund(session, first, 40, Now);
        Assert.True(second.TrySpendResource(50));
        // Reconnect of an active roster member uses its existing snapshot/state;
        // an additional attach is rejected, not a replacement participant.
        Assert.False(session.TryAttachParticipant(first.ActorId, Now.AddSeconds(1), out _));
        session.Snapshot(first.ActorId);
        session.Snapshot(second.ActorId);
        SyncMage(session, second, Now.AddSeconds(2));
        Assert.Equal(50, second.CurrentResource);
        SyncMage(session, first, Now.AddSeconds(2));
        Assert.Equal(80, first.CurrentResource);
        Assert.False(session.TryAttachParticipant(first.ActorId, Now.AddSeconds(2), out _));
        SyncMage(session, first, Now.AddSeconds(3));
        Assert.Single(session.GetEventsAfter(0), e => e.DefinitionId == "A-5-4");
    }

    [Fact]
    public void HotStreakManaDiscountRequiresTwoCritsFromTheSameMage()
    {
        var (session, first, second) = CreateParty("MAGE",
            [Hook("F-8-1", 1) with { Duration = TimeSpan.FromSeconds(10) }], regen: 0,
            abilityOverride: Attack("MAGE_FIREBALL", true), critical: true);
        void Cast(CombatActorState actor, string id) => Assert.True(session.Handle(actor.ActorId,
            new UseAbilityCommand(id, "MAGE_FIREBALL", session.Snapshot(actor.ActorId).Enemy.ActorId), Now).Succeeded);
        Cast(first, "a1"); Cast(second, "b1");
        Assert.DoesNotContain(second.ActiveEffects, e => e.Definition.Id == "MAGE_HOT_STREAK");
        Cast(first, "a2");
        Assert.Contains(first.ActiveEffects, e => e.Definition.Id == "MAGE_HOT_STREAK");
        AbilityDefinition pyroblast = Attack("MAGE_PYROBLAST", true);
        Assert.Equal(10, ComposedCost(session, first, pyroblast, Now));
        Assert.Equal(20, ComposedCost(session, second, pyroblast, Now));
    }

    [Fact]
    public void ColdBloodManaDiscountReadinessBelongsToTheMageWhoUsedIceBlock()
    {
        var iceBlock = new AbilityDefinition("MAGE_ICE_BLOCK", AbilityType.Instant, AbilityTargetType.Self,
            0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, true, "FROST",
            Actions: [new(AbilityActionType.ResourceChange, 0)]);
        var (session, first, second) = CreateParty("MAGE",
            [Hook("I-7-3", 10) with { SecondaryValue = 50, Duration = TimeSpan.FromSeconds(10) }],
            regen: 0, abilityOverride: iceBlock);
        Assert.True(session.Handle(first.ActorId, new UseAbilityCommand("a", iceBlock.Id, first.ActorId), Now).Succeeded);
        Assert.True(session.Handle(second.ActorId, new UseAbilityCommand("b", iceBlock.Id, second.ActorId), Now.AddSeconds(1)).Succeeded);
        SyncMage(session, first, Now.AddSeconds(3));
        var frostbolt = Attack("MAGE_FROSTBOLT", true) with { School = "FROST" };
        Assert.Equal(10, ComposedCost(session, first, frostbolt, Now.AddSeconds(3)));
        Assert.Equal(20, ComposedCost(session, second, frostbolt, Now.AddSeconds(3)));
        SyncMage(session, second, Now.AddSeconds(4));
        Assert.Equal(10, ComposedCost(session, second, frostbolt, Now.AddSeconds(4)));
    }

    private static decimal ComposedCost(CombatSession session, CombatActorState actor,
        AbilityDefinition ability, DateTimeOffset time)
    {
        typeof(CombatSession).GetMethod("ActivatePlayer", Private)!.Invoke(session, [actor.ActorId]);
        return ((AbilityDefinition)typeof(CombatSession).GetMethod("ComposePlayerAbility", Private)!
            .Invoke(session, [ability, time])!).ResourceCost;
    }

    [Theory]
    [InlineData("MAGE")]
    [InlineData("WARRIOR")]
    public void PveEnemyActionSyncDrainsEveryLivingMagesOwnRefundRegardlessOfLastActiveClass(string secondClass)
    {
        var (session, first, second) = CreateParty("MAGE",
            [Hook("A-5-4", 50) with { Duration = TimeSpan.FromSeconds(2) }], regen: 0,
            secondClass: secondClass, activeEnemy: true);
        QueueRefund(session, first, 40, Now);
        session.AdvanceTo(Now.AddSeconds(3));
        Assert.Equal(80, first.CurrentResource);
        Assert.Equal(100, second.CurrentResource);
        CombatEvent refund = Assert.Single(session.GetEventsAfter(0), e => e.DefinitionId == "A-5-4");
        Assert.Equal(first.ActorId, refund.ActorId);
        Assert.Equal(Now.AddSeconds(2), refund.OccurredAtUtc);
    }

    [Fact]
    public void PreparationFocusRecoveryCannotBeConsumedByAnotherArchersTrap()
    {
        AbilityDefinition preparation = new("PREPARATION", AbilityType.Instant, AbilityTargetType.Self,
            0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, false, "PHYSICAL",
            Actions: [new(AbilityActionType.ResourceChange, 0)]);
        AbilityDefinition trap = preparation with
        {
            Id = "FREEZING_TRAP", TargetType = AbilityTargetType.SingleEnemy,
            RuntimeParameters = new Dictionary<string, decimal> { ["normalStunSeconds"] = 3 }
        };
        var (session, first, second) = CreateParty("ARCHER",
            [Hook("S-9-1", 7) with { TargetId = "SURVIVAL_MASTER_PREPARATION" }], regen: 0,
            abilityOverride: preparation, additionalAbilities: [trap]);
        Assert.True(first.TrySpendResource(50));
        Assert.True(second.TrySpendResource(50));
        Assert.True(session.Handle(first.ActorId, new UseAbilityCommand("prepare", preparation.Id, first.ActorId), Now).Succeeded);
        Guid target = session.Snapshot().Enemy.ActorId;
        Assert.True(session.Handle(second.ActorId, new UseAbilityCommand("other-trap", trap.Id, target), Now).Succeeded);
        Assert.Equal(50, second.CurrentResource);
        Assert.True(session.Handle(first.ActorId, new UseAbilityCommand("own-trap", trap.Id, target), Now).Succeeded);
        Assert.Equal(57, first.CurrentResource);
        Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.ResourceChanged && e.DefinitionId == "S-9-1");
    }

    [Theory]
    [InlineData(false, "ARCANE", 40)]
    [InlineData(true, "ARCANE", 40)]
    [InlineData(false, "COMBUSTION", 55)]
    [InlineData(true, "COMBUSTION", 55)]
    [InlineData(false, "HOT_STREAK", 60)]
    [InlineData(true, "HOT_STREAK", 60)]
    public void ResourceWindowCountersPreserveSinglePlayerPveAndHostedArenaParity(
        bool hosted, string window, decimal expectedResource)
    {
        ResolvedTalentEventHook hook = window switch
        {
            "ARCANE" => Hook("A-9-1", 1),
            "COMBUSTION" => Hook("F-6-4", 5),
            _ => Hook("F-8-1", 1) with { Duration = TimeSpan.FromSeconds(10) }
        };
        Fight fight = Create(hosted, "MAGE", 0, [hook],
            abilityOverride: window == "ARCANE" ? null : Attack("MAGE_FIREBALL", true),
            playerStats: CombatStats.Default with { CriticalChance = 100 });
        if (window != "HOT_STREAK")
            EffectEngine.Apply(fight.Player, fight.Player.ActorId,
                new EffectDefinition(window == "ARCANE" ? "MAGE_ARCANE_POWER_ACTIVE" : "MAGE_COMBUSTION_ACTIVE",
                    EffectKind.Buff, TimeSpan.FromSeconds(20), 1, EffectStackPolicy.Replace, 0), Now);
        for (int cast = 0; cast < (window == "HOT_STREAK" ? 2 : 3); cast++)
            Assert.True(fight.Cast($"cast-{cast}", Now));
        Assert.Equal(expectedResource, fight.Player.CurrentResource);
        if (window == "COMBUSTION")
        {
            Assert.DoesNotContain(fight.Player.ActiveEffects, e => e.Definition.Id == "MAGE_COMBUSTION_ACTIVE");
            Assert.Equal(3, fight.Events().Count(e => e.DefinitionId == "F-6-4"));
        }
        else
            Assert.Contains(fight.Player.ActiveEffects, e => e.Definition.Id ==
                (window == "ARCANE" ? "MAGE_CLEARCASTING" : "MAGE_HOT_STREAK"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ColdBloodDiscountRetainsThreeSecondReadinessInPveAndHostedArena(bool hosted)
    {
        var iceBlock = new AbilityDefinition("MAGE_ICE_BLOCK", AbilityType.Instant, AbilityTargetType.Self,
            0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, true, "FROST",
            Actions: [new(AbilityActionType.ResourceChange, 0)]);
        Fight fight = Create(hosted, "MAGE", 0,
            [Hook("I-7-3", 10) with { SecondaryValue = 50, Duration = TimeSpan.FromSeconds(10) }],
            abilityOverride: iceBlock);
        Assert.True(fight.Cast("ice-block", Now));
        fight.Advance(Now.AddSeconds(2));
        fight.Sync(Now.AddSeconds(2));
        Assert.DoesNotContain(fight.Player.ActiveEffects, e => e.Definition.Id == "MAGE_COLD_BLOOD");
        fight.Advance(Now.AddSeconds(3));
        fight.Sync(Now.AddSeconds(3));
        ActiveEffect discount = Assert.Single(fight.Player.ActiveEffects,
            e => e.Definition.Id == "MAGE_COLD_BLOOD");
        Assert.Equal(50, discount.RemainingMagnitude);
        Assert.Equal(100, fight.Player.CurrentResource);
    }

    private static void QueueRefund(CombatSession session, CombatActorState actor, decimal spent, DateTimeOffset time)
    {
        Assert.True(actor.TrySpendResource(spent));
        session.Snapshot(actor.ActorId);
        typeof(CombatSession).GetMethod("ApplyKernelEvents", Private)!.Invoke(session,
            [new[] { new CombatEvent(CombatEventType.ResourceChanged, time, actor.ActorId,
                "MAGE_MANA_SHIELD_EFFECT", -spent, SourceActorId: actor.ActorId, TargetActorId: actor.ActorId) },
                actor.ActorId, actor.ActorId, "MAGE_MANA_SHIELD_EFFECT", null, null]);
    }

    [Fact]
    public void HostedArenaRefundQueuesKeepIndependentDueTimesAndPublishEachRefundOnce()
    {
        var auto = new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0);
        var talents = ResolvedTalentModifiers.Empty with
        { EventHooks = [Hook("A-5-4", 50) with { Duration = TimeSpan.FromSeconds(2) }] };
        var abilities = new Dictionary<string, AbilityDefinition>();
        ArenaFighter Fighter()
        {
            var actor = new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 100, CombatStats.Default);
            var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player, "MAGE", "Fixture",
                "MANA", auto, new HashSet<string>(), CanAutoAttack: false);
            var definition = new CombatPlayerDefinition(Guid.NewGuid(), participant, talents);
            return new(definition.AccountId, actor.ActorId, actor, abilities, auto, TalentModifiers: talents,
                CanAutoAttack: false, PlayerDefinition: definition, BaseAbilities: abilities);
        }
        ArenaFighter first = Fighter(), second = Fighter();
        var arena = new ArenaCombatSession(Guid.NewGuid(), first, second, new SeededGameRandom(1), Now);
        CombatSession Host(ArenaFighter fighter) => (CombatSession)typeof(ArenaCombatSession)
            .GetMethod("MechanicsFor", Private)!.Invoke(arena, [fighter.Actor.ActorId])!;
        void Refund(ArenaFighter fighter, decimal spent, DateTimeOffset time)
        {
            Assert.True(fighter.Actor.TrySpendResource(spent));
            var result = new CombatEvent(CombatEventType.ResourceChanged, time, fighter.Actor.ActorId,
                "MAGE_MANA_SHIELD_EFFECT", -spent, SourceActorId: fighter.Actor.ActorId, TargetActorId: fighter.Actor.ActorId);
            typeof(CombatSession).GetMethod("HandleMechanicsEvents", Private)!.Invoke(Host(fighter),
                [new[] { result }, time, result.DefinitionId, fighter.Actor.ActorId, fighter.Actor.ActorId]);
        }
        DateTimeOffset? Due(ArenaFighter fighter) => (DateTimeOffset?)typeof(CombatSession)
            .GetProperty("NextMechanicsDueAt", Private)!.GetValue(Host(fighter));
        Refund(first, 20, Now);
        arena.AdvanceTo(Now.AddSeconds(1));
        Refund(first, 40, Now.AddSeconds(1));
        Refund(second, 10, Now.AddSeconds(1));
        Assert.Equal(Now.AddSeconds(2), Due(first));
        Assert.Equal(Now.AddSeconds(3), Due(second));
        arena.AdvanceTo(Now.AddSeconds(2));
        Assert.Equal(50, first.Actor.CurrentResource);
        Assert.Equal(90, second.Actor.CurrentResource);
        Assert.Equal(Now.AddSeconds(3), Due(first));
        arena.AdvanceTo(Now.AddSeconds(3));
        arena.AdvanceTo(Now.AddSeconds(4));
        Assert.Equal(70, first.Actor.CurrentResource);
        Assert.Equal(95, second.Actor.CurrentResource);
        Assert.Null(Due(first)); Assert.Null(Due(second));
        Assert.Equal(3, arena.GetEventsAfter(0).Count(e => e.DefinitionId == "A-5-4"));
    }

    [Fact]
    public void PveRefundDoesNotIntroduceANewSchedulerWakeupOrDrainWithoutClassSync()
    {
        var (session, first, _) = CreateParty("MAGE",
            [Hook("A-5-4", 50) with { Duration = TimeSpan.FromSeconds(2) }], regen: 0);
        DateTimeOffset? due = session.NextDueAtUtc;
        QueueRefund(session, first, 40, Now);
        Assert.Equal(due, session.NextDueAtUtc);
        session.AdvanceTo(Now.AddSeconds(3));
        Assert.Equal(60, first.CurrentResource);
        SyncMage(session, first, Now.AddSeconds(3));
        Assert.Equal(80, first.CurrentResource);
    }

    private static void SyncMage(CombatSession session, CombatActorState actor, DateTimeOffset time)
    {
        typeof(CombatSession).GetMethod("ActivatePlayer", Private)!.Invoke(session, [actor.ActorId]);
        typeof(CombatSession).GetMethod("SyncMageConditionalEffects", Private)!.Invoke(session, [time]);
    }

    private static (CombatSession Session, CombatActorState First, CombatActorState Second) CreateParty(
        string classId, ResolvedTalentEventHook[] hooks, decimal regen = 4,
        AbilityDefinition? abilityOverride = null, bool critical = false,
        string? secondClass = null, bool activeEnemy = false, AbilityDefinition[]? additionalAbilities = null)
    {
        var auto = new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0);
        int participantIndex = 0;
        CombatParticipantDefinition Participant(bool enemy = false) => new(
            new CombatActorState(Guid.Parse($"00000000-0000-0000-0000-{++participantIndex:D12}"), 1000, 1000, 100, 100,
                CombatStats.Default with { CriticalChance = critical && !enemy ? 100 : 0 }),
            enemy ? CombatActorKind.Monster : CombatActorKind.Player, enemy ? "PASSIVE" : classId,
            "Fixture", classId == "ARCHER" ? "FOCUS" : "MANA", auto,
            enemy && activeEnemy ? new HashSet<string> { "ENEMY_TICK" } :
                new HashSet<string>(new[] { abilityOverride?.Id ?? "TEST" }.Concat(additionalAbilities?.Select(a => a.Id) ?? [])),
            ResourceRegenPerSecond: enemy ? 0 : regen, CanAutoAttack: false);
        var first = Participant(); var second = Participant(); var enemy = Participant(true);
        if (activeEnemy) enemy = enemy with { AutoAttack = auto with { Interval = TimeSpan.FromSeconds(1) } };
        if (secondClass is not null) second = second with { DefinitionId = secondClass };
        AbilityDefinition ability = abilityOverride ?? (classId == "ARCHER" ? Attack("TEST", false) : new("TEST",
            AbilityType.Instant, AbilityTargetType.Self, 20, TimeSpan.Zero, TimeSpan.Zero, false,
            GlobalCooldownCategory.None, true, "ARCANE", Actions: [new(AbilityActionType.ResourceChange, 0)]));
        var talents = ResolvedTalentModifiers.Empty with { EventHooks = hooks };
        var abilities = new Dictionary<string, AbilityDefinition> { [ability.Id] = ability };
        foreach (AbilityDefinition extra in additionalAbilities ?? []) abilities.Add(extra.Id, extra);
        if (activeEnemy)
            abilities.Add("ENEMY_TICK", new("ENEMY_TICK", AbilityType.Instant, AbilityTargetType.Self, 0,
                TimeSpan.FromSeconds(1), TimeSpan.Zero, false, GlobalCooldownCategory.None, false, "PHYSICAL",
                Actions: [new(AbilityActionType.ResourceChange, 0)]));
        var session = new CombatSession(Guid.NewGuid(), first, enemy,
            abilities, new MonsterAiProfile("FIXTURE", activeEnemy ? ["ENEMY_TICK"] : []),
            talents, new SeededGameRandom(1), Now, additionalPlayers: [new(Guid.NewGuid(), second, talents)]);
        return (session, first.Actor, second.Actor);
    }

    private sealed record Fight(CombatActorState Player, CombatActorState Opponent,
        Func<string, DateTimeOffset, bool> Cast, Action<DateTimeOffset> Advance,
        Func<IReadOnlyList<CombatEvent>> Events, Action<CombatEvent> Publish, Action<DateTimeOffset> Sync);

    private static Fight Create(bool hosted, string classId, decimal regen,
        ResolvedTalentEventHook[]? hooks = null, decimal? resourceAction = null,
        AbilityDefinition? abilityOverride = null, CombatStats? playerStats = null, CombatStats? opponentStats = null)
    {
        var player = new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 100, playerStats ?? CombatStats.Default);
        var opponent = new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 100, opponentStats ?? CombatStats.Default);
        AbilityDefinition ability = abilityOverride ?? new("TEST", AbilityType.Instant, AbilityTargetType.Self, 20,
            TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, true, "ARCANE",
            Actions: [new(AbilityActionType.ResourceChange, resourceAction ?? 0)]);
        var abilities = new Dictionary<string, AbilityDefinition> { [ability.Id] = ability };
        var auto = new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0);
        var talents = ResolvedTalentModifiers.Empty with { EventHooks = hooks ?? [] };
        Guid targetId = ability.TargetType == AbilityTargetType.Self ? player.ActorId : opponent.ActorId;
        CombatParticipantDefinition Participant(CombatActorState actor, bool owner) => new(actor,
            owner || hosted ? CombatActorKind.Player : CombatActorKind.Monster,
            owner ? classId : "MAGE", "Fixture", classId == "WARRIOR" ? "RAGE" : classId == "ARCHER" ? "FOCUS" : "MANA",
            auto, abilities.Keys.ToHashSet(), ResourceRegenPerSecond: owner ? regen : 0, CanAutoAttack: false);
        if (!hosted)
        {
            var session = new CombatSession(Guid.NewGuid(), Participant(player, true), Participant(opponent, false),
                abilities, new MonsterAiProfile("PASSIVE", []), talents, new SeededGameRandom(1), Now);
            return new(player, opponent,
                (id, time) => session.Handle(player.ActorId, new UseAbilityCommand(id, ability.Id, targetId), time).Succeeded,
                time => session.AdvanceTo(time), () => session.GetEventsAfter(0),
                e => typeof(CombatSession).GetMethod("ApplyKernelEvents", Private)!.Invoke(session,
                    [new[] { e }, e.SourceActorId, e.TargetActorId, e.DefinitionId, null, null]),
                time => typeof(CombatSession).GetMethod("SyncMageConditionalEffects", Private)!.Invoke(session, [time]));
        }
        ArenaFighter Fighter(CombatActorState actor, bool owner)
        {
            var definition = new CombatPlayerDefinition(Guid.NewGuid(), Participant(actor, owner),
                owner ? talents : ResolvedTalentModifiers.Empty);
            return new(definition.AccountId, actor.ActorId, actor, abilities, auto,
                TalentModifiers: definition.TalentModifiers, ResourceRegenPerSecond: owner ? regen : 0,
                CanAutoAttack: false, PlayerDefinition: definition, BaseAbilities: abilities);
        }
        ArenaFighter first = Fighter(player, true), second = Fighter(opponent, false);
        var arena = new ArenaCombatSession(Guid.NewGuid(), first, second, new SeededGameRandom(1), Now);
        var host = (CombatSession)typeof(ArenaCombatSession).GetMethod("MechanicsFor", Private)!
            .Invoke(arena, [player.ActorId])!;
        return new(player, opponent,
            (id, time) => arena.UseAbility(first.AccountId, id, ability.Id, targetId, time).Succeeded,
            arena.AdvanceTo, () => arena.GetEventsAfter(0),
            e => typeof(CombatSession).GetMethod("HandleMechanicsEvents", Private)!.Invoke(host,
                [new[] { e }, e.OccurredAtUtc, e.DefinitionId, e.SourceActorId, e.TargetActorId]),
            time => typeof(CombatSession).GetMethod("SyncMageConditionalEffects", Private)!.Invoke(host, [time]));
    }
}
