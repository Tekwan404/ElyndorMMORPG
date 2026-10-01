using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaMechanicsReviewRegressionTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, 12.5)]
    [InlineData(true, 15)]
    public void RegenerationIsIndependentOfAdvancePartition(bool deepMeditation, double expected)
    {
        decimal Run(bool split)
        {
            var spell = Spell("TEST_SPELL", cost: 1);
            var abilities = new Dictionary<string, AbilityDefinition> { [spell.Id] = spell };
            ArenaFighter owner = Fighter("MAGE", abilities, resource: 1);
            ArenaFighter opponent = Fighter("MAGE", abilities);
            var talents = ResolvedTalentModifiers.Empty;
            if (deepMeditation)
                talents = talents with { EventHooks = [new ResolvedTalentEventHook(
                    "A-6-3", "ON_ABILITY_USED", 1, 100, null, TimeSpan.Zero, false,
                    Duration: TimeSpan.FromSeconds(5))] };
            owner = owner with { TalentModifiers = talents,
                PlayerDefinition = owner.PlayerDefinition! with { TalentModifiers = talents } };
            var session = Session(owner, opponent);
            Assert.True(session.UseAbility(owner.AccountId, "spend", spell.Id, opponent.Actor.ActorId, Start).Succeeded);
            if (!deepMeditation)
                EffectEngine.Apply(owner.Actor, owner.Actor.ActorId, new EffectDefinition(
                    "MAGE_CLEARCASTING_REGEN", EffectKind.Buff, TimeSpan.FromSeconds(5),
                    1, EffectStackPolicy.Replace, 50), Start);
            if (split)
            {
                session.AdvanceTo(Start.AddSeconds(2));
                session.AdvanceTo(Start.AddSeconds(5));
            }
            session.AdvanceTo(Start.AddSeconds(10));
            return owner.Actor.CurrentResource;
        }
        Assert.Equal((decimal)expected, Run(false));
        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void CounterspellAttributesInterruptToCasterAndOpponent()
    {
        AbilityDefinition counter = Spell("MAGE_COUNTERSPELL") with { Actions = [] };
        AbilityDefinition cast = Spell("TEST_CAST") with
        { Type = AbilityType.Casted, CastTime = TimeSpan.FromSeconds(3) };
        ArenaFighter owner = Fighter("MAGE", new Dictionary<string, AbilityDefinition> { [counter.Id] = counter });
        ArenaFighter opponent = Fighter("MAGE", new Dictionary<string, AbilityDefinition> { [cast.Id] = cast });
        var session = Session(owner, opponent);
        Assert.True(session.UseAbility(opponent.AccountId, "cast", cast.Id, owner.Actor.ActorId, Start).Succeeded);
        Assert.True(session.UseAbility(owner.AccountId, "counter", counter.Id, opponent.Actor.ActorId, Start).Succeeded);
        CombatEvent interrupted = Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.AbilityInterrupted);
        Assert.Equal(owner.Actor.ActorId, interrupted.SourceActorId);
        Assert.Equal(opponent.Actor.ActorId, interrupted.TargetActorId);
        Assert.Equal(opponent.Actor.ActorId, interrupted.ActorId);
        Assert.Equal(cast.Id, interrupted.DefinitionId);
    }

    [Theory]
    [InlineData(false, "MAGE")]
    [InlineData(true, "WARRIOR")]
    public void ActionlessClassAbilityRequiresMatchingProductionHost(bool hosted, string classId)
    {
        AbilityDefinition counter = Spell("MAGE_COUNTERSPELL") with { Actions = [] };
        var abilities = new Dictionary<string, AbilityDefinition> { [counter.Id] = counter };
        ArenaFighter owner = Fighter(classId, abilities);
        ArenaFighter opponent = Fighter("MAGE", abilities);
        if (!hosted) owner = owner with { PlayerDefinition = null };
        Assert.Throws<NotSupportedException>(() => Session(owner, opponent));
    }

    [Fact]
    public void SyntheticClassNamedAbilityWithKernelActionsStillWorksWithoutHost()
    {
        AbilityDefinition spell = Spell("MAGE_COUNTERSPELL");
        var abilities = new Dictionary<string, AbilityDefinition> { [spell.Id] = spell };
        ArenaFighter owner = Fighter("MAGE", abilities) with { PlayerDefinition = null };
        ArenaFighter opponent = Fighter("MAGE", abilities) with { PlayerDefinition = null };
        var session = Session(owner, opponent);
        Assert.True(session.UseAbility(owner.AccountId, "synthetic", spell.Id, opponent.Actor.ActorId, Start).Succeeded);
        Assert.Equal(990m, opponent.Actor.CurrentHp);
    }

    [Fact]
    public void DeferredAutoAttackDeathPreservesCriticalAndWeaponMetadata()
    {
        var abilities = new Dictionary<string, AbilityDefinition>();
        var profile = new AutoAttackProfile(TimeSpan.FromSeconds(1), 2000, 0, 0,
            WeaponDefinitionId: "TEST_WEAPON", WeaponHand: CombatWeaponHand.MainHand);
        ArenaFighter owner = Fighter("WARRIOR", abilities, profile, criticalChance: 100);
        ArenaFighter opponent = Fighter("WARRIOR", abilities);
        var session = Session(owner, opponent);
        session.AdvanceTo(Start.AddSeconds(1));
        CombatEvent damage = Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.DamageDealt);
        CombatEvent death = Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.ActorDied);
        Assert.True(damage.IsCritical);
        Assert.Equal(damage.IsCritical, death.IsCritical);
        Assert.Equal(profile.WeaponHand, death.WeaponHand);
        Assert.Equal(profile.WeaponDefinitionId, death.WeaponDefinitionId);
        Assert.Equal(damage.SourceActorId, death.SourceActorId);
        Assert.Equal(damage.TargetActorId, death.TargetActorId);
    }

    [Fact]
    public void DeferredSpellDeathPreservesUnblockableMetadata()
    {
        AbilityDefinition spell = Spell("LETHAL") with { Type = AbilityType.Casted,
            CastTime = TimeSpan.FromSeconds(1), Actions = [new AbilityActionDefinition(
                AbilityActionType.Damage, 2000, DamageType.Magical,
                CanMiss: false, CanCrit: false, CanDodge: false, IsUnblockable: true)] };
        var abilities = new Dictionary<string, AbilityDefinition> { [spell.Id] = spell };
        ArenaFighter owner = Fighter("MAGE", abilities);
        ArenaFighter opponent = Fighter("MAGE", abilities);
        var session = Session(owner, opponent);
        Assert.True(session.UseAbility(owner.AccountId, "lethal", spell.Id, opponent.Actor.ActorId, Start).Succeeded);
        session.AdvanceTo(Start.AddSeconds(1));
        Assert.True(Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.ActorDied).IsUnblockable);
    }

    [Fact]
    public void DeferredReflectedDeathPreservesReflectionWithoutReactingAgain()
    {
        var abilities = new Dictionary<string, AbilityDefinition>();
        ArenaFighter owner = Fighter("WARRIOR", abilities,
            new AutoAttackProfile(TimeSpan.FromSeconds(1), 600, 0, 0));
        ArenaFighter opponent = Fighter("WARRIOR", abilities);
        var session = Session(owner, opponent);
        EffectEngine.Apply(opponent.Actor, opponent.Actor.ActorId, new EffectDefinition(
            "TEST_REFLECTION", EffectKind.DamageReflection, TimeSpan.FromSeconds(10),
            1, EffectStackPolicy.Replace, 2), Start);
        session.AdvanceTo(Start.AddSeconds(1));
        CombatEvent reflected = Assert.Single(session.GetEventsAfter(0), e =>
            e.Type == CombatEventType.DamageDealt && e.IsReflected);
        CombatEvent death = Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.ActorDied);
        Assert.True(death.IsReflected);
        Assert.Equal(reflected.DefinitionId, death.DefinitionId);
        Assert.Equal(opponent.Actor.ActorId, death.SourceActorId);
        Assert.Equal(owner.Actor.ActorId, death.TargetActorId);
    }

    [Fact]
    public void CriticalHitAndCriticalDamageMetadataTriggerResourceProcExactlyOnce()
    {
        var abilities = new Dictionary<string, AbilityDefinition>();
        ArenaFighter owner = Fighter("WARRIOR", abilities,
            new AutoAttackProfile(TimeSpan.FromSeconds(1), 10, 0, 0), criticalChance: 100);
        ArenaFighter opponent = Fighter("WARRIOR", abilities);
        var talents = ResolvedTalentModifiers.Empty with { EventHooks = [new ResolvedTalentEventHook(
            "B-3-1", TalentModifierKeys.OnCriticalHit, 1, 7, null, TimeSpan.Zero, false)] };
        owner = owner with { TalentModifiers = talents,
            PlayerDefinition = owner.PlayerDefinition! with { TalentModifiers = talents } };
        opponent = opponent with { TalentModifiers = talents,
            PlayerDefinition = opponent.PlayerDefinition! with { TalentModifiers = talents } };
        var session = Session(owner, opponent);
        session.AdvanceTo(Start.AddSeconds(1));
        var events = session.GetEventsAfter(0);
        Assert.Single(events, e => e.Type == CombatEventType.CriticalHit);
        Assert.True(Assert.Single(events, e => e.Type == CombatEventType.DamageDealt).IsCritical);
        CombatEvent proc = Assert.Single(events, e => e.Type == CombatEventType.ResourceChanged
            && e.DefinitionId == "B-3-1");
        Assert.Equal(owner.Actor.ActorId, proc.ActorId);
        Assert.Equal(7m, proc.Amount);
        session.AdvanceTo(Start.AddSeconds(1));
        Assert.Single(session.GetEventsAfter(0), e => e.Type == CombatEventType.ResourceChanged
            && e.DefinitionId == "B-3-1");
    }

    private static AbilityDefinition Spell(string id, decimal cost = 0) => new(id,
        AbilityType.Instant, AbilityTargetType.SingleEnemy, cost, TimeSpan.Zero,
        TimeSpan.Zero, false, GlobalCooldownCategory.None, true, "ARCANE",
        Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10, DamageType.Magical,
            CanMiss: false, CanCrit: false, CanDodge: false)]);

    private static ArenaFighter Fighter(string classId, IReadOnlyDictionary<string, AbilityDefinition> abilities,
        AutoAttackProfile? auto = null, decimal resource = 100, decimal criticalChance = 0)
    {
        var actor = new CombatActorState(Guid.NewGuid(), 1000, 1000, 1000, resource,
            CombatStats.Default with { Accuracy = 100, CriticalChance = criticalChance });
        AutoAttackProfile profile = auto ?? new AutoAttackProfile(TimeSpan.FromHours(1), 1, 0, 0);
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player,
            classId, classId, classId == "MAGE" ? "MANA" : "RAGE", profile,
            abilities.Keys.ToHashSet(), ResourceRegenPerSecond: 1, CanAutoAttack: auto is not null);
        var player = new CombatPlayerDefinition(Guid.NewGuid(), participant, ResolvedTalentModifiers.Empty);
        return new ArenaFighter(player.AccountId, actor.ActorId, actor, abilities, profile,
            ResourceRegenPerSecond: 1, CanAutoAttack: auto is not null,
            PlayerDefinition: player, BaseAbilities: abilities);
    }

    private static ArenaCombatSession Session(ArenaFighter owner, ArenaFighter opponent) =>
        new(Guid.NewGuid(), owner, opponent, new SeededGameRandom(1), Start);
}
