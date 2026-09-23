using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Characters;

namespace Elyndor.Infrastructure.Content;

internal static class PremiumStoreContentComposer
{
    internal static async Task<GameContentPackage> ComposeAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string? contentDirectory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(contentDirectory))
            return package;

        string directory = Path.Combine(contentDirectory, "economy");
        if (!Directory.Exists(directory))
            return package;

        foreach (string path in Directory
                     .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(item => item, StringComparer.Ordinal))
        {
            PremiumStoreContentFragment fragment =
                await GameContentJson.ReadRequiredAsync<PremiumStoreContentFragment>(
                    path,
                    cancellationToken);

            package = package with
            {
                CharacterSkins = ContentCompositionRules.MergeOptionalByKey(
                    package.CharacterSkins,
                    fragment.CharacterSkins,
                    skin => skin.Id),
                PremiumStoreOffers = ContentCompositionRules.MergeOptionalByKey(
                    package.PremiumStoreOffers,
                    fragment.PremiumStoreOffers,
                    offer => offer.Sku)
            };
        }

        return package;
    }

    private sealed record PremiumStoreContentFragment(
        IReadOnlyList<PremiumStoreOfferDefinition>? PremiumStoreOffers = null,
        IReadOnlyList<CharacterSkinDefinition>? CharacterSkins = null);
}
