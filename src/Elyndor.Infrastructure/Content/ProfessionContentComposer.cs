using Elyndor.Core.Content;
using Elyndor.Core.Professions;

namespace Elyndor.Infrastructure.Content;

internal static class ProfessionContentComposer
{
    internal static async Task<GameContentPackage> ComposeAsync(
        string packagePath,
        GameContentPackage package,
        CancellationToken cancellationToken)
    {
        string? contentDirectory = Path.GetDirectoryName(packagePath);
        if (string.IsNullOrWhiteSpace(contentDirectory))
            return package;

        string directory = Path.Combine(contentDirectory, "professions");
        if (!Directory.Exists(directory))
            return package;

        foreach (string path in Directory
                     .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(item => item, StringComparer.Ordinal))
        {
            ProfessionContentFragment fragment =
                await GameContentJson.ReadRequiredAsync<ProfessionContentFragment>(path, cancellationToken);

            package = package with
            {
                ContentVersion = fragment.ContentVersion is null
                    ? package.ContentVersion
                    : ContentCompositionRules.HigherVersion(package.ContentVersion, fragment.ContentVersion),
                BalanceVersion = fragment.BalanceVersion is null
                    ? package.BalanceVersion
                    : ContentCompositionRules.HigherVersion(package.BalanceVersion, fragment.BalanceVersion),
                PublishedAtUtc = fragment.PublishedAtUtc is null
                    ? package.PublishedAtUtc
                    : ContentCompositionRules.Later(package.PublishedAtUtc, fragment.PublishedAtUtc.Value),
                Professions = ContentCompositionRules.MergeOptionalByKey(
                    package.Professions,
                    fragment.Professions,
                    item => item.Id),
                SkinningSources = ContentCompositionRules.MergeOptionalByKey(
                    package.SkinningSources,
                    fragment.SkinningSources,
                    item => item.Id),
                ProfessionRecipes = ContentCompositionRules.MergeOptionalByKey(
                    package.ProfessionRecipes,
                    fragment.ProfessionRecipes,
                    item => item.Id)
            };
        }

        return package;
    }

    private sealed record ProfessionContentFragment(
        string? ContentVersion = null,
        string? BalanceVersion = null,
        DateTimeOffset? PublishedAtUtc = null,
        IReadOnlyList<ProfessionDefinition>? Professions = null,
        IReadOnlyList<SkinningSourceDefinition>? SkinningSources = null,
        IReadOnlyList<ProfessionRecipeDefinition>? ProfessionRecipes = null);
}
