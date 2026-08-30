using Elyndor.Core.World;

namespace Elyndor.Core.Content;

public static class GameContentPackageValidator
{
    private static readonly HashSet<string> AllowedDangerLevels =
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

        return errors;
    }

    private static void ValidateCharacterProfiles(
        GameContentPackage package,
        IReadOnlySet<ContentKey> definitions,
        List<ContentValidationError> errors)
    {
        if (package.ClassProfiles is null
            || package.StatFormula is null
            || package.ResourceProfiles is null)
        {
            return;
        }

        HashSet<string> resourceIds = [];
        for (var index = 0; index < package.ResourceProfiles.Count; index++)
        {
            ResourceProfile profile = package.ResourceProfiles[index];
            string path = $"resourceProfiles[{index}]";
            if (!resourceIds.Add(profile.Id))
            {
                errors.Add(new ContentValidationError(
                    "DUPLICATE_RESOURCE_PROFILE",
                    path,
                    $"Resource profile '{profile.Id}' is duplicated."));
            }

            if (profile.MaxValue <= 0
                || profile.StartValue < 0
                || profile.StartValue > profile.MaxValue
                || profile.RespawnValue < 0
                || profile.RespawnValue > profile.MaxValue
                || profile.CombatRegenPerSecond < 0
                || profile.OutOfCombatRegenPerSecond < 0
                || profile.OutOfCombatDecayPerSecond < 0
                || profile.OutOfCombatDelaySeconds < 0)
            {
                errors.Add(new ContentValidationError(
                    "INVALID_RESOURCE_PROFILE",
                    path,
                    $"Resource profile '{profile.Id}' contains values outside its valid range."));
            }
        }

        HashSet<string> classIds = [];
        for (var index = 0; index < package.ClassProfiles.Count; index++)
        {
            ClassProfile profile = package.ClassProfiles[index];
            string path = $"classProfiles[{index}]";
            if (!classIds.Add(profile.Id))
            {
                errors.Add(new ContentValidationError(
                    "DUPLICATE_CLASS_PROFILE",
                    path,
                    $"Class profile '{profile.Id}' is duplicated."));
            }

            if (!definitions.Contains(new ContentKey("CLASS", profile.Id)))
            {
                errors.Add(new ContentValidationError(
                    "MISSING_CLASS_DEFINITION",
                    path,
                    $"Class definition '{profile.Id}' does not exist."));
            }

            if (!resourceIds.Contains(profile.ResourceProfileId))
            {
                errors.Add(new ContentValidationError(
                    "MISSING_RESOURCE_PROFILE",
                    path,
                    $"Resource profile '{profile.ResourceProfileId}' does not exist."));
            }

            if (profile.BaseStats.Strength < 0
                || profile.BaseStats.Agility < 0
                || profile.BaseStats.Intellect < 0
                || profile.BaseStats.Stamina < 0
                || profile.LevelGrowth.Strength < 0
                || profile.LevelGrowth.Agility < 0
                || profile.LevelGrowth.Intellect < 0
                || profile.LevelGrowth.Stamina < 0)
            {
                errors.Add(new ContentValidationError(
                    "INVALID_CLASS_STATS",
                    path,
                    $"Class profile '{profile.Id}' contains negative stats."));
            }
        }

        foreach (string requiredClassId in new[] { "WARRIOR", "ARCHER", "MAGE" })
        {
            if (!classIds.Contains(requiredClassId))
            {
                errors.Add(new ContentValidationError(
                    "MISSING_PROTOTYPE_CLASS_PROFILE",
                    "classProfiles",
                    $"Prototype class profile '{requiredClassId}' is required."));
            }
        }

        if (package.StatFormula.MaxHpBase <= 0
            || package.StatFormula.MaxHpPerStamina < 0
            || package.StatFormula.CriticalChanceBase is < 0 or > 100
            || package.StatFormula.CriticalDamageBase < 0
            || package.StatFormula.AccuracyBase < 0
            || package.StatFormula.AttackSpeedBase <= 0)
        {
            errors.Add(new ContentValidationError(
                "INVALID_STAT_FORMULA",
                "statFormula",
                "Stat formula contains values outside its valid range."));
        }
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

    private static void ValidateMetadata(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(package.ContentVersion))
        {
            errors.Add(new ContentValidationError(
                "MISSING_CONTENT_VERSION",
                "contentVersion",
                "ContentVersion is required."));
        }

        if (string.IsNullOrWhiteSpace(package.BalanceVersion))
        {
            errors.Add(new ContentValidationError(
                "MISSING_BALANCE_VERSION",
                "balanceVersion",
                "BalanceVersion is required."));
        }

        if (package.PublishedAtUtc.Offset != TimeSpan.Zero)
        {
            errors.Add(new ContentValidationError(
                "PUBLISHED_AT_NOT_UTC",
                "publishedAtUtc",
                "PublishedAtUtc must use a zero UTC offset."));
        }
    }

    private static void ValidateReferences(
        GameContentPackage package,
        IReadOnlySet<ContentKey> definitions,
        List<ContentValidationError> errors)
    {
        for (var definitionIndex = 0; definitionIndex < package.Definitions.Count; definitionIndex++)
        {
            GameContentDefinition definition = package.Definitions[definitionIndex];

            for (var referenceIndex = 0; referenceIndex < definition.References.Count; referenceIndex++)
            {
                GameContentReference reference = definition.References[referenceIndex];
                string path = $"definitions[{definitionIndex}].references[{referenceIndex}]";

                bool typeIsValid = ValidateIdentifier(
                    reference.Type,
                    "INVALID_REFERENCE_TYPE",
                    $"{path}.type",
                    errors);
                bool idIsValid = ValidateIdentifier(
                    reference.Id,
                    "INVALID_REFERENCE_ID",
                    $"{path}.id",
                    errors);

                if (typeIsValid
                    && idIsValid
                    && !definitions.Contains(new ContentKey(reference.Type, reference.Id)))
                {
                    errors.Add(new ContentValidationError(
                        "MISSING_REFERENCE",
                        path,
                        $"Referenced definition '{reference.Type}:{reference.Id}' does not exist."));
                }
            }
        }
    }

    private static bool ValidateIdentifier(
        string value,
        string errorCode,
        string path,
        List<ContentValidationError> errors)
    {
        if (IsCanonicalIdentifier(value))
        {
            return true;
        }

        errors.Add(new ContentValidationError(
            errorCode,
            path,
            $"'{value}' must use uppercase ASCII letters, digits, and underscores, starting with a letter."));

        return false;
    }

    private static bool IsCanonicalIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || value[0] is < 'A' or > 'Z')
        {
            return false;
        }

        foreach (char character in value)
        {
            if (character is not (>= 'A' and <= 'Z')
                && character is not (>= '0' and <= '9')
                && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct ContentKey(string Type, string Id);
}
