using Elyndor.Core.WorldBosses;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
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
        Assert.Equal(60, boss.Level);
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

        Assert.Equal("WORLD_BOSS_ASH_ARCHON_L60", monster.Id);
        Assert.Equal(60, monster.Level);
        Assert.Equal(1_000_000m, monster.MaxHp);
        Assert.False(monster.GrantsXp);
        Assert.Equal(0, monster.LegacyXpReward);
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
    public async Task AshArchonRewardProfileUsesLevel60PersonalChestLoot()
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
            "UNIQUE_WARRIOR_BLACKHEART",
            "UNIQUE_MAGE_EYE_OF_DEAD_STAR",
            "UNIQUE_ARCHER_LAST_CONSTELLATION"
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
        Assert.Contains(chest.SelectionGroups[0].Entries, entry => entry.ItemId == "FOKUS_OSTATOCHNOI_MANY_L60");
        Assert.Contains(chest.SelectionGroups[0].Entries, entry => entry.ItemId == "LUK_TROINOGO_ASPEKTA_L60");
        Assert.Contains(chest.SelectionGroups[0].Entries, entry => entry.ItemId == "KOLTSO_RAZBITOGO_OTRAZHENIIA_L60");
        Assert.Contains(chest.SelectionGroups[0].Entries, entry => entry.ItemId == "PECHAT_TRIEDINSTVA_L60");

        string[] rewardItemIds = chest.SelectionGroups[0].Entries
            .Select(entry => entry.ItemId)
            .ToArray();
        Assert.Equal(7, rewardItemIds.Length);
        Assert.All(rewardItemIds, rewardItemId =>
        {
            ItemDefinition rewardItem = package.Items!.Single(item => item.Id == rewardItemId);
            Assert.Equal(60, rewardItem.RequiredLevel);
            Assert.Equal(60, rewardItem.ItemLevelMin);
            Assert.Equal(60, rewardItem.ItemLevelMax);
        });
        Assert.DoesNotContain(rewardItemIds, itemId => itemId.EndsWith("_L25", StringComparison.Ordinal));

        WorldBossLeaderboardRewardTierDefinition[] leaderboardTiers =
            rewardProfile.LeaderboardTiers!.ToArray();
        Assert.Contains(
            leaderboardTiers,
            tier => tier.Tier == WorldBossRewardTier.Top95
                && tier.ChestCount == 1
                && !tier.Enhanced);
        Assert.Contains(
            leaderboardTiers,
            tier => tier.Tier == WorldBossRewardTier.Top99
                && tier.ChestCount == 2
                && !tier.Enhanced);
        WorldBossLeaderboardRewardTierDefinition topFive = Assert.Single(
            leaderboardTiers,
            tier => tier.Tier == WorldBossRewardTier.Top5);
        Assert.Equal(5, topFive.MaxRank);
        Assert.Equal(2, topFive.ChestCount);
        Assert.True(topFive.Enhanced);
        Assert.Equal("WORLD_BOSS_ASH_ARCHON_TOP5_LOOT", topFive.LootTableId);
        Assert.Equal("WORLD_BOSS_ASH_ARCHON_TOP5_CHEST", topFive.ChestItemId);

        WorldBossLeaderboardRewardTierDefinition top95 = Assert.Single(
            leaderboardTiers,
            tier => tier.Tier == WorldBossRewardTier.Top95);
        Assert.Equal("WORLD_BOSS_ASH_ARCHON_CHEST", top95.ChestItemId);

        ItemDefinition normalChest = package.Items!.Single(
            item => item.Id == "WORLD_BOSS_ASH_ARCHON_CHEST");
        Assert.Equal(ItemType.LootContainer, normalChest.Type);
        Assert.Equal("WORLD_BOSS_ASH_ARCHON_LOOT", normalChest.LootContainerTableId);
        Assert.Equal(250, normalChest.LootContainerGoldMin);
        Assert.Equal(500, normalChest.LootContainerGoldMax);

        ItemDefinition enhancedChest = package.Items!.Single(
            item => item.Id == "WORLD_BOSS_ASH_ARCHON_TOP5_CHEST");
        Assert.Equal(ItemType.LootContainer, enhancedChest.Type);
        Assert.Equal("WORLD_BOSS_ASH_ARCHON_TOP5_LOOT", enhancedChest.LootContainerTableId);

        var topFiveChest = package.LootTables!.Single(
            table => table.Id == topFive.LootTableId);
        Assert.Single(topFiveChest.SelectionGroups!);
        Assert.Equal(1, topFiveChest.SelectionGroups![0].Rolls);
        Assert.All(uniqueIds, uniqueId =>
            Assert.Contains(
                topFiveChest.SelectionGroups[0].Entries,
                entry => entry.ItemId == uniqueId && entry.Weight > 1));
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
