using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
        internal static void ValidateReferences(
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

}
