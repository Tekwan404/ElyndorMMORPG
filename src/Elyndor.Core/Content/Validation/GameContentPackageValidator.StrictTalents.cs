using Elyndor.Core.Talents;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    public static IReadOnlyList<ContentValidationError> ValidateStrictTalents(
        GameContentPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        List<ContentValidationError> errors = Validate(package).ToList();
        foreach (TalentTreeDefinition tree in package.TalentTrees ?? [])
        {
            for (var nodeIndex = 0; nodeIndex < tree.Nodes.Count; nodeIndex++)
            {
                TalentDefinition node = tree.Nodes[nodeIndex];
                string path = $"talentTrees[{tree.Id}].nodes[{nodeIndex}]";

                if (!ContainsRussian(node.Name) || !ContainsRussian(node.Description))
                {
                    errors.Add(new(
                        "TALENT_MISSING_RUSSIAN_TEXT",
                        path,
                        $"Talent '{node.Id}' must have Russian name and description text."));
                }

                IReadOnlyList<TalentModifierDefinition> modifiers = node.Modifiers ?? [];
                for (var modifierIndex = 0; modifierIndex < modifiers.Count; modifierIndex++)
                {
                    TalentModifierDefinition modifier = modifiers[modifierIndex];
                    string modifierPath = $"{path}.modifiers[{modifierIndex}]";

                    if (modifier.RuntimeStatus == TalentModifierRuntimeStatus.Deferred)
                    {
                        errors.Add(new(
                            "TALENT_RUNTIME_DEFERRED",
                            modifierPath,
                            $"Talent '{node.Id}' contains a deferred runtime modifier."));
                    }

                    if (modifier.Values.Count != node.MaxRank
                        || modifier.SecondaryValues is { } secondary
                            && secondary.Count != node.MaxRank)
                    {
                        errors.Add(new(
                            "TALENT_RANK_VALUE_MISMATCH",
                            modifierPath,
                            $"Talent '{node.Id}' must define one primary and secondary value per rank."));
                    }

                    if (modifier.Values.Count > 0 && modifier.Values.All(value => value == 0)
                        || modifier.SecondaryValues is { Count: > 0 }
                            && modifier.SecondaryValues.All(value => value == 0))
                    {
                        errors.Add(new(
                            "TALENT_ZERO_VALUE_MODIFIER",
                            modifierPath,
                            $"Talent '{node.Id}' contains a zero-value gameplay modifier."));
                    }

                }
            }
        }

        return errors;
    }

    private static bool ContainsRussian(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Any(character => character is >= '\u0400' and <= '\u04FF');
}
