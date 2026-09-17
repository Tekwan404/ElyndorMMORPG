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
    public async Task AuthoredFieldLocationsHaveOnlyResolvableNonRaidEncounterLoot()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            RepositoryContentPath());
        GameContentIndexes indexes = GameContentIndexes.For(package);

        foreach (string locationId in ExpectedFieldLocations)
        {
            Assert.True(indexes.LocationsById.TryGetValue(locationId, out var location));
            Assert.NotEmpty(location!.Encounters ?? []);

            foreach (var encounter in location.Encounters ?? [])
            {
                Assert.True(indexes.MonstersById.TryGetValue(encounter.MonsterId, out MonsterDefinition? monster));
                Assert.Equal(MonsterRank.Normal, monster!.Rank);
                Assert.False(string.IsNullOrWhiteSpace(monster.LootTableId));
                Assert.True(indexes.LootTablesById.TryGetValue(monster.LootTableId!, out var lootTable));

                foreach (var entry in lootTable!.Entries)
                {
                    Assert.True(indexes.ItemsById.ContainsKey(entry.ItemId));
                }
            }
        }

        Assert.DoesNotContain("BLACK_BASTION", indexes.LocationsById.Keys);
        Assert.DoesNotContain("HEART_OF_BLIGHTED_GROVE", indexes.LocationsById.Keys);
        foreach (string locationId in ExpectedFieldLocations)
        {
            var location = indexes.LocationsById[locationId];
            Assert.DoesNotContain("BLACK_BASTION", location.Transitions);
            Assert.DoesNotContain("HEART_OF_BLIGHTED_GROVE", location.Transitions);
            Assert.DoesNotContain("SHATTERED_ORDER_CITADEL", location.Transitions);
        }
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
