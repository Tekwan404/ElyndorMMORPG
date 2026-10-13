using Elyndor.Core.Combat.Simulation;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Combat;

public sealed class MageManaEconomyTests
{
    [Theory]
    [InlineData(1, "FIRE", 46)]
    [InlineData(1, "ARCANE", 10.5)]
    [InlineData(1, "FROST", 45)]
    [InlineData(18, "FIRE", 72)]
    [InlineData(18, "ARCANE", 23.5)]
    [InlineData(18, "FROST", 72)]
    [InlineData(35, "FIRE", 99)]
    [InlineData(35, "ARCANE", 124)]
    [InlineData(35, "FROST", 103)]
    [InlineData(55, "FIRE", 97)]
    [InlineData(55, "ARCANE", 156)]
    [InlineData(55, "FROST", 112)]
    [InlineData(60, "FIRE", 125)]
    [InlineData(60, "ARCANE", 188)]
    [InlineData(60, "FROST", 136)]
    public async Task ContinuousRotationWithHalfSecondGlobalCooldownStaysWithinItsBranchBudget(int level, string branch, decimal targetSeconds)
    {
        var content = await GameContentPackageLoader.LoadAsync(ContentPath());
        var result = new CombatSimulationRunner(content).RunMageManaEconomy(level, branch);
        Assert.NotNull(result.SecondsToOom);
        Assert.InRange(result.SecondsToOom.Value, targetSeconds * .8m, targetSeconds * 1.2m);
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
