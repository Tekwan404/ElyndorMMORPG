using Elyndor.ContentValidator;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Talents;

public sealed class TalentAuditReportTests
{
    [Fact]
    public async Task AuditRequiresEveryComposedTalentModifierToBeRuntimeSupported()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            RepositoryContentPath());

        TalentAuditReport report = TalentAuditReport.Create(package);

        Assert.Equal(3, report.TreeCount);
        Assert.Equal(288, report.NodeCount);
        Assert.Equal(321, report.ModifierCount);
        Assert.Equal(0, report.DeferredModifierCount);
        Assert.Equal(0, report.FullyDeferredNodeCount);
        Assert.True(
            report.RuntimeUnmappedModifierCount == 0,
            string.Join(
                ", ",
                report.Entries
                    .SelectMany(entry => entry.Modifiers)
                    .Where(modifier => !modifier.RuntimeMapped)
                    .Select(modifier => modifier.CoverageId)));
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

    [Fact]
    public async Task ComposedContentContainsEveryWarlordTalentGatedAbility()
    {
        var package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        string[] abilityIds =
        [
            "BATTLE_CRY", "ENDURANCE_CRY", "WAR_BANNER", "CRY_OF_VENGEANCE",
            "VICTORY_FLAG", "RALLY_CRY", "BATTLE_STANDARD"
        ];

        Assert.All(
            abilityIds,
                abilityId => Assert.Contains(
                    package.Abilities!,
                    ability => ability.Id == abilityId));
    }

    [Fact]
    public void AuditDoesNotTreatAnUnknownSupportedEventAsRuntimeImplemented()
    {
        TalentDefinition node = new(
            "UNKNOWN-1-1",
            "UNKNOWN_BRANCH",
            1,
            0,
            "Проверка",
            "Test",
            1,
            [],
            "Проверка runtime",
            Modifiers:
            [
                new(
                    TalentModifierType.EventTriggered,
                    TalentModifierKeys.OnPartyEvent,
                    [1],
                    RuntimeStatus: TalentModifierRuntimeStatus.Supported)
            ]);

        Assert.False(
            TalentRuntimeAvailability.IsModifierSupported(
                node,
                node.Modifiers![0]));
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
