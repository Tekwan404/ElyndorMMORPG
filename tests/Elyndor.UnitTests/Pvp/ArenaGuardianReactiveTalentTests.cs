using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaGuardianReactiveTalentTests
{
    [Fact]
    public void BlockGrantsResourceOnceAndRespectsResourceCap()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        AbilityDefinition strike = Strike();
        ArenaFighter attacker = Fighter(
            Actor(resource: 0),
            new Dictionary<string, AbilityDefinition> { [strike.Id] = strike },
            ResolvedTalentModifiers.Empty);
        ArenaFighter defender = Fighter(
            Actor(resource: 5, blockChance: 100, blockValue: 100),
            new Dictionary<string, AbilityDefinition>(),
            Talents(BlockHook(3)));
        var session = new ArenaCombatSession(
            Guid.NewGuid(),
            attacker,
            defender,
            new SequenceGameRandom(0m, 0m),
            now);

        ArenaCommandResult first = session.UseAbility(
            attacker.AccountId,
            "one",
            strike.Id,
            defender.Actor.ActorId,
            now);
        Assert.True(first.Succeeded);
        Assert.Equal(8m, first.Snapshot.ActorB.CurrentResource);
        CombatEvent resource = Assert.Single(first.Events, x =>
            x.Type == CombatEventType.ResourceChanged
            && x.DefinitionId == "G-2-5");
        Assert.Equal(3m, resource.Amount);

        ArenaCommandResult second = session.UseAbility(
            attacker.AccountId,
            "two",
            strike.Id,
            defender.Actor.ActorId,
            now + TimeSpan.FromSeconds(1));
        Assert.True(second.Succeeded);
        Assert.Equal(10m, second.Snapshot.ActorB.CurrentResource);
        CombatEvent capped = Assert.Single(second.Events, x =>
            x.Type == CombatEventType.ResourceChanged
            && x.DefinitionId == "G-2-5");
        Assert.Equal(2m, capped.Amount);
    }

    [Fact]
    public void NonBlockDoesNotGrantResource()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        AbilityDefinition strike = Strike();
        ArenaFighter attacker = Fighter(
            Actor(resource: 0),
            new Dictionary<string, AbilityDefinition> { [strike.Id] = strike },
            ResolvedTalentModifiers.Empty);
        ArenaFighter defender = Fighter(
            Actor(resource: 5, blockChance: 0, blockValue: 0),
            new Dictionary<string, AbilityDefinition>(),
            Talents(BlockHook(3)));
        var session = new ArenaCombatSession(
            Guid.NewGuid(),
            attacker,
            defender,
            new SequenceGameRandom(),
            now);

        ArenaCommandResult result = session.UseAbility(
            attacker.AccountId,
            "one",
            strike.Id,
            defender.Actor.ActorId,
            now);

        Assert.True(result.Succeeded);
        Assert.Equal(5m, result.Snapshot.ActorB.CurrentResource);
        Assert.DoesNotContain(result.Events, x =>
            x.Type == CombatEventType.ResourceChanged
            && x.DefinitionId == "G-2-5");
    }

    [Fact]
    public void BlockAndIncomingCriticalDamageHooksAreAccepted()
    {
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(BlockHook(3)));
        Assert.True(ArenaTalentRuntimeSupport.SupportsEventHook(new ResolvedTalentEventHook(
            "G-1-5",
            TalentModifierKeys.OnDamageTaken,
            1,
            10,
            "INCOMING_CRITICAL_DAMAGE",
            TimeSpan.Zero,
            false)));
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
        decimal resource,
        decimal blockChance = 0,
        decimal blockValue = 0) => new(
        Guid.NewGuid(),
        100,
        100,
        10,
        resource,
        new CombatStats(
            20,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            BlockChance: blockChance,
            BlockValueMin: blockValue,
            BlockValueMax: blockValue));

    private static AbilityDefinition Strike() => new(
        "TEST_BLOCKABLE_STRIKE",
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

    private static ResolvedTalentEventHook BlockHook(decimal value) => new(
        "G-2-5",
        TalentModifierKeys.OnDamageTaken,
        1,
        value,
        "BLOCK",
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
