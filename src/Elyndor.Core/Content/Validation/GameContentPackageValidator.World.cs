using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
        internal static void ValidateLocations(
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

        private static void ValidateLocationTransitions(
            IReadOnlyList<LocationDefinition> locations,
            HashSet<string> locationIds,
            List<ContentValidationError> errors)
        {
            for (var locationIndex = 0; locationIndex < locations.Count; locationIndex++)
            {
                LocationDefinition location = locations[locationIndex];
                HashSet<string> transitions = [];

                for (var transitionIndex = 0;
                     transitionIndex < location.Transitions.Count;
                     transitionIndex++)
                {
                    string targetId = location.Transitions[transitionIndex];
                    string path = $"locations[{locationIndex}].transitions[{transitionIndex}]";
                    bool targetIsValid = ValidateIdentifier(
                        targetId,
                        "INVALID_LOCATION_TRANSITION_ID",
                        path,
                        errors);

                    if (!targetIsValid)
                    {
                        continue;
                    }

                    if (!transitions.Add(targetId))
                    {
                        errors.Add(new ContentValidationError(
                            "DUPLICATE_LOCATION_TRANSITION",
                            path,
                            $"Transition to '{targetId}' is duplicated."));
                    }

                    if (string.Equals(location.Id, targetId, StringComparison.Ordinal))
                    {
                        errors.Add(new ContentValidationError(
                            "SELF_LOCATION_TRANSITION",
                            path,
                            $"Location '{location.Id}' cannot transition to itself."));
                    }
                    else if (!locationIds.Contains(targetId))
                    {
                        errors.Add(new ContentValidationError(
                            "MISSING_LOCATION_TRANSITION",
                            path,
                            $"Transition target '{targetId}' does not exist."));
                    }
                }
            }
        }

}
