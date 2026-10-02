using Elyndor.Core.WorldBosses;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class WorldBossContentTests
{
    [Fact]
    public async Task AshArchonLoadsFromDataDrivenContentWithExpectedGlobalParameters()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        WorldBossDefinition boss = Assert.Single(
            package.WorldBosses!,
            item => item.Id == "WORLD_BOSS_ASH_ARCHON");

        Assert.Equal("Архон Пепла", boss.Name);
        Assert.Equal(30, boss.Level);
        Assert.Equal(1_000_000m, boss.BaseMaxHealth);
        Assert.Equal(1_800, boss.DurationSeconds);
        Assert.Equal("WB_ASH_ARCHON_V1", boss.EncounterProfileId);
        Assert.Equal("ASH_SHARD", boss.TokenCurrencyId);
        Assert.True(boss.IsEnabled);
        Assert.Equal([100m, 75m, 50m, 25m],
            boss.Phases.Select(phase => phase.StartsAtHealthPercent).ToArray());
    }

    [Theory]
    [InlineData(100, 1)]
    [InlineData(75, 2)]
    [InlineData(50, 3)]
    [InlineData(25, 4)]
    [InlineData(1, 4)]
    public async Task PhasePolicyUsesGlobalHealthThresholds(decimal healthPercent, int expectedPhase)
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        WorldBossDefinition boss = package.WorldBosses!.Single(
            item => item.Id == "WORLD_BOSS_ASH_ARCHON");

        decimal health = boss.BaseMaxHealth * healthPercent / 100m;

        Assert.Equal(expectedPhase,
            WorldBossPhasePolicy.ResolvePhase(boss, health, boss.BaseMaxHealth));
    }
}
