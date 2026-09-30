using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaMageStaticTalentHookTests
{
    [Theory]
    [InlineData("MAGE_FIREBALL", 94)]
    [InlineData("MAGE_SCORCH", 94)]
    [InlineData("MAGE_FIRE_BLAST", 100)]
    public void BurningSoulReducesOnlyFireballAndScorchManaCost(string abilityId, decimal expectedCost)
    {
        ResolvedTalentModifiers talents = Talents(Hook("F-1-3", 20, secondaryValue: 6));
        AbilityDefinition ability = Ability(abilityId, "FIRE") with { ResourceCost = 100 };

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            Player(abilityId, talents), 20,
            new Dictionary<string, AbilityDefinition> { [abilityId] = ability },
            hasCompanion: false);

        Assert.Equal(expectedCost, entrant.Fighter.Abilities[abilityId].ResourceCost);
    }

    [Fact]
    public void BurningSoulManaSavingIsAppliedByTheAuthoritativeArenaCommand()
    {
        ResolvedTalentModifiers talents = Talents(Hook("F-1-3", 20, secondaryValue: 6));
        AbilityDefinition fireball = Ability("MAGE_FIREBALL", "FIRE") with { ResourceCost = 100 };
        ArenaTestEntrant caster = ArenaFighterAssembler.Create(
            Player(fireball.Id, talents), 20,
            new Dictionary<string, AbilityDefinition> { [fireball.Id] = fireball },
            hasCompanion: false);
        ArenaTestEntrant opponent = ArenaFighterAssembler.Create(
            Player("MAGE_FIRE_BLAST", ResolvedTalentModifiers.Empty), 20,
            new Dictionary<string, AbilityDefinition>
            {
                ["MAGE_FIRE_BLAST"] = Ability("MAGE_FIRE_BLAST", "FIRE")
            },
            hasCompanion: false);
        DateTimeOffset now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        var session = new ArenaCombatSession(Guid.NewGuid(), caster.Fighter, opponent.Fighter,
            new SeededGameRandom(42), now);

        ArenaCommandResult result = session.UseAbility(caster.Fighter.AccountId, "cast-1",
            fireball.Id, opponent.Fighter.Actor.ActorId, now);

        Assert.True(result.Succeeded);
        Assert.Equal(6m, session.Snapshot.ActorA.CurrentResource);
    }

    [Fact]
    public void ArcaneStatelessHooksAreAcceptedAndApplied()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(MageStaticAbilityHookResolver.ArcaneFocusTalentId, 4),
            Hook(MageStaticAbilityHookResolver.ImprovedArcaneMissilesTalentId, 20,
                secondaryValue: 10),
            Hook(MageStaticAbilityHookResolver.ArcaneInstabilityTalentId, 5,
                secondaryValue: 3));
        var missiles = new AbilityDefinition(
            "MAGE_ARCANE_MISSILES",
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            100,
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(3),
            false,
            GlobalCooldownCategory.None,
            true,
            "ARCANE",
            Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10)]);

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            Player(missiles.Id, talents),
            20,
            new Dictionary<string, AbilityDefinition> { [missiles.Id] = missiles },
            hasCompanion: false);

        AbilityDefinition resolved = entrant.Fighter.Abilities[missiles.Id];
        Assert.Equal(90m, resolved.ResourceCost);
        Assert.Equal(1.26m, resolved.DamageMultiplier);
        Assert.Equal(4m, resolved.AccuracyBonus);
        Assert.Equal(3m, resolved.CriticalChanceBonus);
    }

    [Fact]
    public void FrostStatelessHooksAreAcceptedAndApplied()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(MageStaticAbilityHookResolver.ImprovedIceShardTalentId, 0.5m),
            Hook(MageStaticAbilityHookResolver.FrostPrecisionTalentId, 3,
                secondaryValue: 10),
            Hook(MageStaticAbilityHookResolver.PiercingIceTalentId, 5),
            Hook(MageStaticAbilityHookResolver.IceShardsTalentId, 20),
            Hook(MageStaticAbilityHookResolver.FocusedIceTalentId, 20));
        var iceShard = new AbilityDefinition(
            "MAGE_ICE_SHARD",
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            100,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(3),
            false,
            GlobalCooldownCategory.None,
            true,
            "FROST",
            Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10)]);

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            Player(iceShard.Id, talents),
            20,
            new Dictionary<string, AbilityDefinition> { [iceShard.Id] = iceShard },
            hasCompanion: false);

        AbilityDefinition resolved = entrant.Fighter.Abilities[iceShard.Id];
        Assert.Equal(72m, resolved.ResourceCost);
        Assert.Equal(1.05m, resolved.DamageMultiplier);
        Assert.Equal(3m, resolved.AccuracyBonus);
        Assert.Equal(20m, resolved.CriticalDamageBonus);
        Assert.Equal(TimeSpan.FromSeconds(2.5), resolved.CastTime);
    }

    [Fact]
    public void FrostHookWithAdditionalStatefulSlowBehaviourIsDormantInOneVsOne()
    {
        ResolvedTalentModifiers talents = Talents(Hook("I-3-3", 10));
        AbilityDefinition blizzard = Ability("MAGE_BLIZZARD", "FROST");

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            Player(blizzard.Id, talents),
            20,
            new Dictionary<string, AbilityDefinition> { [blizzard.Id] = blizzard },
            hasCompanion: false);

        Assert.Empty(entrant.Fighter.EffectiveTalentModifiers.EventHooks);
    }

    private static CombatPlayerDefinition Player(
        string abilityId,
        ResolvedTalentModifiers talents)
    {
        Guid character = Guid.NewGuid();
        var actor = new CombatActorState(character, 100, 100, 100, 100, CombatStats.Default);
        var participant = new CombatParticipantDefinition(
            actor,
            CombatActorKind.Player,
            "MAGE",
            "Test Mage",
            "MANA",
            new AutoAttackProfile(TimeSpan.FromSeconds(3), 5, 0, 0),
            new HashSet<string>(StringComparer.Ordinal) { abilityId },
            GenderId: "FEMALE");
        return new CombatPlayerDefinition(Guid.NewGuid(), participant, talents);
    }

    private static ResolvedTalentModifiers Talents(params ResolvedTalentEventHook[] hooks) => new(
        new TalentStatModifiers(),
        new TalentCombatModifiers(),
        new HashSet<string>(StringComparer.Ordinal),
        new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
        hooks,
        []);

    private static ResolvedTalentEventHook Hook(
        string talentId,
        decimal value,
        decimal secondaryValue = 0) => new(
            talentId,
            TalentModifierKeys.OnAbilityUsed,
            1,
            value,
            null,
            TimeSpan.Zero,
            false,
            SecondaryValue: secondaryValue);

    private static AbilityDefinition Ability(string id, string school) => new(
        id,
        AbilityType.Instant,
        AbilityTargetType.SingleEnemy,
        0,
        TimeSpan.Zero,
        TimeSpan.Zero,
        false,
        GlobalCooldownCategory.None,
        true,
        school,
        Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10)]);
}
