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
    public void BuildWithUnhandledTalentHookIsRejectedBeforeQueueEntry()
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

        Assert.Throws<NotSupportedException>(() => ArenaFighterAssembler.Create(
            player, 15, new Dictionary<string, AbilityDefinition>
            { ["FIREBALL"] = Ability("FIREBALL") }, hasCompanion: false));
    }

    [Fact]
    public void UnknownOrUnsupportedAbilityCannotEnterQueue()
    {
        Assert.Throws<NotSupportedException>(() => ArenaFighterAssembler.Create(
            Player(new HashSet<string> { "MISSING" }), 15, new Dictionary<string, AbilityDefinition>(),
            hasCompanion: false));
    }

    private static CombatPlayerDefinition Player(
        IReadOnlySet<string> abilityIds,
        ResolvedTalentModifiers? talents = null)
    {
        Guid character = Guid.NewGuid();
        var actor = new CombatActorState(character, 100, 100, 100, 100, CombatStats.Default);
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player,
            "MAGE", "Test Mage", "MANA", new AutoAttackProfile(TimeSpan.FromSeconds(3), 5, 0, 0),
            abilityIds, GenderId: "FEMALE");
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

    private static AbilityDefinition Ability(string id) => new(id, AbilityType.Instant,
        AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, TimeSpan.Zero, false,
        GlobalCooldownCategory.None, true, "FIRE",
        Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10)]);
}
