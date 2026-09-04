using Elyndor.Core.Content;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class GameContentPackageLoaderTests
{
    [Fact]
    public async Task PhaseFiveMageAndLocationEncounterPackageLoadsAndValidates()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        Assert.Equal("0.9.1", package.ContentVersion);
        Assert.Equal("0.9.1", package.BalanceVersion);
        Assert.NotNull(package.LevelProgression);
        Assert.Equal(9, package.Items!.Count);
        Assert.Equal(3, package.LootTables!.Count);
        Assert.Equal(100, package.ResourceScaling!.ManaBase);
        Assert.Equal(5, package.ResourceScaling.ManaPerIntellect);

        ClassProfile mage = Assert.Single(package.ClassProfiles!, profile => profile.Id == "MAGE");
        Assert.Equal("INTELLECT", mage.PrimaryAttribute);
        Assert.Equal("MANA", mage.ResourceProfileId);
        Assert.Equal(new[] { "STAFF", "WAND" }, mage.AllowedWeaponCategories);
        Assert.Equal(new[] { "LIGHT" }, mage.AllowedArmorCategories);
        Assert.Equal(
            new[] { "MAGE_FIREBALL", "MAGE_ARCANE_SPARK", "MAGE_ICE_SHARD" },
            mage.StartingAbilityIds);
        Assert.NotNull(mage.CombatAutoAttack);

        TalentTreeDefinition mageTree = Assert.Single(
            package.TalentTrees!, tree => tree.Id == "MAGE_TREE");
        TalentBranchDefinition fire = Assert.Single(mageTree.Branches);
        Assert.Equal("FIRE", fire.Id);
        Assert.Equal(32, mageTree.Nodes.Count);
        Assert.Equal(69, mageTree.Nodes.Sum(node => node.MaxRank));

        LocationDefinition forest = Assert.Single(
            package.Locations,
            location => location.Id == "WHISPERING_FOREST");
        IReadOnlyList<LocationEncounterDefinition> encounters = forest.Encounters!;
        Assert.Equal(
            new[] { "WOLF", "FOREST_BOAR", "GIANT_SPIDER" },
            encounters.Select(encounter => encounter.MonsterId));
        Assert.All(encounters, encounter => Assert.True(encounter.Weight > 0));
        Assert.All(
            package.Monsters!.Where(monster => encounters.Any(encounter => encounter.MonsterId == monster.Id)),
            monster =>
            {
                Assert.False(string.IsNullOrWhiteSpace(monster.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(monster.Description));
                Assert.False(string.IsNullOrWhiteSpace(monster.ArtId));
            });

        Assert.Empty(GameContentPackageValidator.Validate(package));
        Assert.Empty(WorldEncounterContentValidator.Validate(package));

        GameContentIndexes indexes = GameContentIndexes.For(package);
        Assert.Same(indexes, GameContentIndexes.For(package));
        Assert.Equal("MAGE", indexes.ClassesById["MAGE"].Id);
        Assert.Equal("MAGE_FIREBALL", indexes.AbilitiesById["MAGE_FIREBALL"].Id);
        Assert.Equal("WOLF", indexes.MonstersById["WOLF"].Id);
        Assert.Equal("WHISPERING_FOREST", indexes.LocationsById["WHISPERING_FOREST"].Id);
    }

    [Fact]
    public async Task CategoryLocationFragmentIsScannedAndIndexed()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"elyndor-content-{Guid.CreateVersion7():N}");
        string locationsDirectory = Path.Combine(directory, "locations");
        Directory.CreateDirectory(locationsDirectory);
        string packagePath = Path.Combine(directory, "package.json");

        const string packageJson = """
            {
              "contentVersion": "0.1.0",
              "balanceVersion": "0.1.0",
              "publishedAtUtc": "2026-08-29T00:00:00+00:00",
              "definitions": [],
              "locations": [
                {
                  "id": "STARTER_TOWN",
                  "displayName": "Starter Town",
                  "dangerLevel": "SAFE",
                  "recommendedLevel": 1,
                  "transitions": []
                }
              ]
            }
            """;
        const string locationFragment = """
            {
              "contentVersion": "0.1.1",
              "balanceVersion": "0.1.0",
              "publishedAtUtc": "2026-09-04T12:00:00+00:00",
              "locations": [
                {
                  "id": "TEST_CAMP",
                  "displayName": "Test Camp",
                  "dangerLevel": "SAFE",
                  "recommendedLevel": 1,
                  "transitions": []
                }
              ]
            }
            """;

        try
        {
            await File.WriteAllTextAsync(packagePath, packageJson);
            await File.WriteAllTextAsync(
                Path.Combine(locationsDirectory, "test-camp.json"),
                locationFragment);

            GameContentPackage package = await GameContentPackageLoader.LoadAsync(packagePath);
            GameContentIndexes indexes = GameContentIndexes.For(package);

            Assert.Equal("0.1.1", package.ContentVersion);
            Assert.Equal(2, package.Locations.Count);
            Assert.True(indexes.LocationsById.ContainsKey("STARTER_TOWN"));
            Assert.True(indexes.LocationsById.ContainsKey("TEST_CAMP"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsyncReturnsValidatedPackage()
    {
        const string json = """
            {
              "contentVersion": "0.1.0",
              "balanceVersion": "0.1.0",
              "publishedAtUtc": "2026-08-29T00:00:00+00:00",
              "definitions": [],
              "locations": [
                {
                  "id": "STARTER_TOWN",
                  "displayName": "Starter Town",
                  "dangerLevel": "SAFE",
                  "recommendedLevel": 1,
                  "transitions": []
                }
              ]
            }
            """;

        await WithTemporaryPackageAsync(json, async path =>
        {
            GameContentPackage package = await GameContentPackageLoader.LoadAsync(path);

            Assert.Equal("0.1.0", package.ContentVersion);
            Assert.Empty(package.Definitions);
            Assert.Equal("STARTER_TOWN", Assert.Single(package.Locations).Id);
        });
    }

    [Fact]
    public async Task LoadAsyncRejectsUnknownJsonProperties()
    {
        const string json = """
            {
              "contentVersion": "0.1.0",
              "balanceVersion": "0.1.0",
              "publishedAtUtc": "2026-08-29T00:00:00+00:00",
              "definitions": [],
              "locations": [],
              "unexpected": true
            }
            """;

        await WithTemporaryPackageAsync(json, async path =>
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => GameContentPackageLoader.LoadAsync(path));
        });
    }

    [Fact]
    public async Task LoadAsyncRejectsSemanticValidationErrors()
    {
        const string json = """
            {
              "contentVersion": "0.1.0",
              "balanceVersion": "0.1.0",
              "publishedAtUtc": "2026-08-29T00:00:00+00:00",
              "definitions": [
                {
                  "type": "CLASS",
                  "id": "WARRIOR",
                  "references": [
                    { "type": "ABILITY", "id": "MISSING" }
                  ]
                }
              ],
              "locations": []
            }
            """;

        await WithTemporaryPackageAsync(json, async path =>
        {
            ContentPackageValidationException exception =
                await Assert.ThrowsAsync<ContentPackageValidationException>(
                    () => GameContentPackageLoader.LoadAsync(path));

            Assert.Contains(exception.Errors, error => error.Code == "MISSING_REFERENCE");
        });
    }

    private static async Task WithTemporaryPackageAsync(
        string json,
        Func<string, Task> assertion)
    {
        string path = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(path, json);
            await assertion(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
