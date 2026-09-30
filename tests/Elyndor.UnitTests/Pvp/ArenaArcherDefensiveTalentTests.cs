using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaArcherDefensiveTalentTests
{
    [Fact]
    public void ArcherWithoutCompanionCanEnterWithDefensiveHooks()
    {
        CombatActorState archer = Actor();
        ResolvedTalentModifiers talents = Talents(
            Hook("S-1-2", TalentModifierKeys.OnDamageTaken,
                "CONTROL_DURATION_REDUCTION", 15),
            Hook("S-5-4", TalentModifierKeys.OnDamageTaken,
                "IRON_WILL", 20, secondaryValue: 10));
        var participant = new CombatParticipantDefinition(
            archer, CombatActorKind.Player, "ARCHER", "Archer", "FOCUS",
            new AutoAttackProfile(TimeSpan.FromSeconds(3), 5, 0, 0),
            new HashSet<string>(StringComparer.Ordinal), GenderId: "FEMALE");
        var player = new CombatPlayerDefinition(Guid.NewGuid(), participant, talents);

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            player, 20, new Dictionary<string, AbilityDefinition>(),
            hasCompanion: false);

        Assert.Equal(0.65m, entrant.Fighter.Actor.IncomingControlDurationMultiplier);
        Assert.Throws<NotSupportedException>(() => ArenaFighterAssembler.Create(
            player, 20, new Dictionary<string, AbilityDefinition>(),
            hasCompanion: true));
    }

    [Fact]
    public void SureFootingShortensStunAndSilenceButNotUnrelatedEffects()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CombatActorState archer = Actor();
        ArenaTalentRuntimeSupport.ConfigureActorRuntime(archer, Talents(
            Hook("S-1-2", TalentModifierKeys.OnDamageTaken,
                "CONTROL_DURATION_REDUCTION", 15)));

        Assert.Equal(0.85m, archer.IncomingControlDurationMultiplier);
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(Hook(
            "S-1-2", TalentModifierKeys.OnDamageTaken,
            "CONTROL_DURATION_REDUCTION", 15)));

        EffectEngine.Apply(archer, Guid.NewGuid(), Control("STUN", EffectKind.Stun), now);
        EffectEngine.Apply(archer, Guid.NewGuid(), Control("SILENCE", EffectKind.Silence), now);
        EffectEngine.Apply(archer, Guid.NewGuid(), Control("ROOT", EffectKind.Root), now);

        Assert.Equal(now.AddSeconds(8.5), archer.ActiveEffects.Single(x => x.Definition.Id == "STUN").ExpiresAtUtc);
        Assert.Equal(now.AddSeconds(8.5), archer.ActiveEffects.Single(x => x.Definition.Id == "SILENCE").ExpiresAtUtc);
        Assert.Equal(now.AddSeconds(10), archer.ActiveEffects.Single(x => x.Definition.Id == "ROOT").ExpiresAtUtc);
    }

    [Fact]
    public void SureFootingPreservesExistingControlDurationModifier()
    {
        CombatActorState archer = Actor();
        archer.IncomingControlDurationMultiplier = 0.8m;

        ArenaTalentRuntimeSupport.ConfigureActorRuntime(archer, Talents(
            Hook("S-1-2", TalentModifierKeys.OnDamageTaken,
                "CONTROL_DURATION_REDUCTION", 15)));

        Assert.Equal(0.68m, archer.IncomingControlDurationMultiplier);
    }

    [Fact]
    public void IronWillStacksControlReductionAndReducesDamageOnlyWhileControlled()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CombatActorState attacker = Actor();
        CombatActorState archer = Actor();
        ArenaTalentRuntimeSupport.ConfigureActorRuntime(archer, Talents(
            Hook("S-1-2", TalentModifierKeys.OnDamageTaken,
                "CONTROL_DURATION_REDUCTION", 15),
            Hook("S-5-4", TalentModifierKeys.OnDamageTaken,
                "IRON_WILL", 20, secondaryValue: 10)));

        Assert.Equal(0.65m, archer.IncomingControlDurationMultiplier);
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(Hook(
            "S-5-4", TalentModifierKeys.OnDamageTaken,
            "IRON_WILL", 20, secondaryValue: 10)));
        Assert.Equal(20m, Hit(attacker, archer, now));

        EffectEngine.Apply(archer, attacker.ActorId, Control("STUN", EffectKind.Stun), now);
        Assert.Equal(now.AddSeconds(6.5), archer.ActiveEffects.Single().ExpiresAtUtc);
        Assert.Equal(18m, Hit(attacker, archer, now.AddSeconds(1)));
        Assert.Equal(20m, Hit(attacker, archer, now.AddSeconds(7)));

        EffectEngine.Apply(archer, attacker.ActorId, Control("SILENCE", EffectKind.Silence), now.AddSeconds(8));
        Assert.Equal(18m, Hit(attacker, archer, now.AddSeconds(9)));
    }

    [Fact]
    public void IronWillReducesActualArenaAbilityDamageDuringStun()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CombatActorState archer = Actor();
        ArenaTalentRuntimeSupport.ConfigureActorRuntime(archer, Talents(
            Hook("S-5-4", TalentModifierKeys.OnDamageTaken,
                "IRON_WILL", 20, secondaryValue: 10)));
        CombatActorState attacker = Actor();
        EffectEngine.Apply(archer, attacker.ActorId, Control("STUN", EffectKind.Stun), now);

        var strike = new AbilityDefinition(
            "TEST_ARCHER_IRON_WILL_STRIKE", AbilityType.Instant,
            AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, TimeSpan.Zero,
            false, GlobalCooldownCategory.None, false, "PHYSICAL",
            Actions: [new AbilityActionDefinition(
                AbilityActionType.Damage, 20, DamageType.True,
                CanMiss: false, CanCrit: false, CanDodge: false)]);
        var attackFighter = new ArenaFighter(
            Guid.NewGuid(), Guid.NewGuid(), attacker,
            new Dictionary<string, AbilityDefinition> { [strike.Id] = strike },
            new AutoAttackProfile(TimeSpan.FromSeconds(30), 1, 0, 0));
        var archerFighter = new ArenaFighter(
            Guid.NewGuid(), Guid.NewGuid(), archer,
            new Dictionary<string, AbilityDefinition>(),
            new AutoAttackProfile(TimeSpan.FromSeconds(30), 1, 0, 0));
        var session = new ArenaCombatSession(
            Guid.NewGuid(), attackFighter, archerFighter, new SequenceGameRandom(), now);

        ArenaCommandResult result = session.UseAbility(
            attackFighter.AccountId, "iron-will-hit", strike.Id,
            archer.ActorId, now);

        Assert.True(result.Succeeded);
        Assert.Equal(82m, result.Snapshot.ActorB.CurrentHp);
        Assert.Equal(18m, Assert.Single(result.Events,
            combatEvent => combatEvent.Type == CombatEventType.DamageDealt).Amount);
    }

    [Fact]
    public void UnsupportedOrMalformedArcherHooksRemainFailClosed()
    {
        Assert.False(ArenaTalentRuntimeSupport.SupportsEventHook(Hook(
            "S-1-2", TalentModifierKeys.OnDamageTaken,
            "CONTROL_DURATION_REDUCTION", 120)));
        Assert.False(ArenaTalentRuntimeSupport.SupportsEventHook(Hook(
            "S-5-4", TalentModifierKeys.OnDamageTaken,
            "IRON_WILL", 20, secondaryValue: 110)));
        Assert.False(ArenaTalentRuntimeSupport.SupportsEventHook(Hook(
            "B-5-1", TalentModifierKeys.OnCriticalHit,
            "FRENZY", 20)));
    }

    private static EffectDefinition Control(string id, EffectKind kind) => new(
        id, kind, TimeSpan.FromSeconds(10), 1, EffectStackPolicy.Replace, 1);

    private static decimal Hit(CombatActorState attacker, CombatActorState defender, DateTimeOffset now) =>
        DamagePipeline.Resolve(new DamageRequest(attacker, defender, 20, DamageType.True,
            CanMiss: false, CanCrit: false, CanDodge: false), new SequenceGameRandom(), now).HpDamage;

    private static CombatActorState Actor() => new(
        Guid.NewGuid(), 100, 100, 100, 100,
        new CombatStats(20, 0, 0, 0, 1, 0, 0, 0, 0));

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(), new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal), hooks, []);

    private static ResolvedTalentEventHook Hook(string talentId, string key,
        string targetId, decimal value, decimal secondaryValue = 0) =>
        new(talentId, key, 1, value, targetId, TimeSpan.Zero, false,
            SecondaryValue: secondaryValue);
}
