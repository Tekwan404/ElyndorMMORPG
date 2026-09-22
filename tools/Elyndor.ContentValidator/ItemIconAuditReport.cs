using Elyndor.Core.Items;

namespace Elyndor.ContentValidator;

public enum ItemIconAuditStatus
{
    NoIcon,
    Valid,
    Missing,
    CaseMismatch,
    LegacyOnly,
    InvalidPath,
    UnsupportedFormat,
    Ambiguous
}

public sealed record ItemIconAuditEntry(
    string ItemId,
    string? IconId,
    ItemIconAuditStatus Status,
    string? CanonicalIconId = null,
    string? Message = null)
{
    public bool IsBroken => Status is not ItemIconAuditStatus.NoIcon and not ItemIconAuditStatus.Valid;
}

public sealed record ItemIconAuditReport(
    int TotalItems,
    int WithIconId,
    int WithoutIconId,
    int Valid,
    int Missing,
    int CaseMismatch,
    int LegacyOnlyMappings,
    int InvalidPath,
    int UnsupportedFormat,
    int Ambiguous,
    IReadOnlyList<ItemIconAuditEntry> Entries)
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".svg"
    };

    private static readonly HashSet<string> ImageLikeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".svg",
        ".gif",
        ".bmp",
        ".tif",
        ".tiff",
        ".ico",
        ".avif"
    };

    public int Broken => Entries.Count(entry => entry.IsBroken);
    public bool IsValid => Broken == 0;

    public static ItemIconAuditReport Create(
        IEnumerable<ItemDefinition> items,
        string assetRoot)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);

        string normalizedRoot = Path.GetFullPath(assetRoot);
        if (!Directory.Exists(normalizedRoot))
            throw new DirectoryNotFoundException($"Item icon asset root not found: {normalizedRoot}");

        AssetIndex assets = BuildAssetIndex(normalizedRoot);
        ItemIconAuditEntry[] entries = items
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => Audit(item, normalizedRoot, assets))
            .ToArray();

        return new ItemIconAuditReport(
            TotalItems: entries.Length,
            WithIconId: entries.Count(entry => entry.Status != ItemIconAuditStatus.NoIcon),
            WithoutIconId: entries.Count(entry => entry.Status == ItemIconAuditStatus.NoIcon),
            Valid: entries.Count(entry => entry.Status == ItemIconAuditStatus.Valid),
            Missing: entries.Count(entry => entry.Status == ItemIconAuditStatus.Missing),
            CaseMismatch: entries.Count(entry => entry.Status == ItemIconAuditStatus.CaseMismatch),
            LegacyOnlyMappings: entries.Count(entry => entry.Status == ItemIconAuditStatus.LegacyOnly),
            InvalidPath: entries.Count(entry => entry.Status == ItemIconAuditStatus.InvalidPath),
            UnsupportedFormat: entries.Count(entry => entry.Status == ItemIconAuditStatus.UnsupportedFormat),
            Ambiguous: entries.Count(entry => entry.Status == ItemIconAuditStatus.Ambiguous),
            Entries: entries);
    }

    private static ItemIconAuditEntry Audit(
        ItemDefinition item,
        string assetRoot,
        AssetIndex assets)
    {
        string? iconId = item.IconId?.Trim();
        if (string.IsNullOrWhiteSpace(iconId))
            return new ItemIconAuditEntry(item.Id, item.IconId, ItemIconAuditStatus.NoIcon);

        if (!TryValidateIconId(iconId, assetRoot, out string? pathError))
        {
            return new ItemIconAuditEntry(
                item.Id,
                iconId,
                ItemIconAuditStatus.InvalidPath,
                Message: pathError);
        }

        if (assets.AllowedExact.TryGetValue(iconId, out string[]? exactMatches))
        {
            return exactMatches.Length == 1
                ? new ItemIconAuditEntry(item.Id, iconId, ItemIconAuditStatus.Valid, iconId)
                : new ItemIconAuditEntry(
                    item.Id,
                    iconId,
                    ItemIconAuditStatus.Ambiguous,
                    Message: $"Multiple allowed assets resolve to '{iconId}': {string.Join(", ", exactMatches)}");
        }

        if (assets.AllowedIgnoreCase.TryGetValue(iconId, out string[]? caseMatches))
        {
            string[] canonicalIds = caseMatches
                .Select(AssetIdFromRelativePath)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (canonicalIds.Length == 1)
            {
                return new ItemIconAuditEntry(
                    item.Id,
                    iconId,
                    ItemIconAuditStatus.CaseMismatch,
                    canonicalIds[0],
                    $"IconId casing differs from the Linux filesystem. Use '{canonicalIds[0]}'.");
            }

            return new ItemIconAuditEntry(
                item.Id,
                iconId,
                ItemIconAuditStatus.Ambiguous,
                Message: $"Case-insensitive IconId '{iconId}' matches multiple assets: {string.Join(", ", caseMatches)}");
        }

        if (assets.UnsupportedExact.TryGetValue(iconId, out string[]? unsupportedMatches))
        {
            return new ItemIconAuditEntry(
                item.Id,
                iconId,
                ItemIconAuditStatus.UnsupportedFormat,
                Message: $"Only png, jpg, jpeg, webp and svg are allowed. Found: {string.Join(", ", unsupportedMatches)}");
        }

        if (!iconId.Contains('/')
            && assets.AllowedByBaseName.TryGetValue(iconId, out string[]? legacyMatches))
        {
            string[] canonicalIds = legacyMatches
                .Select(AssetIdFromRelativePath)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (canonicalIds.Length == 1)
            {
                return new ItemIconAuditEntry(
                    item.Id,
                    iconId,
                    ItemIconAuditStatus.LegacyOnly,
                    canonicalIds[0],
                    $"Legacy basename-only IconId must be migrated to '{canonicalIds[0]}'.");
            }

            return new ItemIconAuditEntry(
                item.Id,
                iconId,
                ItemIconAuditStatus.Ambiguous,
                Message: $"Legacy basename-only IconId '{iconId}' matches multiple assets: {string.Join(", ", legacyMatches)}");
        }

        return new ItemIconAuditEntry(
            item.Id,
            iconId,
            ItemIconAuditStatus.Missing,
            Message: $"No item icon asset exists for canonical IconId '{iconId}'.");
    }

    private static bool TryValidateIconId(string iconId, string assetRoot, out string? error)
    {
        if (Path.IsPathRooted(iconId) || iconId.StartsWith('/', StringComparison.Ordinal))
        {
            error = "IconId must be relative to the item asset root.";
            return false;
        }

        if (iconId.Contains('\\') || iconId.Contains('\0'))
        {
            error = "IconId must use forward slashes and contain no null characters.";
            return false;
        }

        string[] segments = iconId.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            error = "IconId contains an empty, current-directory, or parent-directory segment.";
            return false;
        }

        string extension = Path.GetExtension(iconId);
        if (ImageLikeExtensions.Contains(extension))
        {
            error = "IconId is an extensionless asset-root-relative identifier; do not include a file extension.";
            return false;
        }

        string rootWithSeparator = assetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(Path.Combine(
            assetRoot,
            iconId.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            error = "IconId resolves outside the item asset root.";
            return false;
        }

        error = null;
        return true;
    }

    private static AssetIndex BuildAssetIndex(string assetRoot)
    {
        string[] files = Directory.EnumerateFiles(assetRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(assetRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        string[] allowedFiles = files
            .Where(path => AllowedExtensions.Contains(Path.GetExtension(path)))
            .ToArray();
        string[] unsupportedImageFiles = files
            .Where(path => !AllowedExtensions.Contains(Path.GetExtension(path))
                && ImageLikeExtensions.Contains(Path.GetExtension(path)))
            .ToArray();

        return new AssetIndex(
            AllowedExact: GroupById(allowedFiles, StringComparer.Ordinal),
            AllowedIgnoreCase: GroupById(allowedFiles, StringComparer.OrdinalIgnoreCase),
            AllowedByBaseName: allowedFiles
                .GroupBy(
                    path => Path.GetFileNameWithoutExtension(path),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToArray(),
                    StringComparer.OrdinalIgnoreCase),
            UnsupportedExact: GroupById(unsupportedImageFiles, StringComparer.Ordinal));
    }

    private static Dictionary<string, string[]> GroupById(
        IEnumerable<string> paths,
        IEqualityComparer<string> comparer) =>
        paths
            .GroupBy(AssetIdFromRelativePath, comparer)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                comparer);

    private static string AssetIdFromRelativePath(string relativePath)
    {
        string extension = Path.GetExtension(relativePath);
        return extension.Length == 0
            ? relativePath
            : relativePath[..^extension.Length];
    }

    private sealed record AssetIndex(
        IReadOnlyDictionary<string, string[]> AllowedExact,
        IReadOnlyDictionary<string, string[]> AllowedIgnoreCase,
        IReadOnlyDictionary<string, string[]> AllowedByBaseName,
        IReadOnlyDictionary<string, string[]> UnsupportedExact);
}
