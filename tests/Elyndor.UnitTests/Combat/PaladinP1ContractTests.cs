using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class PaladinP1ContractTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData(true, "ONE_HAND_SWORD", 106)]
    [InlineData(false, "ONE_HAND_SWORD", 100)]
    [InlineData(true, "TWO_HAND_SWORD", 100)]
    [InlineData(true, null, 100)]
    public void SpecializationRequiresCapturedShieldAndOneHandedWeapon(bool shield, string? weapon, decimal expected)
    {
        var (session, player, _, enemy) = Party([Hook("P-6-2", 6, secondary: 3)], shield, weapon);
        Assert.True(session.Handle(player.ActorId, new UseAbilityCommand("hit", "STRIKE", enemy.ActorId), Now).Succeeded);
        Assert.Equal(expected, 10000 - enemy.CurrentHp);
        Assert.Equal(expected == 106 ? 103 : 100,
            EffectEngine.CalculateStat(player, EffectStat.Accuracy, 100, Now));
    }

    [Fact]
    public void MissedJudgementDoesNotApplySupportMarks()
    {
        var (session, player, ally, enemy) = Party([Hook("H-3-3", 2, duration: 12, icd: 2)],
            judgementCanMiss: true, accuracy: 95);
        session.Handle(player.ActorId, new UseAbilityCommand("missed-mark", "JUDGEMENT", enemy.ActorId), Now);
        Assert.DoesNotContain(enemy.ActiveEffects, effect => effect.Definition.Id == "PALADIN_H_3_3");
        session.Handle(ally.ActorId, new UseAbilityCommand("unmarked-hit", "STRIKE", enemy.ActorId), Now);
        Assert.Equal(50, ally.CurrentResource);
    }

    [Fact]
    public void MultiplePaladinsCannotBypassTheBeneficiaryCooldown()
    {
        var (session, player, ally, enemy) = Party([Hook("H-3-3", 2, duration: 12, icd: 2)], allyPaladin: true);
        session.Handle(player.ActorId, new UseAbilityCommand("mark-one", "JUDGEMENT", enemy.ActorId), Now);
        session.Handle(ally.ActorId, new UseAbilityCommand("mark-two", "JUDGEMENT", enemy.ActorId), Now);
        Assert.Equal(2, enemy.ActiveEffects.Count(effect => effect.Definition.Id == "PALADIN_H_3_3"));
        session.Handle(player.ActorId, new UseAbilityCommand("hit", "STRIKE", enemy.ActorId), Now);
        Assert.Equal(52, player.CurrentResource);
        session.Handle(player.ActorId, new UseAbilityCommand("during-icd", "STRIKE", enemy.ActorId), Now.AddSeconds(1));
        Assert.Equal(52, player.CurrentResource);
    }

    [Fact]
    public void FullyAbsorbedHitsStillApplyJudgementMarksAndTriggerSupport()
    {
        var (session, player, ally, enemy) = Party([
            Hook("H-3-3", 2, duration: 12, icd: 2), Hook("H-7-1", 2, duration: 12, icd: 2)]);
        EffectEngine.Apply(enemy, enemy.ActorId, new("SHIELD", EffectKind.Shield,
            TimeSpan.FromSeconds(20), 1, EffectStackPolicy.Replace, 1000), Now);
        session.Handle(player.ActorId, new UseAbilityCommand("absorbed-mark", "JUDGEMENT", enemy.ActorId), Now);
        Assert.Contains(enemy.ActiveEffects, effect => effect.Definition.Id == "PALADIN_H_3_3");
        session.Handle(ally.ActorId, new UseAbilityCommand("absorbed-hit", "STRIKE", enemy.ActorId), Now);
        Assert.Equal(10000, enemy.CurrentHp);
        Assert.Equal(52, ally.CurrentHp);
        Assert.Equal(52, ally.CurrentResource);
    }

    [Fact]
    public void ThreatBonusesApplyOnlyToTheirAuthoredDamageSources()
    {
        var (session, player, _, enemy) = Party([
            Hook("R-5-2", 9, secondary: 30), Hook("P-3-4", 20, secondary: 20, rank: 2)]);
        decimal Threat() => session.GetThreatSnapshot(player.ActorId, Now)!.Entries
            .Single(entry => entry.ActorId == player.ActorId).Threat;
        decimal before = Threat();
        session.Handle(player.ActorId, new UseAbilityCommand("ret", "CRUSADER_STRIKE", enemy.ActorId), Now);
        Assert.Equal(70, Threat() - before);
        before = Threat();
        session.Handle(player.ActorId, new UseAbilityCommand("holy", "HOLY_SHOCK_OFFENSIVE", enemy.ActorId), Now);
        Assert.Equal(100, Threat() - before);
        before = Threat();
        session.Handle(player.ActorId, new UseAbilityCommand("ground", "CONSECRATION", enemy.ActorId), Now);
        session.AdvanceTo(Now.AddSeconds(1));
        decimal damage = session.GetEventsAfter(0).Where(e => e.Type == CombatEventType.DamageDealt
            && e.DefinitionId == "PALADIN_CONSECRATION_DAMAGE").Sum(e => e.Amount);
        Assert.True(damage > 0);
        Assert.Equal(damage * 1.2m, Threat() - before);
    }

    [Fact]
    public void JudgementMarksSupportEachAllyWithPersonalCooldownAndNoPeriodicRecursion()
    {
        var (session, player, ally, enemy) = Party([
            Hook("H-3-3", 2, duration: 12, icd: 2), Hook("H-7-1", 2, duration: 12, icd: 2)]);
        Assert.True(session.Handle(player.ActorId, new UseAbilityCommand("mark", "JUDGEMENT", enemy.ActorId), Now).Succeeded);
        Assert.True(session.Handle(ally.ActorId, new UseAbilityCommand("ally-hit", "STRIKE", enemy.ActorId), Now).Succeeded);
        Assert.Equal(52, ally.CurrentHp);
        Assert.Equal(52, ally.CurrentResource);
        session.Handle(ally.ActorId, new UseAbilityCommand("periodic", "CONSECRATION", enemy.ActorId), Now);
        session.Handle(ally.ActorId, new UseAbilityCommand("during-icd", "STRIKE", enemy.ActorId), Now.AddSeconds(1));
        Assert.Equal(52, ally.CurrentHp);
        Assert.Equal(52, ally.CurrentResource);
        session.Handle(player.ActorId, new UseAbilityCommand("owner-hit", "STRIKE", enemy.ActorId), Now.AddSeconds(1));
        Assert.Equal(52, player.CurrentHp);
        session.Handle(ally.ActorId, new UseAbilityCommand("after-icd", "STRIKE", enemy.ActorId), Now.AddSeconds(2));
        Assert.Equal(54, ally.CurrentHp);
        session.Handle(ally.ActorId, new UseAbilityCommand("expired", "STRIKE", enemy.ActorId), Now.AddSeconds(13));
        Assert.Equal(54, ally.CurrentHp);
    }

    private static ResolvedTalentEventHook Hook(string id, decimal value, decimal secondary = 0,
        int duration = 0, int icd = 0, int rank = 3) => new(id, "ON_ABILITY_USED", rank, value,
            "PALADIN_" + id.Replace('-', '_'), TimeSpan.FromSeconds(icd), false,
            SecondaryValue: secondary, Duration: TimeSpan.FromSeconds(duration));

    private static (CombatSession Session, CombatActorState Player, CombatActorState Ally, CombatActorState Enemy)
        Party(IReadOnlyList<ResolvedTalentEventHook> hooks, bool shield = false, string? weapon = null,
            bool judgementCanMiss = false, decimal accuracy = 100, bool allyPaladin = false)
    {
        var stats = CombatStats.Default with { Accuracy = accuracy };
        var player = new CombatActorState(Guid.NewGuid(), 100, 50, 100, 50, stats);
        var ally = new CombatActorState(Guid.NewGuid(), 100, 50, 100, 50, stats);
        var enemy = new CombatActorState(Guid.NewGuid(), 10000, 10000, 0, 0, stats);
        CombatParticipantDefinition Participant(CombatActorState actor, string id, CombatActorKind kind) => new(
            actor, kind, id, id, "MANA", new AutoAttackProfile(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string> { "STRIKE", "JUDGEMENT", "CRUSADER_STRIKE", "HOLY_SHOCK_OFFENSIVE", "CONSECRATION" }, CanAutoAttack: false,
            MainHandWeaponCategory: weapon, OffHandEquipmentCategory: shield ? "SHIELD" : null);
        var strike = new AbilityDefinition("STRIKE", AbilityType.Instant, AbilityTargetType.SingleEnemy,
            0, TimeSpan.Zero, TimeSpan.Zero, false, GlobalCooldownCategory.None, false, "PHYSICAL",
            Actions: [new(AbilityActionType.Damage, 100, DamageType.Physical,
                CanMiss: false, CanCrit: false, CanDodge: false)]);
        var session = new CombatSession(Guid.NewGuid(), Participant(player, "PALADIN", CombatActorKind.Player),
            Participant(enemy, "TEST_ENEMY", CombatActorKind.Monster),
            new Dictionary<string, AbilityDefinition>
            {
                [strike.Id] = strike, ["JUDGEMENT"] = strike with { Id = "JUDGEMENT",
                    Actions = strike.Actions!.Select(action => action with { CanMiss = judgementCanMiss }).ToArray() },
                ["CRUSADER_STRIKE"] = strike with { Id = "CRUSADER_STRIKE" },
                ["HOLY_SHOCK_OFFENSIVE"] = strike with { Id = "HOLY_SHOCK_OFFENSIVE" },
                ["CONSECRATION"] = strike with { Id = "CONSECRATION", Actions =
                    [new(AbilityActionType.ApplyEffect, Effect: new("PALADIN_CONSECRATION_DAMAGE",
                        EffectKind.DamageOverTime, TimeSpan.FromSeconds(3), 1, EffectStackPolicy.Refresh,
                        20, TickInterval: TimeSpan.FromSeconds(1)))] }
            },
            new MonsterAiProfile("PASSIVE", []), ResolvedTalentModifiers.Empty with { EventHooks = hooks },
            new SequenceGameRandom(Enumerable.Repeat(0m, 100).ToArray()), Now,
            additionalPlayers: [new(Guid.NewGuid(), Participant(ally, allyPaladin ? "PALADIN" : "MAGE", CombatActorKind.Player),
                allyPaladin ? ResolvedTalentModifiers.Empty with { EventHooks = hooks } : ResolvedTalentModifiers.Empty)]);
        return (session, player, ally, enemy);
    }
}
