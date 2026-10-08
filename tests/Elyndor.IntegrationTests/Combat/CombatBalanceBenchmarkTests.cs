using Elyndor.Core.Balance;
using Elyndor.Core.Combat.Simulation;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Combat;

public sealed class CombatBalanceBenchmarkTests
{
    [Fact]
    public async Task RealContentLoadsBalanceProfileAndEquipmentChangesSimulation()
    {
        var content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        Assert.Equal("0.44.0", content.ContentVersion);
        Assert.Equal("0.34.0", content.BalanceVersion);
        Assert.NotNull(content.CombatBalance);
        Assert.Equal(
            ["WEAK", "NORMAL", "GOOD"],
            content.CombatBalance!.GearStates.Select(item => item.Id));

        MonsterDefinition monster = Assert.Single(
            content.Monsters!,
            item => item.Id == "ASHEN_BORDER_OBUGLENNYI_DREVEN_L21");
        MonsterBalanceAuditEntry audit = MonsterBalanceAudit.Audit(
            content.CombatBalance,
            monster);
        Assert.True(audit.WithinTolerance);
        Assert.InRange(audit.HpDeltaPercent, 3m, 4m);

        CombatSimulationRunner simulator = new(content);
        CombatSimulationResult naked = simulator.Run(
            new CombatSimulationScenario(
                "WARRIOR",
                21,
                monster.Id,
                Iterations: 3,
                Seed: 20261002,
                MaxDurationSeconds: 60));
        CombatSimulationResult geared = simulator.Run(
            new CombatSimulationScenario(
                "WARRIOR",
                21,
                monster.Id,
                Iterations: 3,
                Seed: 20261002,
                MaxDurationSeconds: 60,
                GearState: CombatSimulationGearState.Good));

        Assert.Equal(CombatSimulationGearState.Good, geared.GearState);
        Assert.True(geared.PlayerMaxHp > naked.PlayerMaxHp);
        Assert.True(geared.PlayerArmor > naked.PlayerArmor);
        Assert.True(geared.PlayerPhysicalEhp > naked.PlayerPhysicalEhp);
        Assert.True(geared.AveragePlayerDps > naked.AveragePlayerDps);
    }

    [Fact]
    public async Task BenchmarkRunnerProducesLevelTwentyOneWarriorRow()
    {
        var content = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        CombatBalanceBenchmarkRunner runner = new(content);

        IReadOnlyList<CombatBalanceBenchmarkRow> rows = runner.Run(
            new CombatBalanceBenchmarkRequest(
                MinimumLevel: 21,
                MaximumLevel: 21,
                Iterations: 2,
                Seed: 21,
                MaxDurationSeconds: 60,
                ClassIds: ["WARRIOR"],
                GearStates: [CombatSimulationGearState.Normal]));

        CombatBalanceBenchmarkRow row = Assert.Single(rows);
        Assert.Equal(21, row.Level);
        Assert.Equal("WARRIOR", row.ClassId);
        Assert.Equal(CombatSimulationGearState.Normal, row.GearState);
        Assert.True(row.PlayerMaxHp > 0);
        Assert.True(row.PlayerArmor > 0);
        Assert.True(row.PlayerDps > 0);
        Assert.True(row.EnemyDps > 0);
    }
}
