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
            hasAllocatedTalents: false, hasCompanion: false);
        Assert.Equal(player.AccountId, entrant.Fighter.AccountId);
        Assert.Equal(player.Participant.Actor.ActorId, entrant.Fighter.CharacterId);
        Assert.Equal(["FIREBALL"], entrant.Fighter.Abilities.Keys);
        Assert.Equal(15, entrant.Level);
    }

    [Fact]
    public void BuildWithUnhandledTalentHookIsRejectedBeforeQueueEntry()
    {
        Assert.Throws<NotSupportedException>(() => ArenaFighterAssembler.Create(
            Player(new HashSet<string> { "FIREBALL" }), 15, new Dictionary<string, AbilityDefinition>
            { ["FIREBALL"] = Ability("FIREBALL") }, hasAllocatedTalents: true, hasCompanion: false));
    }

    [Fact]
    public void UnknownOrUnsupportedAbilityCannotEnterQueue()
    {
        Assert.Throws<NotSupportedException>(() => ArenaFighterAssembler.Create(
            Player(new HashSet<string> { "MISSING" }), 15, new Dictionary<string, AbilityDefinition>(),
            hasAllocatedTalents: false, hasCompanion: false));
    }

    private static CombatPlayerDefinition Player(IReadOnlySet<string> abilityIds)
    {
        Guid character = Guid.NewGuid();
        var actor = new CombatActorState(character, 100, 100, 100, 100, CombatStats.Default);
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player,
            "MAGE", "Test Mage", "MANA", new AutoAttackProfile(TimeSpan.FromSeconds(3), 5, 0, 0),
            abilityIds, GenderId: "FEMALE");
        return new CombatPlayerDefinition(Guid.NewGuid(), participant, ResolvedTalentModifiers.Empty);
    }

    private static AbilityDefinition Ability(string id) => new(id, AbilityType.Instant,
        AbilityTargetType.SingleEnemy, 0, TimeSpan.Zero, TimeSpan.Zero, false,
        GlobalCooldownCategory.None, true, "Fire",
        Actions: [new AbilityActionDefinition(AbilityActionType.Damage, 10)]);
}
