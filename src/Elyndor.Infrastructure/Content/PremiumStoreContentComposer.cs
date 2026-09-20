using Elyndor.Core.Content;
using Elyndor.Core.Items;

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
                PremiumStoreOffers = ContentCompositionRules.MergeOptionalByKey(
                    package.PremiumStoreOffers,
                    fragment.PremiumStoreOffers,
                    offer => offer.Sku)
            };
        }

        return package;
    }

    private sealed record PremiumStoreContentFragment(
        IReadOnlyList<PremiumStoreOfferDefinition>? PremiumStoreOffers = null);
}
