using Elyndor.ContentValidator;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Talents;

public sealed class TalentAuditReportTests
{
    [Fact]
    public async Task AuditCountsAllComposedTalentTreesAndDeferredModifiers()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            RepositoryContentPath());

        TalentAuditReport report = TalentAuditReport.Create(package);

        Assert.Equal(3, report.TreeCount);
        Assert.Equal(288, report.NodeCount);
        Assert.Equal(321, report.ModifierCount);
        Assert.Equal(230, report.DeferredModifierCount);
        Assert.Equal(209, report.FullyDeferredNodeCount);
        Assert.True(report.RuntimeUnmappedModifierCount > 0);
        Assert.Equal(
            report.RuntimeUnmappedModifierCount,
            report.Entries
                .SelectMany(entry => entry.Modifiers)
                .Count(modifier => !modifier.RuntimeMapped));
    }

    [Fact]
    public async Task AuditProducesUniqueCoverageIdentityForEveryModifier()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            RepositoryContentPath());

        TalentAuditReport report = TalentAuditReport.Create(package);
        string[] coverageIds = report.Entries
            .SelectMany(entry => entry.Modifiers)
            .Select(modifier => modifier.CoverageId)
            .ToArray();

        Assert.Equal(report.ModifierCount, coverageIds.Length);
        Assert.Equal(coverageIds.Length, coverageIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(coverageIds, id => Assert.Contains(":", id, StringComparison.Ordinal));
    }

    private static string RepositoryContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
