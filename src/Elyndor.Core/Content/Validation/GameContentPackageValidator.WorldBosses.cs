using Elyndor.Core.WorldBosses;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    internal static void ValidateWorldBossDefinitions(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        HashSet<string> ids = [];
        IReadOnlyList<WorldBossDefinition> definitions = package.WorldBosses ?? [];
        for (int index = 0; index < definitions.Count; index++)
        {
            WorldBossDefinition definition = definitions[index];
            string path = $"worldBosses[{index}]";
            bool idValid = ValidateIdentifier(
                definition.Id,
                "INVALID_WORLD_BOSS_ID",
                $"{path}.id",
                errors);
            if (idValid && !ids.Add(definition.Id))
            {
                errors.Add(new(
                    "DUPLICATE_WORLD_BOSS_ID",
                    path,
                    $"World boss '{definition.Id}' is duplicated."));
            }

            if (string.IsNullOrWhiteSpace(definition.Name)
                || definition.Level <= 0
                || definition.BaseMaxHealth <= 0
                || definition.DurationSeconds <= 0
                || string.IsNullOrWhiteSpace(definition.EncounterProfileId)
                || string.IsNullOrWhiteSpace(definition.LootTableId)
                || string.IsNullOrWhiteSpace(definition.RewardProfileId)
                || string.IsNullOrWhiteSpace(definition.TokenCurrencyId)
                || definition.Phases.Count == 0)
            {
                errors.Add(new(
                    "INVALID_WORLD_BOSS_DEFINITION",
                    path,
                    $"World boss '{definition.Id}' contains invalid required values."));
                continue;
            }

            decimal previousThreshold = decimal.MaxValue;
            for (int phaseIndex = 0; phaseIndex < definition.Phases.Count; phaseIndex++)
            {
                WorldBossPhaseDefinition phase = definition.Phases[phaseIndex];
                string phasePath = $"{path}.phases[{phaseIndex}]";
                bool invalid = phase.Phase != phaseIndex + 1
                    || string.IsNullOrWhiteSpace(phase.Name)
                    || phase.StartsAtHealthPercent <= 0
                    || phase.StartsAtHealthPercent > 100
                    || phase.StartsAtHealthPercent >= previousThreshold
                    || phaseIndex == 0 && phase.StartsAtHealthPercent != 100;
                if (invalid)
                {
                    errors.Add(new(
                        "INVALID_WORLD_BOSS_PHASE",
                        phasePath,
                        $"World boss '{definition.Id}' has an invalid phase sequence."));
                }

                previousThreshold = phase.StartsAtHealthPercent;
            }
        }
    }
}
