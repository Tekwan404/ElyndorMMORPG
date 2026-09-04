namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    private static readonly HashSet<string> AllowedDangerLevels =
        ["SAFE", "ADVENTURE", "DANGEROUS"];

    public static IReadOnlyList<ContentValidationError> Validate(GameContentPackage package) =>
        ContentValidationPipeline.Default.Validate(package);

    internal static HashSet<ContentKey> ValidateDefinitions(
        GameContentPackage package,
        List<ContentValidationError> errors)
    {
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

            if (typeIsValid
                && idIsValid
                && !definitions.Add(new ContentKey(definition.Type, definition.Id)))
            {
                errors.Add(new ContentValidationError(
                    "DUPLICATE_DEFINITION_ID",
                    path,
                    $"Definition '{definition.Type}:{definition.Id}' is duplicated."));
            }
        }

        return definitions;
    }

    internal readonly record struct ContentKey(string Type, string Id);
}
