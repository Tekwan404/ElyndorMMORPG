namespace Elyndor.Core.Content;

public static class GameContentPackageValidator
{
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

        return errors;
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
