using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
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

}
