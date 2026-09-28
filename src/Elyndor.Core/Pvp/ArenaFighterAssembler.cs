using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;

namespace Elyndor.Core.Pvp;

public static class ArenaFighterAssembler
{
    public static ArenaTestEntrant Create(CombatPlayerDefinition player, int level,
        IReadOnlyDictionary<string, AbilityDefinition> availableAbilities,
        bool hasAllocatedTalents, bool hasCompanion)
    {
        if (player.Participant.Kind != CombatActorKind.Player || level < 1)
            throw new ArgumentException("Arena entrant must be a valid player.");
        if (hasAllocatedTalents || hasCompanion || player.Participant.OffHandAutoAttack is not null)
            throw new NotSupportedException("This build has PvE-only arena mechanics.");

        var known = new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal);
        foreach (string abilityId in player.Participant.KnownAbilityIds)
        {
            if (!availableAbilities.TryGetValue(abilityId, out AbilityDefinition? ability))
                throw new NotSupportedException($"Ability {abilityId} is missing from arena content.");
            known.Add(abilityId, ability);
        }
        ArenaCombatSession.ValidateAbilities(known);
        var fighter = new ArenaFighter(player.AccountId, player.Participant.Actor.ActorId,
            player.Participant.Actor, known, player.Participant.AutoAttack);
        return new ArenaTestEntrant(fighter, level, player.Participant.Name,
            player.Participant.DefinitionId, player.Participant.GenderId ?? "UNKNOWN",
            player.Participant.SkinId, player.Participant.ResourceType);
    }
}
