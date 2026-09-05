using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Combat.Simulation;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;

namespace Elyndor.UnitTests.Combat;

public sealed class CombatSimulationRunnerTests
{
    [Fact]
    public void SameSeedAndScenarioProduceSameAggregateResult()
    {
        GameContentPackage package = CreatePackage();
        CombatSimulationScenario scenario = new(
            "WARRIOR",
            5,
            "WOLF",
            Iterations: 20,
            Seed: 4242,
            MaxDurationSeconds: 20);

        CombatSimulationResult first = new CombatSimulationRunner(package).Run(scenario);
        CombatSimulationResult second = new CombatSimulationRunner(package).Run(scenario);

        Assert.Equal(first, second);
        Assert.Equal(20, first.Iterations);
        Assert.Equal(20, first.Victories + first.Defeats + first.Timeouts);
        Assert.True(first.AveragePlayerDps > 0);
    }

    [Fact]
    public void UnlockLevelChangesDefaultSimulationAbilitySet()
    {
        GameContentPackage package = CreatePackage();
        CombatSimulationResult levelOne = new CombatSimulationRunner(package).Run(
            new CombatSimulationScenario("WARRIOR", 1, "WOLF", 1, 1, 10));
        CombatSimulationResult levelFive = new CombatSimulationRunner(package).Run(
            new CombatSimulationScenario("WARRIOR", 5, "WOLF", 1, 1, 10));

        Assert.DoesNotContain(levelOne.DamageSources, source => source.DefinitionId == "HEAVY_BLOW");
        Assert.Contains(levelFive.DamageSources, source => source.DefinitionId == "HEAVY_BLOW");
    }

    private static GameContentPackage CreatePackage()
    {
        AbilityDefinition strike = new(
            "STRIKE",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.Zero,
            true,
            GlobalCooldownCategory.Standard,
            false,
            "PHYSICAL",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    3,
                    DamageType.Physical,
                    AttackPowerCoefficient: 0.8m)
            ]);
        AbilityDefinition heavy = new(
            "HEAVY_BLOW",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            10,
            TimeSpan.FromSeconds(2),
            TimeSpan.Zero,
            true,
            GlobalCooldownCategory.Standard,
            false,
            "PHYSICAL",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    8,
                    DamageType.Physical,
                    AttackPowerCoefficient: 1.2m)
            ]);

        return new GameContentPackage(
            "SIM_TEST",
            "SIM_BALANCE",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            [],
            [],
            ClassProfiles:
            [
                new ClassProfile(
                    "WARRIOR",
                    "STRENGTH",
                    "RAGE",
                    new PrimaryStats(12, 6, 4, 10),
                    new PrimaryStats(3, 1, 0.5m, 2),
                    ["ONE_HAND_SWORD"],
                    ["HEAVY"],
                    "Simulation warrior",
                    StartingAbilityIds: ["STRIKE"],
                    AbilityUnlocks: [new AbilityUnlockDefinition("HEAVY_BLOW", 2)],
                    CombatAutoAttack: new AutoAttackProfile(
                        TimeSpan.FromSeconds(2),
                        2,
                        0.5m,
                        10))
            ],
            StatFormula: new StatFormulaProfile(
                "DEFAULT",
                100,
                10,
                1,
                0,
                1,
                1,
                0,
                1,
                0,
                5,
                0,
                100,
                95,
                0,
                1),
            ResourceProfiles:
            [
                new ResourceProfile("RAGE", 100, 0, 0, 0, 0, 5, 5)
            ],
            Abilities: [strike, heavy],
            Monsters:
            [
                new MonsterDefinition(
                    "WOLF",
                    "Wolf",
                    MonsterRank.Normal,
                    3,
                    180,
                    new CombatStats(3, 95, 0, 0, 1, 5, 0, 0, 0, 8, 0),
                    TimeSpan.FromSeconds(2.5),
                    3,
                    [],
                    "WOLF_AI",
                    AutoAttackAttackPowerCoefficient: 0.5m,
                    DisplayName: "Wolf",
                    Description: "Test wolf",
                    ArtId: "WOLF")
            ],
            MonsterAiProfiles:
            [
                new MonsterAiProfile("WOLF_AI", [])
            ]);
    }
}
