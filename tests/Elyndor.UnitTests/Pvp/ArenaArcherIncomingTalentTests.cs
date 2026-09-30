using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaArcherIncomingTalentTests
{
    [Fact]
    public void SurvivalInstinctReducesDamageWhenHealthIsBelowThresholdBeforeHit()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        AbilityDefinition strike = Strike();
        ArenaFighter attacker = Fighter(Actor(100), strike, ResolvedTalentModifiers.Empty);
        ResolvedTalentModifiers talents = Talents(SurvivalInstinctHook(10));
        CombatActorState defenderActor = Actor(29);
        ArenaTalentRuntimeSupport.ConfigureActorRuntime(defenderActor, talents);
        ArenaFighter defender = Fighter(defenderActor, null, talents);
        var session = new ArenaCombatSession(
            Guid.NewGuid(),
            attacker,
            defender,
            new SequenceGameRandom(),
            now);

        ArenaCommandResult result = session.UseAbility(
            attacker.AccountId,
            "low-hp",
            strike.Id,
            defender.Actor.ActorId,
            now);

        Assert.True(result.Succeeded);
        Assert.Equal(11m, result.Snapshot.ActorB.CurrentHp);
        CombatEvent damage = Assert.Single(result.Events, x => x.Type == CombatEventType.DamageDealt);
        Assert.Equal(18m, damage.Amount);
    }

    [Fact]
    public void SurvivalInstinctDoesNotTriggerAtOrAboveThreshold()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        AbilityDefinition strike = Strike();
        ArenaFighter attacker = Fighter(Actor(100), strike, ResolvedTalentModifiers.Empty);
        ResolvedTalentModifiers talents = Talents(SurvivalInstinctHook(10));
        CombatActorState defenderActor = Actor(30);
        ArenaTalentRuntimeSupport.ConfigureActorRuntime(defenderActor, talents);
        ArenaFighter defender = Fighter(defenderActor, null, talents);
        var session = new ArenaCombatSession(
            Guid.NewGuid(),
            attacker,
            defender,
            new SequenceGameRandom(),
            now);

        ArenaCommandResult result = session.UseAbility(
            attacker.AccountId,
            "threshold",
            strike.Id,
            defender.Actor.ActorId,
            now);

        Assert.True(result.Succeeded);
        Assert.Equal(10m, result.Snapshot.ActorB.CurrentHp);
        CombatEvent damage = Assert.Single(result.Events, x => x.Type == CombatEventType.DamageDealt);
        Assert.Equal(20m, damage.Amount);
    }

    [Fact]
    public void SurvivalInstinctHookIsAcceptedByCapabilityValidation()
    {
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(SurvivalInstinctHook(10)));
    }

    private static ArenaFighter Fighter(
        CombatActorState actor,
        AbilityDefinition? ability,
        ResolvedTalentModifiers talents)
    {
        IReadOnlyDictionary<string, AbilityDefinition> abilities = ability is null
            ? new Dictionary<string, AbilityDefinition>()
            : new Dictionary<string, AbilityDefinition> { [ability.Id] = ability };
        return new ArenaFighter(
            Guid.NewGuid(),
            Guid.NewGuid(),
            actor,
            abilities,
            new AutoAttackProfile(TimeSpan.FromSeconds(30), 1, 0, 0),
            talents);
    }

    private static CombatActorState Actor(decimal currentHp) => new(
        Guid.NewGuid(),
        100,
        currentHp,
        100,
        100,
        new CombatStats(20, 0, 0, 0, 1, 0, 0, 0, 0));

    private static AbilityDefinition Strike() => new(
        "TEST_SURVIVAL_INSTINCT_STRIKE",
        AbilityType.Instant,
        AbilityTargetType.SingleEnemy,
        0,
        TimeSpan.Zero,
        TimeSpan.Zero,
        false,
        GlobalCooldownCategory.None,
        false,
        "PHYSICAL",
        Actions: [new AbilityActionDefinition(
            AbilityActionType.Damage,
            20,
            DamageType.Physical,
            CanMiss: false,
            CanCrit: false,
            CanDodge: false)]);

    private static ResolvedTalentEventHook SurvivalInstinctHook(decimal value) => new(
        "S-6-4",
        TalentModifierKeys.OnHpThreshold,
        1,
        value,
        "LOW_HP_REDUCTION",
        TimeSpan.Zero,
        false,
        Threshold: 30);

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(),
        new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
        hooks,
        []);
}
