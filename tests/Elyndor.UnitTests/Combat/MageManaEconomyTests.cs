using Elyndor.Core.Combat.Simulation;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Combat;

public sealed class MageManaEconomyTests
{
    [Theory]
    [InlineData(1, 45)]
    [InlineData(18, 90)]
    [InlineData(35, 120)]
    [InlineData(55, 150)]
    [InlineData(60, 180)]
    public async Task SustainedBuildsStayWithinTheirLevelBudget(int level, decimal targetSeconds)
    {
        var content = await GameContentPackageLoader.LoadAsync(ContentPath());
        var runner = new CombatSimulationRunner(content);
        foreach (string branch in new[] { "FIRE", "ARCANE", "FROST" })
        {
            var result = runner.RunMageManaEconomy(level, branch);
            Assert.NotNull(result.SecondsToOom);
            Assert.InRange(result.SecondsToOom.Value, targetSeconds * .8m, targetSeconds * 1.2m);
        }
    }

    [Fact]
    public async Task RetainingOldGearKeepsManaPoolStableAndGoodGearExtendsSustain()
    {
        var content = await GameContentPackageLoader.LoadAsync(ContentPath());
        var runner = new CombatSimulationRunner(content);
        var previous = runner.RunMageManaEconomy(55, "FIRE");
        var current = runner.RunMageManaEconomy(59, "FIRE");
        var normal = runner.RunMageManaEconomy(60, "FIRE");
        var good = runner.RunMageManaEconomy(60, "FIRE", CombatSimulationGearState.Good);
        Assert.True(current.MaxMana >= previous.MaxMana);
        Assert.True(good.MaxMana > normal.MaxMana);
        Assert.True(good.SecondsToOom > normal.SecondsToOom);
    }

    [Fact]
    public async Task BenchmarkIsDeterministicAndStopsAtTheFirstUnaffordableRotationSpell()
    {
        var content = await GameContentPackageLoader.LoadAsync(ContentPath());
        var runner = new CombatSimulationRunner(content);
        var first = runner.RunMageManaEconomy(1, "FIRE", CombatSimulationGearState.None);
        var second = runner.RunMageManaEconomy(1, "FIRE", CombatSimulationGearState.None);
        Assert.Equal(first.SecondsToOom, second.SecondsToOom);
        Assert.InRange(first.SecondsToOom!.Value, 10, 100);
        Assert.True(first.ResourceSpent > first.MaxMana);
        Assert.True(first.Casts > 0);
        Assert.True(first.RemainingMana < first.FailedSpellCost);
    }

    [Fact]
    public async Task BenchmarkReportsSustainableRotationWithoutInventingAnOomTime()
    {
        var content = await GameContentPackageLoader.LoadAsync(ContentPath());
        var resources = content.ResourceProfiles!.Select(p => p.Id == "MANA" ? p with { CombatRegenPerSecond = 1000 } : p).ToArray();
        var result = new CombatSimulationRunner(content with { ResourceProfiles = resources })
            .RunMageManaEconomy(1, "FIRE", CombatSimulationGearState.None, durationSeconds: 10);
        Assert.Null(result.SecondsToOom);
        Assert.True(result.Casts > 1);
    }

    private static string ContentPath()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "content", "package.json"))) root = root.Parent;
        return Path.Combine(root!.FullName, "content", "package.json");
    }
}
