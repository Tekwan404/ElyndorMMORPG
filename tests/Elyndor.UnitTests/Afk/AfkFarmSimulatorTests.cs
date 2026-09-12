using System.Text.Json;
using Elyndor.Core.Afk;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Core.World;

namespace Elyndor.UnitTests.Afk;

public sealed class AfkFarmSimulatorTests
{
    [Fact]
    public void SameRequestProducesTheSameResult()
    {
        AfkFarmSimulationRequest request = CreateRequest(CreateSnapshot(15));
        AfkFarmSimulationResult first = AfkFarmSimulator.Simulate(request);
        AfkFarmSimulationResult second = AfkFarmSimulator.Simulate(request);

        Assert.Equal(first.SimulatedDuration, second.SimulatedDuration);
        Assert.Equal(first.EncounteredEnemies, second.EncounteredEnemies);
        Assert.Equal(first.Kills, second.Kills);
        Assert.Equal(first.FailedKills, second.FailedKills);
        Assert.Equal(first.EstimatedIncomingDamage, second.EstimatedIncomingDamage);
        Assert.Equal(first.XpCandidate, second.XpCandidate);
        Assert.Equal(first.GoldCandidate, second.GoldCandidate);
        Assert.Equal(first.LootCandidates, second.LootCandidates);
        Assert.True(first.Kills > 0);
        Assert.Equal(request.Character.CurrentHp, first.ResultingHpEstimate);
    }

    [Fact]
    public void StrongerCharacterKillsMoreMonstersInTheSameInterval()
    {
        AfkFarmSimulationResult weaker = AfkFarmSimulator.Simulate(CreateRequest(CreateSnapshot(8)));
        AfkFarmSimulationResult stronger = AfkFarmSimulator.Simulate(CreateRequest(CreateSnapshot(28)));

        Assert.True(stronger.Kills > weaker.Kills);
        Assert.True(stronger.XpCandidate > weaker.XpCandidate);
    }

    [Fact]
    public void BossEncountersAreIgnored()
    {
        MonsterDefinition boss = CreateMonster("BOSS", MonsterRank.Boss, 1_000, 100);
        AfkFarmSimulationRequest request = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            0,
            CreateSnapshot(20),
            new LocationDefinition(
                "TEST_LOCATION", "Test", "ADVENTURE", 1, [],
                [new LocationEncounterDefinition(boss.Id, 1)], AllowAfk: true),
            new Dictionary<string, MonsterDefinition> { [boss.Id] = boss },
            new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 12, 0, 15, 0, TimeSpan.Zero),
            "content-v1");

        AfkFarmSimulationResult result = AfkFarmSimulator.Simulate(request);

        Assert.Equal(0, result.EncounteredEnemies);
        Assert.Equal(0, result.Kills);
    }

    [Fact]
    public void StrongerMonsterReducesKillRate()
    {
        AfkCharacterSnapshot character = CreateSnapshot(20);
        MonsterDefinition weakMonster = CreateMonster("WEAK", MonsterRank.Normal, 40, 5);
        MonsterDefinition strongMonster = CreateMonster("STRONG", MonsterRank.Normal, 300, 5);

        AfkFarmSimulationResult weakResult = AfkFarmSimulator.Simulate(
            CreateRequest(character, weakMonster));
        AfkFarmSimulationResult strongResult = AfkFarmSimulator.Simulate(
            CreateRequest(character, strongMonster));

        Assert.True(weakResult.Kills > strongResult.Kills);
    }

    private static AfkFarmSimulationRequest CreateRequest(
        AfkCharacterSnapshot snapshot,
        MonsterDefinition? suppliedMonster = null)
    {
        MonsterDefinition wolf = suppliedMonster ?? CreateMonster("WOLF", MonsterRank.Normal, 45, 9);
        return new AfkFarmSimulationRequest(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            0,
            snapshot,
            new LocationDefinition(
                "TEST_LOCATION", "Test", "ADVENTURE", 1, [],
                [new LocationEncounterDefinition(wolf.Id, 1)], AllowAfk: true),
            new Dictionary<string, MonsterDefinition> { [wolf.Id] = wolf },
            new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 12, 0, 15, 0, TimeSpan.Zero),
            "content-v1");
    }

    private static AfkCharacterSnapshot CreateSnapshot(decimal attackPower)
    {
        ClassProfile profile = new(
            "WARRIOR", "STRENGTH", "RAGE", new PrimaryStats(1, 1, 1, 1),
            new PrimaryStats(1, 1, 1, 1), [], [], "test",
            CombatAutoAttack: new AutoAttackProfile(TimeSpan.FromSeconds(2), 8, 1, 0));
        return new AfkCharacterSnapshot(
            profile.Id,
            5,
            new CharacterStats(1, 1, 1, 1, 100, attackPower, 0, 0, 1, 100, 0, 0, 1, 0, 0, 0),
            100,
            0,
            JsonSerializer.Serialize(profile),
            "{}", "{}", new Dictionary<string, int>(), "{}", []);
    }

    private static MonsterDefinition CreateMonster(
        string id,
        MonsterRank rank,
        decimal maxHp,
        decimal attackDamage) => new(
        id, id, rank, 3, maxHp,
        new CombatStats(3, 0, 0, 0, 1, 0, 0, 0, 0, attackDamage, 0),
        TimeSpan.FromSeconds(2), attackDamage, [], "NONE",
        XpReward: 10, GoldRewardMin: 2, GoldRewardMax: 4);
}
