using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Pvp;

public static class ArenaFighterAssembler
{
    public static ArenaTestEntrant Create(CombatPlayerDefinition player, int level,
        IReadOnlyDictionary<string, AbilityDefinition> availableAbilities,
        bool hasCompanion)
    {
        if (player.Participant.Kind != CombatActorKind.Player || level < 1)
            throw new ArgumentException("Arena entrant must be a valid player.");
        if (hasCompanion || player.Participant.OffHandAutoAttack is not null)
            throw new NotSupportedException("This build has PvE-only arena mechanics.");

        ValidateTalentRuntime(player);
        ArenaTalentRuntimeSupport.ConfigureActorRuntime(
            player.Participant.Actor,
            player.TalentModifiers);

        var known = new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal);
        foreach (string abilityId in player.Participant.KnownAbilityIds)
        {
            if (!availableAbilities.TryGetValue(abilityId, out AbilityDefinition? ability))
                throw new NotSupportedException($"Ability {abilityId} is missing from arena content.");

            AbilityDefinition resolved = ArenaTalentRuntimeSupport.ApplyAbilityDefinitionModifiers(
                ability,
                player.TalentModifiers);
            known.Add(abilityId, resolved);
        }

        ArenaCombatSession.ValidateAbilities(known);
        var fighter = new ArenaFighter(player.AccountId, player.Participant.Actor.ActorId,
            player.Participant.Actor, known, player.Participant.AutoAttack, player.TalentModifiers);
        return new ArenaTestEntrant(fighter, level, player.Participant.Name,
            player.Participant.DefinitionId, player.Participant.GenderId ?? "UNKNOWN",
            player.Participant.SkinId, player.Participant.ResourceType);
    }

    private static void ValidateTalentRuntime(CombatPlayerDefinition player)
    {
        ResolvedTalentModifiers talents = player.TalentModifiers;
        if (talents.DeferredHooks.Count > 0)
            throw new NotSupportedException("This build has unsupported deferred talent hooks.");

        IReadOnlyList<ResolvedTalentEventHook> unsupported =
            ArenaTalentRuntimeSupport.UnsupportedEventHooks(talents);
        if (unsupported.Count == 0)
            return;

        string mechanics = string.Join(", ", unsupported
            .Select(ArenaTalentRuntimeSupport.DescribeUnsupportedHook)
            .Distinct(StringComparer.Ordinal));
        throw new NotSupportedException(
            $"This build has unsupported arena talent hooks: {mechanics}.");
    }
}
