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

public sealed class CombatAbilityRoutingCharacterizationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData(false, AbilityType.Instant)]
    [InlineData(true, AbilityType.Instant)]
    [InlineData(false, AbilityType.Casted)]
    [InlineData(true, AbilityType.Casted)]
    public void StartedCallbacksConsumeFireBuffsBeforeClearcastingAndAfterglow(bool hosted, AbilityType type)
    {
        AbilityDefinition spell = Spell("MAGE_FIREBALL", type);
        Fight fight = Create(hosted, "MAGE", spell, [Hook("A-6-2", "ON_ABILITY_USED", 50)]);
        foreach (string id in new[] { "MAGE_KINDLING_FLAME", "MAGE_HEAT_DISCOUNT", "MAGE_CLEARCASTING" })
            Buff(fight.Player, id);
        Assert.True(fight.Cast("cast", Now));
        Assert.Equal(["EffectRemoved:MAGE_KINDLING_FLAME", "EffectRemoved:MAGE_HEAT_DISCOUNT",
            "EffectRemoved:MAGE_CLEARCASTING", "EffectApplied:MAGE_CLEARCASTING_REGEN"],
            fight.Events().Where(e => e.DefinitionId is "MAGE_KINDLING_FLAME" or "MAGE_HEAT_DISCOUNT"
                or "MAGE_CLEARCASTING" or "MAGE_CLEARCASTING_REGEN").Select(Label));
        Assert.Equal(100, fight.Player.CurrentResource);
        if (type == AbilityType.Casted)
        {
            Assert.DoesNotContain(fight.Events(), e => e.Type == CombatEventType.AbilityCompleted);
            fight.Advance(Now.AddSeconds(2));
            Assert.Single(fight.Events(), e => e.Type == CombatEventType.AbilityCompleted);
            Assert.Single(fight.Events(), e => e.DefinitionId == "MAGE_CLEARCASTING_REGEN");
        }
    }

    [Theory]
    [InlineData(false, AbilityType.Instant)]
    [InlineData(true, AbilityType.Instant)]
    [InlineData(false, AbilityType.Casted)]
    [InlineData(true, AbilityType.Casted)]
    public void ResolvedCallbacksFollowPrimaryAndGenericEventsThenPyromancerThenMage(bool hosted, AbilityType type)
    {
        Fight fight = Create(hosted, "MAGE", Spell("MAGE_SCORCH", type),
            [Hook("F-3-2", "ON_ABILITY_USED", 5), Hook("A-1-2", "ON_ABILITY_USED", 100),
                Hook("TEST_COMPLETE", TalentModifierKeys.OnAbilityUsed, 1)]);
        Assert.True(fight.Cast("cast", Now));
        if (type == AbilityType.Casted)
        {
            Assert.Equal(1000, fight.Opponent.CurrentHp);
            Assert.DoesNotContain(fight.Events(), e => e.Type == CombatEventType.EffectApplied);
            fight.Advance(Now.AddSeconds(2));
        }
        Assert.Equal(["DamageDealt:MAGE_SCORCH", "AbilityCompleted:MAGE_SCORCH",
            "ResourceChanged:TEST_COMPLETE", "EffectApplied:MAGE_FIRE_VULNERABILITY",
            "EffectApplied:MAGE_CLEARCASTING"], fight.Events().Where(e =>
                e.Type is CombatEventType.DamageDealt or CombatEventType.AbilityCompleted
                || e.DefinitionId is "TEST_COMPLETE" or "MAGE_FIRE_VULNERABILITY" or "MAGE_CLEARCASTING").Select(Label));
        Assert.Equal(990, fight.Opponent.CurrentHp);
        Assert.Equal(81, fight.Player.CurrentResource);
        Assert.Equal(type == AbilityType.Casted ? Now.AddSeconds(2) : Now,
            Assert.Single(fight.Events(), e => e.DefinitionId == "MAGE_FIRE_VULNERABILITY").OccurredAtUtc);
        long count = fight.Events().Count;
        Assert.False(fight.Cast("cast", type == AbilityType.Casted ? Now.AddSeconds(2) : Now));
        Assert.Equal(count, fight.Events().Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NextAttackModifierResolvesWarriorHookOnceAfterArmingWithoutAnotherSpend(bool hosted)
    {
        AbilityDefinition ability = Spell("TEST_NEXT_ATTACK", AbilityType.NextAttackModifier) with
        {
            IsSpell = false, School = "PHYSICAL", TargetType = AbilityTargetType.Self,
            Actions = [new AbilityActionDefinition(AbilityActionType.ApplyEffect,
                Effect: new EffectDefinition("TEST_ARMED", EffectKind.Buff, TimeSpan.FromSeconds(10),
                    1, EffectStackPolicy.Replace, 1))]
        };
        Fight fight = Create(hosted, "WARRIOR", ability, [Hook("B-2-4", "ON_ABILITY_USED", 10)]);
        Assert.True(fight.Cast("arm", Now));
        Assert.Equal(["EffectApplied:TEST_ARMED", "AbilityCompleted:TEST_NEXT_ATTACK",
            "EffectApplied:BERSERKER_MOMENTUM_ATTACK_SPEED"], fight.Events().Where(e =>
                e.Type is CombatEventType.EffectApplied or CombatEventType.AbilityCompleted).Select(Label));
        Assert.Equal(80, fight.Player.CurrentResource);
        Assert.False(fight.Cast("cooldown", Now.AddSeconds(1)));
        Assert.Equal(80, fight.Player.CurrentResource);
        Assert.Single(fight.Player.ActiveEffects, e => e.Definition.Id == "TEST_ARMED");
        Assert.Single(fight.Events(), e => e.DefinitionId == "BERSERKER_MOMENTUM_ATTACK_SPEED");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArcherStartedConsumptionPrecedesResolvedExposedDefenseRefresh(bool hosted)
    {
        AbilityDefinition shot = Spell("PIERCING_ARROW", AbilityType.Instant) with
        {
            IsSpell = false, School = "PHYSICAL",
            Actions = [new AbilityActionDefinition(AbilityActionType.Damage, 10,
                CanMiss: false, CanCrit: false, CanDodge: false)]
        };
        Fight fight = Create(hosted, "ARCHER", shot,
            [Hook("M-4-3", "ON_ABILITY_USED", 20) with { TargetId = "PIERCING_EXPOSED_DEFENSE", TriggerCount = 2 }]);
        Buff(fight.Player, "ARCHER_EXPOSED_DEFENSE");
        Buff(fight.Player, "ARCHER_COORDINATION");
        Buff(fight.Player, "ARCHER_MASTER_TACTICIAN");
        Assert.True(fight.Cast("shot", Now));
        Assert.Equal(["EffectRemoved:ARCHER_EXPOSED_DEFENSE", "EffectRemoved:ARCHER_COORDINATION",
            "EffectRemoved:ARCHER_MASTER_TACTICIAN", "EffectApplied:ARCHER_EXPOSED_DEFENSE"],
            fight.Events().Where(e => e.DefinitionId is "ARCHER_EXPOSED_DEFENSE"
                or "ARCHER_COORDINATION" or "ARCHER_MASTER_TACTICIAN").Select(Label));
        Assert.Equal(988, fight.Opponent.CurrentHp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedAbilityDoesNotConsumeStartedBuffsOrRunResolvedHooks(bool hosted)
    {
        Fight fight = Create(hosted, "MAGE", Spell("MAGE_FIREBALL", AbilityType.Instant) with { ResourceCost = 200 },
            [Hook("A-1-2", "ON_ABILITY_USED", 100)]);
        Buff(fight.Player, "MAGE_HEAT_DISCOUNT");
        Assert.False(fight.Cast("rejected", Now));
        Assert.Contains(fight.Player.ActiveEffects, e => e.Definition.Id == "MAGE_HEAT_DISCOUNT");
        Assert.DoesNotContain(fight.Events(), e => e.Type is CombatEventType.AbilityStarted or CombatEventType.AbilityCompleted
            || e.DefinitionId == "MAGE_CLEARCASTING");
        Assert.Equal(100, fight.Player.CurrentResource);
        Assert.Equal(1000, fight.Opponent.CurrentHp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedCastNeverDispatchesResolvedCallbacks(bool hosted)
    {
        Fight fight = Create(hosted, "MAGE", Spell("MAGE_SCORCH", AbilityType.Casted),
            [Hook("F-3-2", "ON_ABILITY_USED", 5), Hook("A-1-2", "ON_ABILITY_USED", 100)]);
        Assert.True(fight.Cast("cast", Now));
        Assert.True(fight.Interrupt(Now.AddSeconds(1)));
        fight.Advance(Now.AddSeconds(2));
        Assert.Contains(fight.Events(), e => e.Type == CombatEventType.AbilityInterrupted);
        Assert.DoesNotContain(fight.Events(), e => e.Type == CombatEventType.AbilityCompleted && e.DefinitionId == "MAGE_SCORCH"
            || e.Type == CombatEventType.DamageDealt
            || e.DefinitionId is "MAGE_CLEARCASTING" or "MAGE_FIRE_VULNERABILITY");
        Assert.Equal(80, fight.Player.CurrentResource);
        Assert.Equal(1000, fight.Opponent.CurrentHp);
    }

    private static string Label(CombatEvent e) => $"{e.Type}:{e.DefinitionId}";

    private static AbilityDefinition Spell(string id, AbilityType type) => new(id, type,
        AbilityTargetType.SingleEnemy, 20, TimeSpan.FromSeconds(5),
        type == AbilityType.Casted ? TimeSpan.FromSeconds(2) : TimeSpan.Zero, false,
        GlobalCooldownCategory.None, true, "FIRE",
        Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10, DamageType.Magical,
            CanMiss: false, CanCrit: false, CanDodge: false)]);

    private static ResolvedTalentEventHook Hook(string id, string key, decimal value) =>
        new(id, key, 1, value, null, TimeSpan.Zero, false, Duration: TimeSpan.FromSeconds(5));

    private static void Buff(CombatActorState actor, string id) => EffectEngine.Apply(actor, actor.ActorId,
        new EffectDefinition(id, EffectKind.Buff, TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 10), Now);

    private sealed record Fight(CombatActorState Player, CombatActorState Opponent,
        Func<string, DateTimeOffset, bool> Cast,
        Action<DateTimeOffset> Advance, Func<IReadOnlyList<CombatEvent>> Events,
        Func<DateTimeOffset, bool> Interrupt);

    private static Fight Create(bool hosted, string classId, AbilityDefinition ability, ResolvedTalentEventHook[] hooks)
    {
        AbilityDefinition interrupt = new("TEST_INTERRUPT", AbilityType.Instant, AbilityTargetType.SingleEnemy,
            0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, true, "ARCANE",
            Actions: [new AbilityActionDefinition(AbilityActionType.Interrupt, InterruptLockout: TimeSpan.Zero)]);
        var abilities = new Dictionary<string, AbilityDefinition> { [ability.Id] = ability, [interrupt.Id] = interrupt };
        var talents = ResolvedTalentModifiers.Empty with { EventHooks = hooks };
        var player = new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 100,
            CombatStats.Default with { Accuracy = 100 });
        var target = new CombatActorState(Guid.NewGuid(), 1000, 1000, 100, 100, CombatStats.Default);
        var auto = new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0);
        CombatParticipantDefinition Participant(CombatActorState actor, bool owner) => new(actor,
            owner || hosted ? CombatActorKind.Player : CombatActorKind.Monster,
            owner ? classId : "MAGE", "Test", classId switch { "WARRIOR" => "RAGE", "ARCHER" => "FOCUS", _ => "MANA" },
            auto, abilities.Keys.ToHashSet(), CanAutoAttack: false);
        Guid targetId = ability.TargetType == AbilityTargetType.Self ? player.ActorId : target.ActorId;
        if (hosted)
        {
            ArenaFighter Fighter(CombatActorState actor, bool owner)
            {
                var definition = new CombatPlayerDefinition(Guid.NewGuid(), Participant(actor, owner),
                    owner ? talents : ResolvedTalentModifiers.Empty);
                return new ArenaFighter(definition.AccountId, actor.ActorId, actor, abilities, auto,
                    CanAutoAttack: false, TalentModifiers: definition.TalentModifiers,
                    PlayerDefinition: definition, BaseAbilities: abilities);
            }
            ArenaFighter first = Fighter(player, true), second = Fighter(target, false);
            var arena = new ArenaCombatSession(Guid.NewGuid(), first, second, new SeededGameRandom(1), Now);
            return new Fight(player, target,
                (id, now) => arena.UseAbility(first.AccountId, id, ability.Id, targetId, now).Succeeded,
                arena.AdvanceTo, () => arena.GetEventsAfter(0),
                now => arena.UseAbility(second.AccountId, "interrupt", interrupt.Id, player.ActorId, now).Succeeded);
        }
        var session = new CombatSession(Guid.NewGuid(), Participant(player, true), Participant(target, false),
            abilities, new MonsterAiProfile("PASSIVE", []), talents, new SeededGameRandom(1), Now);
        return new Fight(player, target,
            (id, now) => session.Handle(player.ActorId, new UseAbilityCommand(id, ability.Id, targetId), now).Succeeded,
            now => session.AdvanceTo(now), () => session.GetEventsAfter(0),
            now =>
            {
                // The defender's cast runtime is private; use the existing engine boundary.
                CombatRuntimeState playerRuntime = (CombatRuntimeState)typeof(CombatSession)
                    .GetProperty("_playerRuntime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .GetValue(session)!;
                AbilityExecutionResult result = AbilityEngine.Interrupt(playerRuntime, now, TimeSpan.Zero);
                typeof(CombatSession).GetMethod("ApplyKernelEvents", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(session, [result.Events, target.ActorId, player.ActorId, ability.Id, null, null]);
                return result.Succeeded;
            });
    }
}
