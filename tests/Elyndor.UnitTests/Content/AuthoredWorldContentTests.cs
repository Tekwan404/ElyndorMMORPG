using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Content;

namespace Elyndor.UnitTests.Content;

public sealed class AuthoredWorldContentTests
{
    private static readonly string[] ExpectedFieldLocations =
    [
        "WHISPERING_FOREST",
        "FLOWER_MEADOW",
        "DEEP_FOREST",
        "OLD_ROAD",
        "STONE_SPURS",
        "BLIGHTED_GROVE",
        "ASHEN_BORDER",
        "MOON_ASH_MARSHES",
        "ECLIPSE_OUTSKIRTS",
        "SHATTERED_LANDS",
        "BLACKSTONE_HIGHLANDS",
        "CRIMSON_WASTELAND",
        "OBSIDIAN_EDGE"
    ];

    [Fact]
    public async Task AuthoredFieldLocationsHaveResolvableEncounterLoot()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            RepositoryContentPath());
        GameContentIndexes indexes = GameContentIndexes.For(package);
        HashSet<string> authoredEncounterIds = [];
        int authoredEliteCount = 0;
        bool authoredEquipmentLootExists = false;

        foreach (string locationId in ExpectedFieldLocations)
        {
            Assert.True(indexes.LocationsById.TryGetValue(locationId, out var location));
            Assert.NotEmpty(location!.Encounters ?? []);

            foreach (var encounter in location.Encounters ?? [])
            {
                authoredEncounterIds.Add(encounter.MonsterId);
                Assert.True(indexes.MonstersById.TryGetValue(encounter.MonsterId, out MonsterDefinition? monster));
                Assert.True(monster!.Rank is MonsterRank.Normal or MonsterRank.Elite);
                Assert.Empty(monster.AbilityIds);
                Assert.Equal("AUTHORED_EMPTY_AI", monster.AiProfileId);
                if (monster.Rank == MonsterRank.Elite) authoredEliteCount++;
                Assert.False(string.IsNullOrWhiteSpace(monster.LootTableId));
                Assert.True(indexes.LootTablesById.TryGetValue(monster.LootTableId!, out var lootTable));

                foreach (var entry in lootTable!.Entries)
                {
                    Assert.True(indexes.ItemsById.ContainsKey(entry.ItemId));
                    if (indexes.ItemsById[entry.ItemId].Type == Elyndor.Core.Items.ItemType.Equipment)
                        authoredEquipmentLootExists = true;
                }
            }
        }

        Assert.Equal(169, authoredEncounterIds.Count);
        Assert.Equal(35, authoredEliteCount);
        Assert.True(authoredEquipmentLootExists);

        Assert.Equal("DEADLY", indexes.LocationsById["OBSIDIAN_EDGE"].DangerLevel);
    }

    [Fact]
    public async Task AuthoredRaidsAreStaticContentWithEmptyMobBehaviorAndResolvableLoot()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(RepositoryContentPath());
        GameContentIndexes indexes = GameContentIndexes.For(package);
        string[] raidIds = ["HEART_OF_BLIGHTED_GROVE", "SHATTERED_ORDER_CITADEL", "BLACK_BASTION"];
        string[] dungeonIds = raidIds.Select(id => $"{id}_RAID").ToArray();

        Assert.Equal(3, package.Dungeons!.Count(dungeon => dungeonIds.Contains(dungeon.Id)));
        foreach (string raidId in raidIds)
        {
            Assert.True(indexes.LocationsById.TryGetValue(raidId, out var location));
            Assert.False(location!.AllowAfk);
            Assert.NotNull(location.ArtId);

            var dungeon = package.Dungeons!.Single(definition => definition.Id == $"{raidId}_RAID");
            Assert.Equal(raidId, dungeon.EntryLocationId);
            Assert.NotEmpty(dungeon.Encounters);

            foreach (var encounter in dungeon.Encounters)
            {
                Assert.True(indexes.MonstersById.TryGetValue(encounter.MonsterId, out MonsterDefinition? monster));
                Assert.Empty(monster!.AbilityIds);
                Assert.Equal("AUTHORED_EMPTY_AI", monster.AiProfileId);
                Assert.False(string.IsNullOrWhiteSpace(monster.ArtId));
                Assert.True(indexes.LootTablesById.TryGetValue(monster.LootTableId!, out var lootTable));
                Assert.NotEmpty(lootTable!.Entries);
                Assert.All(lootTable.Entries, entry => Assert.True(indexes.ItemsById.ContainsKey(entry.ItemId)));
            }
        }

        Assert.Contains("BLACK_BASTION", indexes.LocationsById["OBSIDIAN_EDGE"].Transitions);
        Assert.Contains("HEART_OF_BLIGHTED_GROVE", indexes.LocationsById["BLIGHTED_GROVE"].Transitions);
        Assert.Contains("SHATTERED_ORDER_CITADEL", indexes.LocationsById["SHATTERED_LANDS"].Transitions);
        Assert.Contains(package.MonsterAiProfiles!, profile =>
            profile.Id == "AUTHORED_EMPTY_AI" && profile.PriorityAbilityIds.Count == 0);
    }

    private static string RepositoryContentPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "content", "package.json");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository content package was not found.");
    }
}
