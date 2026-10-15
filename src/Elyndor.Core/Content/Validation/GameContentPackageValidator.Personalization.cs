namespace Elyndor.Core.Content;

public static partial class GameContentPackageValidator
{
    internal static void ValidatePersonalization(GameContentPackage package, List<ContentValidationError> errors)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (var cosmetic in package.ProfileCosmetics ?? [])
        {
            if (!IsCanonicalIdentifier(cosmetic.Id) || cosmetic.Id.Length > 64 || !ids.Add(cosmetic.Id)
                || string.IsNullOrWhiteSpace(cosmetic.Name) || cosmetic.Name.Length > 40
                || string.IsNullOrWhiteSpace(cosmetic.Description)
                || cosmetic.CrystalPrice is <= 0 or > 1000000
                || cosmetic.Kind is not ("Frame" or "Title")
                || cosmetic.StyleId is not ("ash" or "ether" or "iron"))
                errors.Add(new("INVALID_PROFILE_COSMETIC", "profileCosmetics", "Invalid cosmetic identity, slot, style or price."));
        }
        if (package.CharacterRename is { CrystalPrice: <= 0 or > 1000000 })
            errors.Add(new("INVALID_RENAME_OFFER", "characterRename", "Rename price must be positive and within supported range."));
    }
}
