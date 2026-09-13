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
        Assert.Equal(317, report.ModifierCount);
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
    public async Task ComposedContentContainsExactGuardianClassicV2Tree()
    {
        var package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        TalentTreeDefinition warrior = Assert.Single(
            package.TalentTrees!,
            tree => tree.Id == "WARRIOR_TREE");
        TalentBranchDefinition guardian = Assert.Single(
            warrior.Branches,
            branch => branch.Id == "GUARDIAN");
        TalentDefinition[] nodes = warrior.Nodes
            .Where(node => node.BranchId == "GUARDIAN")
            .OrderBy(node => node.Tier)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
        string[] expectedIds =
        [
            "G-1-1", "G-1-2", "G-1-3", "G-1-4", "G-1-5",
            "G-2-1", "G-2-2", "G-2-3", "G-2-4", "G-2-5",
            "G-3-1", "G-3-2", "G-3-3", "G-3-4", "G-3-5", "G-3-6",
            "G-4-1", "G-4-2", "G-4-3", "G-4-4", "G-4-5",
            "G-5-1", "G-5-2", "G-5-3", "G-5-4", "G-5-5",
            "G-6-1", "G-6-2", "G-6-3", "G-6-4", "G-6-5"
        ];

        Assert.Equal(31, guardian.NodeCount);
        Assert.Equal(31, nodes.Length);
        Assert.Equal(expectedIds, nodes.Select(node => node.Id));
        Assert.DoesNotContain(nodes, node => node.Id.StartsWith("G-7-", StringComparison.Ordinal)
            || node.Id.StartsWith("G-8-", StringComparison.Ordinal)
            || node.Id.StartsWith("G-9-", StringComparison.Ordinal));
        Assert.DoesNotContain(nodes.SelectMany(node => node.Modifiers ?? []), modifier =>
            modifier.Key is TalentModifierKeys.DodgePercent or TalentModifierKeys.OnDodge);

        Assert.All(nodes.Where(node => node.Tier == 1), node => Assert.Equal(0, node.RequiredSpentPoints));
        Assert.All(nodes.Where(node => node.Tier == 2), node => Assert.Equal(5, node.RequiredSpentPoints));
        Assert.All(nodes.Where(node => node.Tier == 3), node => Assert.Equal(10, node.RequiredSpentPoints));
        Assert.All(nodes.Where(node => node.Tier == 4), node => Assert.Equal(15, node.RequiredSpentPoints));
        Assert.All(nodes.Where(node => node.Tier == 5), node => Assert.Equal(20, node.RequiredSpentPoints));
        Assert.All(nodes.Where(node => node.Tier == 6), node => Assert.Equal(25, node.RequiredSpentPoints));
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
    public async Task ComposedContentContainsEveryGuardianTalentGatedAbility()
    {
        var package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        string[] abilityIds =
        [
            "LAST_STAND", "REVENGE", "SHIELD_BLOCK", "PROVOKE", "SUNDER_ARMOR",
            "CONCUSSION_BLOW", "BASTION", "CHALLENGING_SHOUT", "SHIELD_SLAM"
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
