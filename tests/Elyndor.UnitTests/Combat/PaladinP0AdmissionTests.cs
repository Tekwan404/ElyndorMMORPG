using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Combat;

public sealed class PaladinP0AdmissionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IntercessionInterceptsOtherwiseLethalHitBeforeAnyAllyDeathEvent()
    {
        var (_, paladin, ally, enemy) = Party();
        ally.SetCurrentHp(40);
        EffectEngine.Apply(ally, paladin.ActorId,
            new EffectDefinition("PALADIN_INTERCESSION", EffectKind.Buff,
                TimeSpan.FromSeconds(8), 1, EffectStackPolicy.Replace, 0.30m), Now);

        DamageResult hit = Hit(enemy, ally, 50, Now);

        Assert.Equal(5, ally.CurrentHp);
        Assert.Equal(85, paladin.CurrentHp);
        Assert.False(ally.IsDead);
        Assert.DoesNotContain(hit.Events, e =>
            e.Type == CombatEventType.ActorDied && e.ActorId == ally.ActorId);
        CombatEvent transferred = Assert.Single(hit.Events, e =>
            e.Type == CombatEventType.DamageDealt
            && e.DefinitionId == "PALADIN_INTERCESSION_REDIRECT");
        Assert.Equal(15, transferred.Amount);
        Assert.Equal(paladin.ActorId, transferred.TargetActorId);
        Assert.True(transferred.IsProc);
    }

    [Fact]
    public void IntercessionCanKillTheProtectorButNeverResurrectsTheVictimAfterTheFact()
    {
        var (_, paladin, ally, enemy) = Party();
        paladin.SetCurrentHp(10);
        ally.SetCurrentHp(40);
        EffectEngine.Apply(ally, paladin.ActorId,
            new EffectDefinition("PALADIN_INTERCESSION", EffectKind.Buff,
                TimeSpan.FromSeconds(8), 1, EffectStackPolicy.Replace, 0.30m), Now);

        DamageResult hit = Hit(enemy, ally, 50, Now);

        Assert.Equal(5, ally.CurrentHp);
        Assert.Equal(0, paladin.CurrentHp);
        Assert.Contains(hit.Events, e =>
            e.Type == CombatEventType.ActorDied && e.ActorId == paladin.ActorId);
        Assert.DoesNotContain(hit.Events, e =>
            e.Type == CombatEventType.ActorDied && e.ActorId == ally.ActorId);
    }

    [Fact]
    public void UnbreakableBastionOnlyReducesAdmittedLargeHitsOncePerTwentySeconds()
    {
        var hook = new ResolvedTalentEventHook("P-8-1", "ON_DAMAGE_TAKEN", 1, 1,
            "PALADIN_P_8_1", TimeSpan.FromSeconds(20), false,
            SecondaryValue: 20, Threshold: 25);
        var talents = ResolvedTalentModifiers.Empty with { EventHooks = [hook] };
        var (_, paladin, _, enemy) = Party(talents);

        DamageResult first = Hit(enemy, paladin, 30, Now);
        DamageResult duringIcd = Hit(enemy, paladin, 30, Now.AddSeconds(1));
        DamageResult afterIcd = Hit(enemy, paladin, 30, Now.AddSeconds(20));

        Assert.Equal(24, first.HpDamage);
        Assert.Equal(30, duringIcd.HpDamage);
        Assert.Equal(24, afterIcd.HpDamage);
        Assert.Equal(22, paladin.CurrentHp);
    }

    [Fact]
    public void CleanseRemovesOneActiveInstanceNotEveryEffectInItsCategory()
    {
        var (session, paladin, ally, _) = Party();
        foreach (string id in new[] { "TEST_CURSE_A", "TEST_CURSE_B" })
        {
            EffectEngine.Apply(ally, paladin.ActorId,
                new EffectDefinition(id, EffectKind.Debuff,
                    TimeSpan.FromSeconds(20), 1, EffectStackPolicy.Replace, 1,
                    DispelCategory: "CURSE"), Now);
        }
        Assert.Equal(2, ally.ActiveEffects.Count(x => x.Definition.DispelCategory == "CURSE"));

        CombatCommandResult result = session.Handle(
            paladin.ActorId, new UseAbilityCommand("p0-cleanse", "CLEANSE", ally.ActorId), Now);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Single(ally.ActiveEffects.Where(x => x.Definition.DispelCategory == "CURSE"));
        Assert.DoesNotContain(ally.ActiveEffects, x => x.Definition.Id == "TEST_CURSE_A");
        Assert.Contains(ally.ActiveEffects, x => x.Definition.Id == "TEST_CURSE_B");
    }

    private static DamageResult Hit(CombatActorState source, CombatActorState target,
        decimal amount, DateTimeOffset at) =>
        DamagePipeline.Resolve(new DamageRequest(source, target, amount, DamageType.True,
            CanMiss: false, CanDodge: false, CanCrit: false,
            CanBlock: false, MinimumDamage: 0), new SequenceGameRandom([0.99m]), at);

    private static (CombatSession Session, CombatActorState Paladin,
        CombatActorState Ally, CombatActorState Enemy) Party(ResolvedTalentModifiers? talents = null)
    {
        CombatStats stats = CombatStats.Default with
        {
            Accuracy = 100, Dodge = 0, CriticalChance = 0, CriticalDamage = 1
        };
        CombatActorState paladin = new(Guid.NewGuid(), 100, 100, 100, 100, stats);
        CombatActorState ally = new(Guid.NewGuid(), 100, 100, 100, 100, stats);
        CombatActorState enemy = new(Guid.NewGuid(), 500, 500, 0, 0, stats);
        AutoAttackProfile auto = new(TimeSpan.FromHours(1), 0, 0, 0);
        CombatParticipantDefinition player = new(paladin, CombatActorKind.Player,
            "PALADIN", "Paladin", "MANA", auto,
            new HashSet<string>(["CLEANSE", "INTERCESSION"], StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition member = new(ally, CombatActorKind.Player,
            "MAGE", "Mage", "MANA", auto, new HashSet<string>(StringComparer.Ordinal),
            CanAutoAttack: false);
        CombatParticipantDefinition monster = new(enemy, CombatActorKind.Monster,
            "TEST_ENEMY", "Enemy", "NONE", auto,
            new HashSet<string>(StringComparer.Ordinal));
        AbilityDefinition cleanse = new("CLEANSE", AbilityType.Instant,
            AbilityTargetType.SingleAlly, 0, TimeSpan.Zero, TimeSpan.Zero,
            false, GlobalCooldownCategory.None, true, "HOLY", AllowSelfTarget: true,
            Actions:
            [
                new AbilityActionDefinition(AbilityActionType.ApplyEffect,
                    Effect: new EffectDefinition("PALADIN_CLEANSE_CAST", EffectKind.Buff,
                        TimeSpan.FromMilliseconds(100), 1, EffectStackPolicy.Replace, 1))
            ]);
        CombatSession session = new(Guid.NewGuid(), player, monster,
            new Dictionary<string, AbilityDefinition> { ["CLEANSE"] = cleanse },
            new MonsterAiProfile("PASSIVE_PALADIN_P0", []),
            talents ?? ResolvedTalentModifiers.Empty,
            new SequenceGameRandom(Enumerable.Repeat(0.99m, 100).ToArray()), Now,
            additionalPlayers: [new CombatPlayerDefinition(Guid.NewGuid(), member,
                ResolvedTalentModifiers.Empty)]);
        return (session, paladin, ally, enemy);
    }
}
