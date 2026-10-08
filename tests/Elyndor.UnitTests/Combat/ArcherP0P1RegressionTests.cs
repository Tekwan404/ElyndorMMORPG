using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class ArcherP0P1RegressionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void TrueshotAuraGivesOwnerFivePercentAndEligibleAlliesThreePercent()
    {
        CombatParticipantDefinition owner = Player("ARCHER");
        CombatParticipantDefinition warrior = Player("WARRIOR");
        CombatParticipantDefinition paladin = Player("PALADIN");
        CombatParticipantDefinition mage = Player("MAGE");
        CombatParticipantDefinition enemy = Monster();
        var talents = ResolvedTalentModifiers.Empty with
        {
            EventHooks = [Hook("M-6-1", TalentModifierKeys.OnPartyEvent, "TRUESHOT_AURA", 5, 3)]
        };
        var session = new CombatSession(Guid.NewGuid(), owner, enemy, 
            new Dictionary<string, AbilityDefinition>(), new MonsterAiProfile("IDLE", []),
            talents, Random(), Now, additionalPlayers:
            [
                new CombatPlayerDefinition(Guid.NewGuid(), warrior, ResolvedTalentModifiers.Empty),
                new CombatPlayerDefinition(Guid.NewGuid(), paladin, ResolvedTalentModifiers.Empty),
                new CombatPlayerDefinition(Guid.NewGuid(), mage, ResolvedTalentModifiers.Empty)
            ]);
        session.AdvanceTo(Now.AddSeconds(1));

        decimal AttackPower(CombatParticipantDefinition player) =>
            EffectEngine.CalculateStat(player.Actor, EffectStat.AttackPower, 100m, Now.AddSeconds(1));

        Assert.Equal(105m, AttackPower(owner));
        Assert.Equal(103m, AttackPower(warrior));
        Assert.Equal(103m, AttackPower(paladin));
        Assert.Equal(100m, AttackPower(mage));
        Assert.Single(owner.Actor.ActiveEffects, effect => effect.Definition.Id == "ARCHER_TRUESHOT_AURA");
    }

    [Fact]
    public void UnstoppablePackRejectsAllFiveControlsWithoutResettingCommandCooldown()
    {
        var (session, _, companion, enemy) = CreateCompanionSession(
            Hook("B-8-3", TalentModifierKeys.OnAbilityUsed, "BESTIAL_WRATH_UNSTOPPABLE", 1));
        Use(session, "COMMAND_ATTACK", enemy.Actor.ActorId, 0);
        Use(session, "BESTIAL_WRATH", companion.Actor.ActorId, 1);

        foreach (EffectKind kind in new[]
                 { EffectKind.Stun, EffectKind.Silence, EffectKind.Root, EffectKind.Fear, EffectKind.Disarm })
        {
            IReadOnlyList<CombatEvent> events = EffectEngine.Apply(
                companion.Actor, enemy.Actor.ActorId,
                new EffectDefinition("TEST_CONTROL_" + kind, kind, TimeSpan.FromSeconds(2),
                    1, EffectStackPolicy.Replace, 0),
                Now.AddSeconds(2));
            Assert.Contains(events, item => item.Type == CombatEventType.EffectImmune);
        }

        CombatCommandResult command = session.Handle(
            new UseAbilityCommand("command-again", "COMMAND_ATTACK", enemy.Actor.ActorId),
            Now.AddSeconds(2));
        Assert.False(command.Succeeded);
    }

    [Fact]
    public void PrimalCommandStillResetsCommandDuringBestialWrath()
    {
        var (session, _, companion, enemy) = CreateCompanionSession(
            Hook("B-7-2", TalentModifierKeys.OnAbilityUsed, "PRIMAL_COMMAND", 50));
        Use(session, "COMMAND_ATTACK", enemy.Actor.ActorId, 0);
        Use(session, "BESTIAL_WRATH", companion.Actor.ActorId, 1);
        Use(session, "COMMAND_ATTACK", enemy.Actor.ActorId, 2);
    }

    [Fact]
    public void ImprovedMendPetDispelsAtCompletionNotAtCast()
    {
        var (session, _, companion, enemy) = CreateCompanionSession(
            Hook("B-3-1", TalentModifierKeys.OnAbilityUsed, "MEND_PET_BONUS", 20));
        companion.Actor.ApplyDamage(200);
        EffectEngine.Apply(companion.Actor, enemy.Actor.ActorId, 
            new EffectDefinition("TEST_CURSE", EffectKind.Debuff, TimeSpan.FromSeconds(15),
                1, EffectStackPolicy.Replace, 0), Now);
        Use(session, "MEND_PET", companion.Actor.ActorId, 1);
        Assert.Contains(companion.Actor.ActiveEffects, effect => effect.Definition.Id == "TEST_CURSE");
        session.AdvanceTo(Now.AddSeconds(5));
        Assert.Contains(companion.Actor.ActiveEffects, effect => effect.Definition.Id == "TEST_CURSE");
        session.AdvanceTo(Now.AddSeconds(6));
        Assert.DoesNotContain(companion.Actor.ActiveEffects, effect => effect.Definition.Id == "TEST_CURSE");
    }

    private static CombatCommandResult Use(CombatSession session, string ability, Guid target, int second)
    {
        CombatCommandResult result = session.Handle(
            new UseAbilityCommand($"{ability}-{second}", ability, target), Now.AddSeconds(second));
        Assert.True(result.Succeeded, result.ErrorCode);
        return result;
    }

    private static ResolvedTalentEventHook Hook(
        string id, string key, string target, decimal value, decimal secondary = 0) =>
        new(id, key, 1, value, target, TimeSpan.Zero, false, secondary);

    private static (CombatSession Session, CombatParticipantDefinition Owner,
        CombatParticipantDefinition Companion, CombatParticipantDefinition Enemy)
        CreateCompanionSession(params ResolvedTalentEventHook[] hooks)
    {
        CombatParticipantDefinition owner = Player("ARCHER", ["COMMAND_ATTACK", "BESTIAL_WRATH", "MEND_PET"]);
        CombatParticipantDefinition companion = new(
            Actor(), CombatActorKind.Companion, "ARCHER_STARTER_PREDATOR", "Pet", "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(100), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal), CanAutoAttack: false);
        CombatParticipantDefinition enemy = Monster();
        var abilities = new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
        {
            ["COMMAND_ATTACK"] = new(
                "COMMAND_ATTACK", AbilityType.Instant, AbilityTargetType.SingleEnemy,
                20, TimeSpan.FromSeconds(10), TimeSpan.Zero, false, GlobalCooldownCategory.None,
                false, "PHYSICAL", Actions: [],
                RuntimeParameters: new Dictionary<string, decimal>
                {
                    ["predatorDamageMultiplier"] = 1.5m,
                    ["predatorBleedPercent"] = 0,
                    ["predatorBleedDurationSeconds"] = 3
                }),
            ["BESTIAL_WRATH"] = new(
                "BESTIAL_WRATH", AbilityType.Instant, AbilityTargetType.Self,
                0, TimeSpan.FromSeconds(60), TimeSpan.Zero, false, GlobalCooldownCategory.None,
                false, "PHYSICAL", Actions: [],
                RuntimeParameters: new Dictionary<string, decimal>
                {
                    ["durationSeconds"] = 12,
                    ["companionDamageMultiplier"] = 1.35m,
                    ["companionAttackSpeedMultiplier"] = 1.2m
                }),
            ["MEND_PET"] = new(
                "MEND_PET", AbilityType.Instant, AbilityTargetType.Self,
                20, TimeSpan.FromSeconds(20), TimeSpan.Zero, false, GlobalCooldownCategory.None,
                false, "PHYSICAL", Actions: [],
                RuntimeParameters: new Dictionary<string, decimal>
                {
                    ["healPercent"] = 20,
                    ["durationSeconds"] = 5,
                    ["tickSeconds"] = 1
                })
        };
        var talents = ResolvedTalentModifiers.Empty with { EventHooks = hooks };
        var session = new CombatSession(Guid.NewGuid(), owner, enemy,
            abilities, new MonsterAiProfile("IDLE", []), talents, Random(), Now,
            companion: companion);
        return (session, owner, companion, enemy);
    }

    private static CombatParticipantDefinition Player(string classId, string[]? abilities = null) =>
        new(Actor(), CombatActorKind.Player, classId, "Player", classId == "ARCHER" ? "FOCUS" : "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(100), 0, 0, 0),
            (abilities ?? []).ToHashSet(StringComparer.Ordinal), CanAutoAttack: false);

    private static CombatParticipantDefinition Monster() =>
        new(Actor(), CombatActorKind.Monster, "TEST_MONSTER", "Target", "NONE",
            new AutoAttackProfile(TimeSpan.FromSeconds(100), 0, 0, 0),
            new HashSet<string>(StringComparer.Ordinal), CanAutoAttack: false);

    private static CombatActorState Actor() =>
        new(Guid.NewGuid(), 10000, 10000, 100, 100,
            CombatStats.Default with { AttackPower = 100, Accuracy = 100, CriticalChance = 0 });

    private static IGameRandom Random() => new SequenceGameRandom(Enumerable.Repeat(0.99m, 200).ToArray());
}
