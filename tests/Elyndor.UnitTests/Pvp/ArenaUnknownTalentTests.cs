using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Pvp;
using Elyndor.Core.Talents;

namespace Elyndor.UnitTests.Pvp;

public sealed class ArenaUnknownTalentTests
{
    [Fact]
    public void UnknownOwnerHookIsRejectedInsteadOfSilentlyIgnored()
    {
        var actor = new CombatActorState(Guid.NewGuid(), 100, 100, 100, 100, CombatStats.Default);
        var participant = new CombatParticipantDefinition(actor, CombatActorKind.Player,
            "MAGE", "Mage", "MANA", new AutoAttackProfile(TimeSpan.FromSeconds(2), 1, 0, 0),
            new HashSet<string>());
        var hook = new ResolvedTalentEventHook("UNKNOWN_NODE", TalentModifierKeys.OnAbilityUsed,
            1, 1, "UNKNOWN_MECHANIC", TimeSpan.Zero, false);
        ResolvedTalentModifiers talents = ResolvedTalentModifiers.Empty with { EventHooks = [hook] };
        Assert.Throws<NotSupportedException>(() => ArenaFighterAssembler.Create(
            new CombatPlayerDefinition(Guid.NewGuid(), participant, talents), 1,
            new Dictionary<string, Elyndor.Core.Combat.Abilities.AbilityDefinition>(), false));
    }
}
