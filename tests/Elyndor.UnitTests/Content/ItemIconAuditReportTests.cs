using Elyndor.ContentValidator;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Content;

public sealed class ItemIconAuditReportTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"elyndor-item-icons-{Guid.NewGuid():N}");

    public ItemIconAuditReportTests()
    {
        Directory.CreateDirectory(root);
    }

    [Fact]
    public void Create_AcceptsCanonicalNestedPathAndMissingIconId()
    {
        WriteAsset("sets/guardian/helmet.webp");

        ItemIconAuditReport report = ItemIconAuditReport.Create(
            [Item("HELMET", "sets/guardian/helmet"), Item("NO_ART", null)],
            root);

        Assert.True(report.IsValid);
        Assert.Equal(2, report.TotalItems);
        Assert.Equal(1, report.WithIconId);
        Assert.Equal(1, report.WithoutIconId);
        Assert.Equal(1, report.Valid);
        Assert.Equal(0, report.Broken);
    }

    [Fact]
    public void Create_ClassifiesCaseLegacyMissingUnsupportedAndInvalidPaths()
    {
        WriteAsset("sets/guardian/Helmet.webp");
        WriteAsset("sets/mage/staff.webp");
        WriteAsset("bad-format.gif");

        ItemIconAuditReport report = ItemIconAuditReport.Create(
            [
                Item("CASE", "sets/guardian/helmet"),
                Item("LEGACY", "staff"),
                Item("MISSING", "does-not-exist"),
                Item("FORMAT", "bad-format"),
                Item("ESCAPE", "../outside")
            ],
            root);

        Assert.False(report.IsValid);
        Assert.Equal(1, report.CaseMismatch);
        Assert.Equal(1, report.LegacyOnlyMappings);
        Assert.Equal(1, report.Missing);
        Assert.Equal(1, report.UnsupportedFormat);
        Assert.Equal(1, report.InvalidPath);
        Assert.Equal("sets/guardian/Helmet", report.Entries.Single(entry => entry.ItemId == "CASE").CanonicalIconId);
        Assert.Equal("sets/mage/staff", report.Entries.Single(entry => entry.ItemId == "LEGACY").CanonicalIconId);
    }

    [Fact]
    public void Create_ReportsAmbiguousLegacyBasename()
    {
        WriteAsset("sets/guardian/ring.webp");
        WriteAsset("sets/mage/ring.png");

        ItemIconAuditReport report = ItemIconAuditReport.Create([Item("RING", "ring")], root);

        Assert.False(report.IsValid);
        Assert.Equal(1, report.Ambiguous);
        Assert.Equal(ItemIconAuditStatus.Ambiguous, report.Entries[0].Status);
    }

    private void WriteAsset(string relativePath)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, []);
    }

    private static ItemDefinition Item(string id, string? iconId) => new(
        Id: id,
        Name: id,
        Type: ItemType.Material,
        Rarity: ItemRarity.Common,
        RequiredLevel: 1,
        Stackable: true,
        MaxStack: 99,
        Slot: null,
        Stats: new PrimaryStats(0, 0, 0, 0),
        Description: id,
        IconId: iconId);

    public void Dispose()
    {
        Directory.Delete(root, recursive: true);
    }
}
