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
