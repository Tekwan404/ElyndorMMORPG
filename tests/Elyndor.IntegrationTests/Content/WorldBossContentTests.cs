using Elyndor.Core.WorldBosses;
using Elyndor.Core.Content;
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

    [Fact]
    public async Task AshArchonCombatProfileIsPlayableAndCannotLeakOrdinaryPveRewards()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        var boss = package.WorldBosses!.Single(
            item => item.Id == "WORLD_BOSS_ASH_ARCHON");
        var encounter = package.Encounters!.Single(
            item => item.Id == boss.EncounterProfileId);
        var monster = package.Monsters!.Single(
            item => item.Id == encounter.MonsterId);

        Assert.Equal("WORLD_BOSS_ASH_ARCHON_L30", monster.Id);
        Assert.Equal(30, monster.Level);
        Assert.Equal(1_000_000m, monster.MaxHp);
        Assert.Equal(0, monster.XpReward);
        Assert.Equal(0, monster.GoldRewardMin);
        Assert.Equal(0, monster.GoldRewardMax);
        Assert.Null(monster.LootTableId);
        Assert.Equal(
            ["P1", "P2", "P3", "P4"],
            encounter.Phases.Select(phase => phase.Id).ToArray());
        Assert.Equal(
            [100m, 75m, 50m, 25m],
            encounter.Phases.Select(phase =>
                phase.Trigger.Type == Elyndor.Core.Combat.Encounters.EncounterTriggerType.CombatStart
                    ? 100m
                    : phase.Trigger.Threshold).ToArray());
        Assert.Empty(GameContentPackageValidator.Validate(package));
    }

    [Fact]
    public async Task AshArchonRewardProfileMovesThreeUniqueWeaponsOutOfDungeonIntoPersonalChest()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        WorldBossDefinition boss = package.WorldBosses!.Single(
            item => item.Id == "WORLD_BOSS_ASH_ARCHON");
        WorldBossRewardProfileDefinition rewardProfile =
            package.WorldBossRewardProfiles!.Single(
                item => item.Id == boss.RewardProfileId);
        var chest = package.LootTables!.Single(
            table => table.Id == boss.LootTableId);
        var dungeonBoss = package.LootTables!.Single(
            table => table.Id == "ECLIPSED_CITADEL_BOSS_LOOT");

        Assert.Equal(5_000m, rewardProfile.MinimumContribution);
        Assert.Equal(200_000, rewardProfile.BossExperience);
        Assert.Equal(1_000, rewardProfile.BossGold);
        Assert.Equal(250, rewardProfile.ChestGoldMin);
        Assert.Equal(500, rewardProfile.ChestGoldMax);

        string[] uniqueIds =
        [
            "UNIQUE_WARRIOR_BLACKHEART_L25",
            "UNIQUE_MAGE_EYE_OF_DEAD_STAR_L25",
            "UNIQUE_ARCHER_LAST_CONSTELLATION_L25"
        ];
        Assert.All(uniqueIds, uniqueId =>
        {
            Assert.DoesNotContain(dungeonBoss.Entries, entry => entry.ItemId == uniqueId);
            Assert.Contains(
                chest.SelectionGroups!.SelectMany(group => group.Entries),
                entry => entry.ItemId == uniqueId);
        });

        Assert.Single(chest.SelectionGroups!);
        Assert.Equal(1, chest.SelectionGroups![0].Rolls);
        Assert.Contains(chest.SelectionGroups[0].Entries, entry => entry.ItemId == "FOKUS_OSTATOCHNOI_MANY");
        Assert.Contains(chest.SelectionGroups[0].Entries, entry => entry.ItemId == "LUK_TROINOGO_ASPEKTA");
        Assert.Contains(chest.SelectionGroups[0].Entries, entry => entry.ItemId == "KOLTSO_RAZBITOGO_OTRAZHENIIA");
        Assert.Contains(chest.SelectionGroups[0].Entries, entry => entry.ItemId == "PECHAT_TRIEDINSTVA");
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
