using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaIncomingDamageTalentTests
{
    [Fact]
    public void AnticipationReducesIncomingCriticalDamageAndPostDamageSeesFinalAmount()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        AbilityDefinition strike = Strike(canCrit: true);
        ArenaFighter attacker = Fighter(
            Actor(criticalChance: 100, criticalDamage: 1),
            new Dictionary<string, AbilityDefinition> { [strike.Id] = strike },
            ResolvedTalentModifiers.Empty);
        ResolvedTalentModifiers talents = Talents(AnticipationHook(20));
        CombatActorState defenderActor = Actor();
        ArenaTalentRuntimeSupport.ConfigureActorRuntime(defenderActor, talents);
        ArenaFighter defender = Fighter(
            defenderActor,
            new Dictionary<string, AbilityDefinition>(),
            talents);
        var session = new ArenaCombatSession(
            Guid.NewGuid(),
            attacker,
            defender,
            new SequenceGameRandom(0m),
            now);

        ArenaCommandResult result = session.UseAbility(
            attacker.AccountId,
            "crit",
            strike.Id,
            defender.Actor.ActorId,
            now);

        Assert.True(result.Succeeded);
        Assert.Equal(68m, result.Snapshot.ActorB.CurrentHp);
        CombatEvent damage = Assert.Single(result.Events, x => x.Type == CombatEventType.DamageDealt);
        Assert.Equal(32m, damage.Amount);

        ArenaTalentCombatEvent postDamage = Assert.Single(
            ArenaTalentEventDispatcher.FromDamageEvents(result.Events, strike),
            x => x.Type == ArenaTalentEventType.OnDamageTaken);
        Assert.True(postDamage.WasCritical);
        Assert.Equal(32m, postDamage.FinalDamage);
    }

    [Fact]
    public void AnticipationDoesNotChangeNormalHit()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        AbilityDefinition strike = Strike(canCrit: false);
        ArenaFighter attacker = Fighter(
            Actor(),
            new Dictionary<string, AbilityDefinition> { [strike.Id] = strike },
            ResolvedTalentModifiers.Empty);
        ResolvedTalentModifiers talents = Talents(AnticipationHook(20));
        CombatActorState defenderActor = Actor();
        ArenaTalentRuntimeSupport.ConfigureActorRuntime(defenderActor, talents);
        ArenaFighter defender = Fighter(
            defenderActor,
            new Dictionary<string, AbilityDefinition>(),
            talents);
        var session = new ArenaCombatSession(
            Guid.NewGuid(),
            attacker,
            defender,
            new SequenceGameRandom(),
            now);

        ArenaCommandResult result = session.UseAbility(
            attacker.AccountId,
            "normal",
            strike.Id,
            defender.Actor.ActorId,
            now);

        Assert.True(result.Succeeded);
        Assert.Equal(80m, result.Snapshot.ActorB.CurrentHp);
        CombatEvent damage = Assert.Single(result.Events, x => x.Type == CombatEventType.DamageDealt);
        Assert.Equal(20m, damage.Amount);
    }

    [Fact]
    public void SharedIncomingDamageModifierRunsBeforeHpMutation()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        CombatActorState source = Actor();
        CombatActorState target = Actor();
        target.IncomingDamageModifier = (context, _) =>
        {
            Assert.Equal(100m, context.Target.CurrentHp);
            Assert.Equal(20m, context.CurrentAmount);
            return context.CurrentAmount / 2m;
        };

        DamageResult result = DamagePipeline.Resolve(
            new DamageRequest(
                source,
                target,
                20,
                DamageType.Physical,
                CanMiss: false,
                CanDodge: false,
                CanCrit: false,
                CanBlock: false),
            new SequenceGameRandom(),
            now);

        Assert.Equal(10m, result.HpDamage);
        Assert.Equal(90m, target.CurrentHp);
    }

    [Fact]
    public void CriticalReductionComposesBeforeBlockDeterministically()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        AbilityDefinition strike = Strike(canCrit: true);
        ArenaFighter attacker = Fighter(
            Actor(criticalChance: 100, criticalDamage: 1),
            new Dictionary<string, AbilityDefinition> { [strike.Id] = strike },
            ResolvedTalentModifiers.Empty);
        ResolvedTalentModifiers talents = Talents(AnticipationHook(20));
        CombatActorState defenderActor = Actor(blockChance: 100, blockValue: 5);
        ArenaTalentRuntimeSupport.ConfigureActorRuntime(defenderActor, talents);
        ArenaFighter defender = Fighter(
            defenderActor,
            new Dictionary<string, AbilityDefinition>(),
            talents);
        var session = new ArenaCombatSession(
            Guid.NewGuid(),
            attacker,
            defender,
            new SequenceGameRandom(0m, 0m),
            now);

        ArenaCommandResult result = session.UseAbility(
            attacker.AccountId,
            "crit-block",
            strike.Id,
            defender.Actor.ActorId,
            now);

        Assert.True(result.Succeeded);
        Assert.Equal(73m, result.Snapshot.ActorB.CurrentHp);
        CombatEvent blocked = Assert.Single(result.Events, x => x.Type == CombatEventType.DamageBlocked);
        Assert.Equal(5m, blocked.Amount);
        CombatEvent damage = Assert.Single(result.Events, x => x.Type == CombatEventType.DamageDealt);
        Assert.Equal(27m, damage.Amount);
        Assert.Equal(32m, damage.DamageBeforeBlock);
    }

    private static ArenaFighter Fighter(
        CombatActorState actor,
        IReadOnlyDictionary<string, AbilityDefinition> abilities,
        ResolvedTalentModifiers talents) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        actor,
        abilities,
        new AutoAttackProfile(TimeSpan.FromSeconds(30), 1, 0, 0),
        talents);

    private static CombatActorState Actor(
        decimal criticalChance = 0,
        decimal criticalDamage = 1,
        decimal blockChance = 0,
        decimal blockValue = 0) => new(
        Guid.NewGuid(),
        100,
        100,
        100,
        100,
        new CombatStats(
            20,
            0,
            0,
            criticalChance,
            criticalDamage,
            0,
            0,
            0,
            0,
            BlockChance: blockChance,
            BlockValueMin: blockValue,
            BlockValueMax: blockValue));

    private static AbilityDefinition Strike(bool canCrit) => new(
        "TEST_ANTICIPATION_STRIKE",
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
            CanCrit: canCrit,
            CanDodge: false)]);

    private static ResolvedTalentEventHook AnticipationHook(decimal value) => new(
        "G-1-5",
        TalentModifierKeys.OnDamageTaken,
        1,
        value,
        "INCOMING_CRITICAL_DAMAGE",
        TimeSpan.Zero,
        false);

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(),
        new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
        hooks,
        []);
}
