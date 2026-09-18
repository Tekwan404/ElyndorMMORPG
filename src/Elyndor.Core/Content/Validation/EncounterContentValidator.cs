using Elyndor.Core.Combat.Encounters;

namespace Elyndor.Core.Content;

public sealed class EncounterContentValidator : IContentValidationStage
{
    public void Validate(ContentValidationContext context)
    {
        IReadOnlyList<EncounterDefinition> encounters = context.Package.Encounters ?? [];
        if (encounters.Count == 0)
            return;

        HashSet<string> monsterIds = (context.Package.Monsters ?? [])
            .Select(monster => monster.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> abilityIds = (context.Package.Abilities ?? [])
            .Select(ability => ability.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> effectIds = (context.Package.Effects ?? [])
            .Select(effect => effect.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> encounterIds = new(StringComparer.Ordinal);
        HashSet<string> encounterMonsterIds = new(StringComparer.Ordinal);

        for (var encounterIndex = 0; encounterIndex < encounters.Count; encounterIndex++)
        {
            EncounterDefinition encounter = encounters[encounterIndex];
            string path = $"encounters[{encounterIndex}]";

            if (!encounterIds.Add(encounter.Id))
            {
                context.Errors.Add(new ContentValidationError(
                    "DUPLICATE_ENCOUNTER_ID",
                    path,
                    $"Encounter '{encounter.Id}' is duplicated."));
            }
            if (!string.IsNullOrWhiteSpace(encounter.MonsterId)
                && !encounterMonsterIds.Add(encounter.MonsterId))
            {
                context.Errors.Add(new ContentValidationError(
                    "DUPLICATE_ENCOUNTER_MONSTER",
                    $"{path}.monsterId",
                    $"Monster '{encounter.MonsterId}' has more than one active encounter definition."));
            }

            foreach (string error in EncounterDefinitionValidator.Validate(encounter))
            {
                context.Errors.Add(new ContentValidationError(
                    "INVALID_ENCOUNTER",
                    path,
                    error));
            }

            if (!monsterIds.Contains(encounter.MonsterId))
            {
                context.Errors.Add(new ContentValidationError(
                    "UNKNOWN_ENCOUNTER_MONSTER",
                    $"{path}.monsterId",
                    $"Encounter '{encounter.Id}' references unknown monster '{encounter.MonsterId}'."));
            }

            HashSet<string> phaseIds = encounter.Phases
                .Select(phase => phase.Id)
                .ToHashSet(StringComparer.Ordinal);
            for (var phaseIndex = 0; phaseIndex < encounter.Phases.Count; phaseIndex++)
            {
                EncounterPhaseDefinition phase = encounter.Phases[phaseIndex];
                string phasePath = $"{path}.phases[{phaseIndex}]";

                ValidateAbilityIds(
                    phase.AbilityIds,
                    abilityIds,
                    $"{phasePath}.abilityIds",
                    encounter.Id,
                    context.Errors);

                for (var actionIndex = 0; actionIndex < phase.Actions.Count; actionIndex++)
                {
                    EncounterActionDefinition action = phase.Actions[actionIndex];
                    string actionPath = $"{phasePath}.actions[{actionIndex}]";
                    switch (action.Type)
                    {
                        case EncounterActionType.Summon when action.Summon is { } summon
                            && !monsterIds.Contains(summon.MonsterId):
                            context.Errors.Add(new ContentValidationError(
                                "UNKNOWN_SUMMON_MONSTER",
                                $"{actionPath}.summon.monsterId",
                                $"Encounter '{encounter.Id}' summons unknown monster '{summon.MonsterId}'."));
                            break;
                        case EncounterActionType.ApplyEffect
                            when action.EffectId is { Length: > 0 } effectId
                                 && !effectIds.Contains(effectId):
                            context.Errors.Add(new ContentValidationError(
                                "UNKNOWN_ENCOUNTER_EFFECT",
                                $"{actionPath}.effectId",
                                $"Encounter '{encounter.Id}' references unknown effect '{effectId}'."));
                            break;
                        case EncounterActionType.ChangeAbilitySet:
                            ValidateAbilityIds(
                                action.AbilityIds,
                                abilityIds,
                                $"{actionPath}.abilityIds",
                                encounter.Id,
                                context.Errors);
                            break;
                        case EncounterActionType.SetPhase
                            when action.PhaseId is { Length: > 0 } targetPhase
                                 && !phaseIds.Contains(targetPhase):
                            context.Errors.Add(new ContentValidationError(
                                "UNKNOWN_ENCOUNTER_PHASE",
                                $"{actionPath}.phaseId",
                                $"Encounter '{encounter.Id}' references unknown phase '{targetPhase}'."));
                            break;
                    }
                }
            }
        }
    }

    private static void ValidateAbilityIds(
        IReadOnlyList<string>? ids,
        HashSet<string> knownAbilityIds,
        string path,
        string encounterId,
        List<ContentValidationError> errors)
    {
        if (ids is null)
            return;

        for (var index = 0; index < ids.Count; index++)
        {
            string id = ids[index];
            if (knownAbilityIds.Contains(id))
                continue;

            errors.Add(new ContentValidationError(
                "UNKNOWN_ENCOUNTER_ABILITY",
                $"{path}[{index}]",
                $"Encounter '{encounterId}' references unknown ability '{id}'."));
        }
    }
}
