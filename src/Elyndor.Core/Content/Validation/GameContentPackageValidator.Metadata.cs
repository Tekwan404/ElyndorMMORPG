using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Items;

namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
        internal static void ValidateMetadata(
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

}
