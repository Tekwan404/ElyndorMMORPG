using Elyndor.ContentValidator;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class ItemIconValidationTests
{
    [Theory]
    [InlineData("../sword")]
    [InlineData("sets//sword")]
    [InlineData("sets/Sword")]
    [InlineData("sets/sword.webp")]
    [InlineData("/assets/items/sword")]
    [InlineData("sets\\sword")]
    [InlineData("sets/warrior sword")]
    [InlineData("https://cdn.example/items/sword")]
    public void ContentValidationRejectsNonCanonicalIconIds(string iconId)
    {
        GameContentPackage package = PackageWith(iconId);

        IReadOnlyList<ContentValidationError> errors =
            ContentValidationPipeline.Default.Validate(package);

        Assert.Contains(errors, error => error.Code == "ITEM_ICON_INVALID_PATH");
    }

    [Fact]
    public void AssetAuditReportsMissingCaseTraversalUnsupportedAndAmbiguousReferences()
    {
        GameContentPackage package = PackageWithItems(
            Item("VALID", "sets/sword"),
            Item("CASE", "sets/Sword"),
            Item("TRAVERSAL", "../sword"),
            Item("UNSUPPORTED", "sets/axe"),
            Item("AMBIGUOUS", "shield"),
            Item("MISSING", "sets/missing"));
        ItemIconAsset[] assets =
        [
            new("sets/sword", ".webp"),
            new("sets/axe", ".bmp"),
            new("sets/shield", ".webp"),
            new("imported/shield", ".png")
        ];

        ItemIconAuditReport report = ItemIconAuditReport.Create(package, assets);

        Assert.Equal(1, report.Valid);
        Assert.Equal(1, report.CaseMismatch);
        Assert.Equal(1, report.InvalidPath);
        Assert.Equal(1, report.UnsupportedFormat);
        Assert.Equal(1, report.Ambiguous);
        Assert.Equal(1, report.MissingAsset);
        Assert.Contains(report.Issues, issue => issue.Code == "ITEM_ICON_CASE_MISMATCH");
        Assert.Contains(report.Issues, issue => issue.Code == "ITEM_ICON_INVALID_PATH");
        Assert.Contains(report.Issues, issue => issue.Code == "ITEM_ICON_UNSUPPORTED_FORMAT");
        Assert.Contains(report.Issues, issue => issue.Code == "ITEM_ICON_AMBIGUOUS");
        Assert.Contains(report.Issues, issue => issue.Code == "ITEM_ICON_ASSET_NOT_FOUND");
    }

    [Fact]
    public async Task ComposedRepositoryContentUsesNoLegacyOrUnsafeItemIconIds()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        ItemIconAuditReport report = ItemIconAuditReport.Create(
            package,
            ItemIconAuditReport.ReadAssets(RepositoryItemAssetPath()));

        Assert.Equal(0, report.LegacyOnly);
        Assert.Equal(0, report.CaseMismatch);
        Assert.Equal(0, report.InvalidPath);
        Assert.Equal(0, report.UnsupportedFormat);
        Assert.Equal(0, report.Ambiguous);
        Assert.DoesNotContain(report.Issues, issue => issue.Severity == ItemIconIssueSeverity.Error);
    }

    private static GameContentPackage PackageWith(string iconId) =>
        PackageWithItems(Item("TEST_ITEM", iconId));

    private static GameContentPackage PackageWithItems(params ItemDefinition[] items) =>
        new("0.1.0", "0.1.0", DateTimeOffset.UnixEpoch, [], [], Items: items);

    private static ItemDefinition Item(string id, string iconId) =>
        new(
            id,
            id,
            ItemType.Material,
            ItemRarity.Common,
            1,
            true,
            99,
            null,
            new PrimaryStats(0, 0, 0, 0),
            id,
            IconId: iconId);

    private static string RepositoryContentPath() =>
        FindRepositoryPath("content", "package.json");

    private static string RepositoryItemAssetPath() =>
        FindRepositoryPath("web", "elyndor-web", "src", "assets", "items");

    private static string FindRepositoryPath(params string[] relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. relativePath]);
            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Repository path '{Path.Combine(relativePath)}' was not found.");
    }
}
