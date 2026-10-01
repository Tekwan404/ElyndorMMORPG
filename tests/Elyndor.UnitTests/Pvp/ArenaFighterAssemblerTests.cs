using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaFighterAssemblerTests
{
    [Fact]
    public void RealPlayerIdentityAndKnownAbilitiesAreCaptured()
    {
        var player = Player(new HashSet<string> { "FIREBALL" });
        var abilities = new Dictionary<string, AbilityDefinition>
        {
            ["FIREBALL"] = Ability("FIREBALL")
        };
        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(player, 15, abilities,
            hasCompanion: false);
        Assert.Equal(player.AccountId, entrant.Fighter.AccountId);
        Assert.Equal(player.Participant.Actor.ActorId, entrant.Fighter.CharacterId);
        Assert.Equal(["FIREBALL"], entrant.Fighter.Abilities.Keys);
        Assert.Equal(15, entrant.Level);
    }

    [Fact]
    public void TalentUnlockedAbilityIsAddedToArenaAbilitySet()
    {
        ResolvedTalentModifiers talents = new(
            new TalentStatModifiers(),
            new TalentCombatModifiers(),
            new HashSet<string>(StringComparer.Ordinal) { "TALENT_UNLOCKED_STRIKE" },
            new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
            [],
            []);
        CombatPlayerDefinition player = Player(new HashSet<string> { "FIREBALL" }, talents);
        AbilityDefinition unlocked = Ability("TALENT_UNLOCKED_STRIKE");

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            player,
            15,
            new Dictionary<string, AbilityDefinition>
            {
                ["FIREBALL"] = Ability("FIREBALL"),
                [unlocked.Id] = unlocked
            },
            hasCompanion: false);

        Assert.Contains(unlocked.Id, entrant.Fighter.Abilities.Keys);
    }

    [Fact]
    public void StaticAbilityTalentModifierIsAppliedBeforeQueueEntry()
    {
        var talents = new ResolvedTalentModifiers(
            new TalentStatModifiers(),
            new TalentCombatModifiers(),
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal)
            {
                ["FIREBALL"] = new TalentAbilityModifiers(DamagePercentBonus: 25)
            },
            [],
            []);
        CombatPlayerDefinition player = Player(new HashSet<string> { "FIREBALL" }, talents);
        var abilities = new Dictionary<string, AbilityDefinition>
        {
            ["FIREBALL"] = Ability("FIREBALL")
        };

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(player, 15, abilities,
            hasCompanion: false);

        AbilityActionDefinition damage = Assert.Single(entrant.Fighter.Abilities["FIREBALL"].Actions!);
        Assert.Equal(12.5m, damage.Amount);
    }

    [Fact]
    public void StatOnlyTalentBuildIsAccepted()
    {
        var talents = new ResolvedTalentModifiers(
            new TalentStatModifiers(IntellectPercent: 10, CriticalChancePercent: 5),
            new TalentCombatModifiers(DamageDealtPercent: 3),
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, TalentAbilityModifiers>(StringComparer.Ordinal),
            [],
            []);
        CombatPlayerDefinition player = Player(new HashSet<string> { "FIREBALL" }, talents);

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            player,
            15,
            new Dictionary<string, AbilityDefinition> { ["FIREBALL"] = Ability("FIREBALL") },
            hasCompanion: false);

        Assert.Same(talents, entrant.Fighter.EffectiveTalentModifiers);
    }

    [Fact]
    public void StatelessPyromancerFireballHooksAreAppliedBeforeQueueEntry()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(PyromancerStaticAbilityHookResolver.ImprovedFireballTalentId, 0.5m),
            Hook(PyromancerStaticAbilityHookResolver.EfficientMagicTalentId, 10),
            Hook(PyromancerStaticAbilityHookResolver.CriticalMassTalentId, 5),
            Hook(PyromancerStaticAbilityHookResolver.FirePowerTalentId, 20));
        CombatPlayerDefinition player = Player(
            new HashSet<string> { "MAGE_FIREBALL" }, talents);
        var fireball = new AbilityDefinition(
            "MAGE_FIREBALL",
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            100,
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(3),
            false,
            GlobalCooldownCategory.None,
            true,
            "FIRE",
            Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10)]);

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            player,
            15,
            new Dictionary<string, AbilityDefinition> { [fireball.Id] = fireball },
            hasCompanion: false);

        AbilityDefinition resolved = entrant.Fighter.Abilities[fireball.Id];
        Assert.Equal(90m, resolved.ResourceCost);
        Assert.Equal(TimeSpan.FromSeconds(2.5), resolved.CastTime);
        Assert.Equal(1.2m, resolved.DamageMultiplier);
        Assert.Equal(5m, resolved.CriticalChanceBonus);
    }

    [Fact]
    public void StatelessPyromancerFireBlastHooksCompose()
    {
        ResolvedTalentModifiers talents = Talents(
            Hook(PyromancerStaticAbilityHookResolver.IncinerationTalentId, 3),
            Hook(PyromancerStaticAbilityHookResolver.ImprovedFireBlastTalentId, 2,
                secondaryValue: 4));
        CombatPlayerDefinition player = Player(
            new HashSet<string> { "MAGE_FIRE_BLAST" }, talents);
        var fireBlast = new AbilityDefinition(
            "MAGE_FIRE_BLAST",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            20,
            TimeSpan.FromSeconds(8),
            TimeSpan.Zero,
            false,
            GlobalCooldownCategory.None,
            true,
            "FIRE",
            Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10)]);

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            player,
            15,
            new Dictionary<string, AbilityDefinition> { [fireBlast.Id] = fireBlast },
            hasCompanion: false);

        AbilityDefinition resolved = entrant.Fighter.Abilities[fireBlast.Id];
        Assert.Equal(TimeSpan.FromSeconds(6), resolved.Cooldown);
        Assert.Equal(7m, resolved.CriticalChanceBonus);
    }

    [Fact]
    public void ImpactTalentHookIsAcceptedBeforeQueueEntry()
    {
        CombatPlayerDefinition player = Player(
            new HashSet<string> { "FIREBALL" },
            Talents(new ResolvedTalentEventHook(
                PyromancerImpactRuntime.TalentId,
                TalentModifierKeys.OnAbilityUsed,
                1,
                100,
                null,
                TimeSpan.Zero,
                false,
                Duration: TimeSpan.FromSeconds(4))));

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            player,
            15,
            new Dictionary<string, AbilityDefinition> { ["FIREBALL"] = Ability("FIREBALL") },
            hasCompanion: false);

        Assert.Contains(entrant.Fighter.EffectiveTalentModifiers.EventHooks,
            hook => hook.TalentId == PyromancerImpactRuntime.TalentId);
    }

    [Fact]
    public void BuildWithUnhandledTalentHookPreservesItForRuntimeAndCapabilityAudit()
    {
        CombatPlayerDefinition player = Player(
            new HashSet<string> { "FIREBALL" },
            Talents(new ResolvedTalentEventHook(
                "F-2-1",
                TalentModifierKeys.OnAbilityUsed,
                1,
                20,
                null,
                TimeSpan.Zero,
                false)));

        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(
            player, 15, new Dictionary<string, AbilityDefinition>
            { ["FIREBALL"] = Ability("FIREBALL") }, hasCompanion: false);

        Assert.Contains(entrant.Fighter.EffectiveTalentModifiers.EventHooks,
            hook => hook.TalentId == "F-2-1");
        Assert.Contains(ArenaTalentRuntimeSupport.UnsupportedEventHooks(
            entrant.Fighter.EffectiveTalentModifiers), hook => hook.TalentId == "F-2-1");
    }

    [Fact]
    public void UnknownOrUnsupportedAbilityCannotEnterQueue()
    {
        Assert.Throws<NotSupportedException>(() => ArenaFighterAssembler.Create(
            Player(new HashSet<string> { "MISSING" }), 15, new Dictionary<string, AbilityDefinition>(),
            hasCompanion: false));
    }

    [Fact]
    public void OffHandProfileIsPreservedForArenaCombat()
    {
        var offHand = new AutoAttackProfile(TimeSpan.FromSeconds(2), 7, 0, 0);
        var player = Player(new HashSet<string> { "FIREBALL" }, offHand: offHand);
        ArenaTestEntrant entrant = ArenaFighterAssembler.Create(player, 15,
            new Dictionary<string, AbilityDefinition> { ["FIREBALL"] = Ability("FIREBALL") },
            hasCompanion: false);
        Assert.Same(offHand, entrant.Fighter.OffHandAutoAttack);
    }

    private static CombatPlayerDefinition Player(
        IReadOnlySet<string> abilityIds,
        ResolvedTalentModifiers? talents = null,
        AutoAttackProfile? offHand = null)
    {
        Guid character = Guid.NewGuid();
        var actor = new CombatActorState(character, 100, 100, 100, 100, CombatStats.Default);
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player,
            "MAGE", "Test Mage", "MANA", new AutoAttackProfile(TimeSpan.FromSeconds(3), 5, 0, 0),
            abilityIds, OffHandAutoAttack: offHand, GenderId: "FEMALE");
        return new CombatPlayerDefinition(Guid.NewGuid(), participant,
            talents ?? ResolvedTalentModifiers.Empty);
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

    private static AbilityDefinition Ability(string id) => new(id, AbilityType.Instant,
        AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, TimeSpan.Zero, false,
        GlobalCooldownCategory.None, true, "FIRE",
        Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10)]);
}
