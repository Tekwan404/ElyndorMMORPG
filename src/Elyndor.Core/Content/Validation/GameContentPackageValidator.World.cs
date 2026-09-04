using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
        internal static readonly HashSet<string> AllowedDangerLevels =
            ["SAFE", "ADVENTURE", "DANGEROUS"];

        public static IReadOnlyList<ContentValidationError> Validate(GameContentPackage package)
        {
            ArgumentNullException.ThrowIfNull(package);

            List<ContentValidationError> errors = [];

            ValidateMetadata(package, errors);

            HashSet<ContentKey> definitions = [];

            for (var index = 0; index < package.Definitions.Count; index++)
            {
                GameContentDefinition definition = package.Definitions[index];
                string path = $"definitions[{index}]";

                bool typeIsValid = ValidateIdentifier(
                    definition.Type,
                    "INVALID_DEFINITION_TYPE",
                    $"{path}.type",
                    errors);
                bool idIsValid = ValidateIdentifier(
                    definition.Id,
                    "INVALID_DEFINITION_ID",
                    $"{path}.id",
                    errors);

                if (typeIsValid && idIsValid && !definitions.Add(new ContentKey(definition.Type, definition.Id)))
                {
                    errors.Add(new ContentValidationError(
                        "DUPLICATE_DEFINITION_ID",
                        path,
                        $"Definition '{definition.Type}:{definition.Id}' is duplicated."));
                }
            }

            ValidateReferences(package, definitions, errors);
            ValidateLocations(package.Locations, errors);
            ValidateCharacterProfiles(package, definitions, errors);
            ValidateCombatDefinitions(package, errors);
            ValidateMonsterDefinitions(package, errors);
            ValidateTalentDefinitions(package.TalentTrees ?? [], package.Abilities ?? [], errors);
            ValidateProgressionItemsAndLoot(package, errors);

            return errors;
        }

        private static void ValidateLocations(
            IReadOnlyList<LocationDefinition> locations,
            List<ContentValidationError> errors)
        {
            HashSet<string> locationIds = [];

            for (var index = 0; index < locations.Count; index++)
            {
                LocationDefinition location = locations[index];
                string path = $"locations[{index}]";

                bool idIsValid = ValidateIdentifier(
                    location.Id,
                    "INVALID_LOCATION_ID",
                    $"{path}.id",
                    errors);

                if (idIsValid && !locationIds.Add(location.Id))
                {
                    errors.Add(new ContentValidationError(
                        "DUPLICATE_LOCATION_ID",
                        $"{path}.id",
                        $"Location '{location.Id}' is duplicated."));
                }

                if (string.IsNullOrWhiteSpace(location.DisplayName))
                {
                    errors.Add(new ContentValidationError(
                        "MISSING_LOCATION_DISPLAY_NAME",
                        $"{path}.displayName",
                        "Location display name is required."));
                }

                if (!AllowedDangerLevels.Contains(location.DangerLevel))
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_LOCATION_DANGER_LEVEL",
                        $"{path}.dangerLevel",
                        $"Location danger level '{location.DangerLevel}' is not supported."));
                }

                if (location.RecommendedLevel <= 0)
                {
                    errors.Add(new ContentValidationError(
                        "INVALID_LOCATION_RECOMMENDED_LEVEL",
                        $"{path}.recommendedLevel",
                        "Location recommended level must be positive."));
                }
            }

            ValidateLocationTransitions(locations, locationIds, errors);
        }

}
