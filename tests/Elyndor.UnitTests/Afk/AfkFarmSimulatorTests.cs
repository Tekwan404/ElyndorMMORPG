using System.Text.Json;
using Elyndor.Core.Afk;
using Elyndor.Core.Characters;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
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
        Assert.Equal(first.EfficiencyPercent, second.EfficiencyPercent);
        Assert.Equal(first.XpCandidate, second.XpCandidate);
        Assert.Equal(first.GoldCandidate, second.GoldCandidate);
        Assert.Equal(first.LootCandidates, second.LootCandidates);
        Assert.Equal(first.DefeatedMonsterIds, second.DefeatedMonsterIds);
        Assert.Equal(first.Kills, first.DefeatedMonsterIds.Count);
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
        AfkFarmSimulationResult underpowered = AfkFarmSimulator.Simulate(CreateRequest(CreateSnapshot(1)));
        Assert.True(stronger.EfficiencyPercent > underpowered.EfficiencyPercent);
    }

    [Fact]
    public void SafeInstantAbilityIncreasesKillsInTheSameInterval()
    {
        AbilityDefinition strike = new(
            "AFK_TEST_STRIKE",
            AbilityType.Instant,
            AbilityTargetType.SingleEnemy,
            10,
            TimeSpan.FromSeconds(4),
            TimeSpan.Zero,
            true,
            GlobalCooldownCategory.Standard,
            false,
            "PHYSICAL",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    DamageType: Elyndor.Core.Combat.Damage.DamageType.Physical,
                    AttackPowerCoefficient: 1.5m)
            ]);
        MonsterDefinition monster = CreateMonster("ABILITY_TARGET", MonsterRank.Normal, 140, 5);
        AfkCharacterSnapshot snapshot = CreateSnapshot(12, [strike.Id], currentResource: 100);

        AfkFarmSimulationResult autoOnly = AfkFarmSimulator.Simulate(
            CreateRequest(snapshot, monster));
        AfkFarmSimulationResult withAbility = AfkFarmSimulator.Simulate(
            CreateRequest(
                snapshot,
                monster,
                new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
                {
                    [strike.Id] = strike
                }));

        Assert.True(withAbility.Kills > autoOnly.Kills);
        Assert.True(withAbility.XpCandidate > autoOnly.XpCandidate);
    }

    [Fact]
    public void CastedDirectDamageAbilityIncreasesMageFarmThroughput()
    {
        AbilityDefinition fireball = new(
            "AFK_TEST_FIREBALL",
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1.8),
            true,
            GlobalCooldownCategory.Standard,
            true,
            "FIRE",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    Amount: 180,
                    DamageType: Elyndor.Core.Combat.Damage.DamageType.Magical,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false)
            ]);
        MonsterDefinition monster = CreateMonster("CAST_TARGET", MonsterRank.Normal, 140, 5);
        AfkCharacterSnapshot mage = CreateSnapshot(
            4,
            [fireball.Id],
            classId: "MAGE",
            autoAttackInterval: TimeSpan.FromSeconds(2.6));

        AfkFarmSimulationResult autoOnly = AfkFarmSimulator.Simulate(
            CreateRequest(mage, monster));
        AfkFarmSimulationResult withFireball = AfkFarmSimulator.Simulate(
            CreateRequest(
                mage,
                monster,
                new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
                {
                    [fireball.Id] = fireball
                }));

        Assert.True(withFireball.Kills > autoOnly.Kills);
        Assert.True(withFireball.XpCandidate > autoOnly.XpCandidate);
        Assert.True(withFireball.EfficiencyPercent > autoOnly.EfficiencyPercent);
    }

    [Fact]
    public void CastDrivenEfficiencyDoesNotFavorTheFasterClassAutoAttackProfile()
    {
        AbilityDefinition spell = new(
            "AFK_TEST_NEUTRAL_CAST",
            AbilityType.Casted,
            AbilityTargetType.SingleEnemy,
            0,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1.8),
            true,
            GlobalCooldownCategory.Standard,
            true,
            "ARCANE",
            Actions:
            [
                new AbilityActionDefinition(
                    AbilityActionType.Damage,
                    Amount: 180,
                    DamageType: Elyndor.Core.Combat.Damage.DamageType.Magical,
                    CanMiss: false,
                    CanCrit: false,
                    CanDodge: false)
            ]);
        MonsterDefinition monster = CreateMonster("NEUTRAL_TARGET", MonsterRank.Normal, 140, 5);
        IReadOnlyDictionary<string, AbilityDefinition> abilities =
            new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal)
            {
                [spell.Id] = spell
            };
        AfkCharacterSnapshot warriorProfile = CreateSnapshot(
            4,
            [spell.Id],
            classId: "WARRIOR",
            autoAttackInterval: TimeSpan.FromSeconds(2));
        AfkCharacterSnapshot mageProfile = CreateSnapshot(
            4,
            [spell.Id],
            classId: "MAGE",
            autoAttackInterval: TimeSpan.FromSeconds(2.6));

        AfkFarmSimulationResult warrior = AfkFarmSimulator.Simulate(
            CreateRequest(warriorProfile, monster, abilities));
        AfkFarmSimulationResult mage = AfkFarmSimulator.Simulate(
            CreateRequest(mageProfile, monster, abilities));

        Assert.Equal(warrior.Kills, mage.Kills);
        Assert.Equal(warrior.EfficiencyPercent, mage.EfficiencyPercent);
        Assert.Equal(100, mage.EfficiencyPercent);
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

    [Fact]
    public void TargetedFarmUsesOnlyTheSelectedNormalMonster()
    {
        AfkCharacterSnapshot character = CreateSnapshot(20);
        MonsterDefinition wolf = CreateMonster("WOLF", MonsterRank.Normal, 45, 9);
        MonsterDefinition boar = CreateMonster("BOAR", MonsterRank.Normal, 90, 9, lootTableId: "BOAR_LOOT");
        DateTimeOffset start = new(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        AfkFarmSimulationRequest request = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            0,
            character,
            new LocationDefinition(
                "TEST_LOCATION", "Test", "ADVENTURE", 1, [],
                [new LocationEncounterDefinition(wolf.Id, 1), new LocationEncounterDefinition(boar.Id, 1)],
                AllowAfk: true),
            new Dictionary<string, MonsterDefinition> { [wolf.Id] = wolf, [boar.Id] = boar },
            start,
            start.AddMinutes(15),
            "content-v1",
            TargetMonsterId: boar.Id);

        AfkFarmSimulationResult result = AfkFarmSimulator.Simulate(request);

        Assert.NotEmpty(result.LootCandidates);
        Assert.All(result.LootCandidates, candidate => Assert.Equal(boar.Id, candidate.MonsterId));
        Assert.NotEmpty(result.DefeatedMonsterIds);
        Assert.All(result.DefeatedMonsterIds, monsterId => Assert.Equal(boar.Id, monsterId));
    }

    private static AfkFarmSimulationRequest CreateRequest(
        AfkCharacterSnapshot snapshot,
        MonsterDefinition? suppliedMonster = null,
        IReadOnlyDictionary<string, AbilityDefinition>? abilities = null)
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
            "content-v1",
            AbilitiesById: abilities);
    }

    private static AfkCharacterSnapshot CreateSnapshot(
        decimal attackPower,
        IReadOnlyList<string>? knownAbilityIds = null,
        decimal currentResource = 0,
        string classId = "WARRIOR",
        TimeSpan? autoAttackInterval = null)
    {
        string resourceId = string.Equals(classId, "MAGE", StringComparison.Ordinal)
            ? "MANA"
            : "RAGE";
        ClassProfile profile = new(
            classId, classId == "MAGE" ? "INTELLECT" : "STRENGTH", resourceId,
            new PrimaryStats(1, 1, 1, 1),
            new PrimaryStats(1, 1, 1, 1), [], [], "test",
            CombatAutoAttack: new AutoAttackProfile(
                autoAttackInterval ?? TimeSpan.FromSeconds(2),
                8,
                1,
                0));
        ResourceProfile resource = new(
            resourceId,
            100,
            0,
            0,
            10,
            0,
            5,
            5);
        return new AfkCharacterSnapshot(
            profile.Id,
            5,
            new CharacterStats(1, 1, 1, 1, 100, attackPower, 0, 0, 1, 100, 0, 0, 1, 0, 0, 0),
            100,
            currentResource,
            JsonSerializer.Serialize(profile),
            JsonSerializer.Serialize(resource),
            "{}",
            new Dictionary<string, int>(),
            JsonSerializer.Serialize(ResolvedTalentModifiers.Empty),
            knownAbilityIds ?? []);
    }

    private static MonsterDefinition CreateMonster(
        string id,
        MonsterRank rank,
        decimal maxHp,
        decimal attackDamage,
        string lootTableId = "NONE") => new(
        id, id, rank, 3, maxHp,
        new CombatStats(3, 0, 0, 0, 1, 0, 0, 0, 0, attackDamage, 0),
        TimeSpan.FromSeconds(2), attackDamage, [], "NONE",
        LegacyXpReward: 10, LootTableId: lootTableId, GoldRewardMin: 2, GoldRewardMax: 4);
}
