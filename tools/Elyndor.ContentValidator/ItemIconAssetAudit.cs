using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.ContentValidator;

public enum ItemIconIssueSeverity
{
    Warning,
    Error
}

public sealed record ItemIconAsset(string Id, string Extension);

public sealed record ItemIconAuditIssue(
    ItemIconIssueSeverity Severity,
    string Code,
    string ItemId,
    string? IconId,
    string Message);

public sealed record ItemIconAuditReport(
    int TotalItemDefinitions,
    int WithIconId,
    int WithoutIconId,
    int Valid,
    int MissingAsset,
    int CaseMismatch,
    int InvalidPath,
    int UnsupportedFormat,
    int Ambiguous,
    int LegacyOnly,
    int UnusedAssets,
    IReadOnlyList<string> UnusedAssetIds,
    IReadOnlyList<ItemIconAuditIssue> Issues)
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".webp", ".png", ".jpg", ".jpeg", ".svg"
        };

    public static ItemIconAuditReport Create(
        GameContentPackage package,
        IEnumerable<ItemIconAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(assets);

        ItemIconAsset[] assetList = assets.ToArray();
        Dictionary<string, ItemIconAsset> supportedById = assetList
            .Where(asset => SupportedExtensions.Contains(asset.Extension))
            .GroupBy(asset => asset.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        Dictionary<string, ItemIconAsset[]> supportedByIgnoreCase = assetList
            .Where(asset => SupportedExtensions.Contains(asset.Extension))
            .GroupBy(asset => asset.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, ItemIconAsset[]> allByBasename = assetList
            .GroupBy(asset => Path.GetFileName(asset.Id), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        List<ItemIconAuditIssue> issues = [];
        HashSet<string> referencedAssetIds = new(StringComparer.Ordinal);
        HashSet<string> reportedUnsupportedAssetIds = new(StringComparer.Ordinal);
        int withIconId = 0;
        int withoutIconId = 0;
        int valid = 0;
        int missingAsset = 0;
        int caseMismatch = 0;
        int invalidPath = 0;
        int unsupportedFormat = 0;
        int ambiguous = 0;
        int legacyOnly = 0;

        IReadOnlyList<ItemDefinition> items = package.Items ?? [];
        foreach (ItemDefinition item in items.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            string? iconId = item.IconId;
            if (string.IsNullOrWhiteSpace(iconId))
            {
                withoutIconId++;
                continue;
            }

            withIconId++;
            if (supportedById.ContainsKey(iconId))
            {
                valid++;
                referencedAssetIds.Add(iconId);
                continue;
            }

            if (supportedByIgnoreCase.TryGetValue(iconId, out ItemIconAsset[]? casingCandidates))
            {
                caseMismatch++;
                issues.Add(Error(
                    "ITEM_ICON_CASE_MISMATCH",
                    item,
                    $"Asset exists as '{casingCandidates[0].Id}', but Linux paths are case-sensitive."));
                continue;
            }

            if (!ItemIconId.IsCanonical(iconId))
            {
                invalidPath++;
                issues.Add(Error(
                    "ITEM_ICON_INVALID_PATH",
                    item,
                    "IconId must be a lowercase, extensionless path relative to the item asset root."));
                continue;
            }

            ItemIconAsset[] basenameCandidates = allByBasename.TryGetValue(iconId, out ItemIconAsset[]? candidates)
                ? candidates
                : [];
            if (basenameCandidates.Length > 1)
            {
                ambiguous++;
                issues.Add(Error(
                    "ITEM_ICON_AMBIGUOUS",
                    item,
                    $"Legacy basename matches multiple assets: {string.Join(", ", basenameCandidates.Select(asset => asset.Id))}."));
                continue;
            }

            if (basenameCandidates.Length == 1)
            {
                legacyOnly++;
                missingAsset++;
                issues.Add(Error(
                    "ITEM_ICON_ASSET_NOT_FOUND",
                    item,
                    $"Canonical asset '{iconId}' is missing; legacy basename would resolve to '{basenameCandidates[0].Id}'."));
                continue;
            }

            ItemIconAsset[] unsupportedCandidates = assetList
                .Where(asset => string.Equals(asset.Id, iconId, StringComparison.Ordinal))
                .ToArray();
            if (unsupportedCandidates.Length > 0)
            {
                unsupportedFormat++;
                reportedUnsupportedAssetIds.Add(unsupportedCandidates[0].Id);
                issues.Add(Error(
                    "ITEM_ICON_UNSUPPORTED_FORMAT",
                    item,
                    $"Asset uses unsupported extension '{unsupportedCandidates[0].Extension}'."));
                continue;
            }

            missingAsset++;
            issues.Add(new(
                ItemIconIssueSeverity.Warning,
                "ITEM_ICON_ASSET_NOT_FOUND",
                item.Id,
                iconId,
                "No asset exists. Item icons are optional in the current content policy, so runtime fallback remains available."));
        }

        foreach (ItemIconAsset asset in assetList.Where(asset =>
                     !SupportedExtensions.Contains(asset.Extension)
                     && !reportedUnsupportedAssetIds.Contains(asset.Id)))
        {
            unsupportedFormat++;
            issues.Add(new(
                ItemIconIssueSeverity.Error,
                "ITEM_ICON_UNSUPPORTED_FORMAT",
                $"ASSET:{asset.Id}",
                asset.Id,
                $"Item asset uses unsupported extension '{asset.Extension}'."));
        }

        string[] unusedAssetIds = supportedById.Keys
            .Where(assetId => !referencedAssetIds.Contains(assetId))
            .OrderBy(assetId => assetId, StringComparer.Ordinal)
            .ToArray();
        return new ItemIconAuditReport(
            items.Count,
            withIconId,
            withoutIconId,
            valid,
            missingAsset,
            caseMismatch,
            invalidPath,
            unsupportedFormat,
            ambiguous,
            legacyOnly,
            unusedAssetIds.Length,
            unusedAssetIds,
            issues);
    }

    public static IReadOnlyList<ItemIconAsset> ReadAssets(string assetRoot)
    {
        if (!Directory.Exists(assetRoot))
            throw new DirectoryNotFoundException($"Item asset root '{assetRoot}' does not exist.");

        return Directory.EnumerateFiles(assetRoot, "*", SearchOption.AllDirectories)
            .Select(path => new ItemIconAsset(
                Path.GetRelativePath(assetRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
                Path.GetExtension(path)))
            .Select(asset => asset with
            {
                Id = asset.Id[..^asset.Extension.Length]
            })
            .OrderBy(asset => asset.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static ItemIconAuditIssue Error(
        string code,
        ItemDefinition item,
        string message) =>
        new(ItemIconIssueSeverity.Error, code, item.Id, item.IconId, message);
}
