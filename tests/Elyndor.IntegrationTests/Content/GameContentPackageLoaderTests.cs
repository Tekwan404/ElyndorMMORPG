using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Content;
using Elyndor.Core.Talents;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class GameContentPackageLoaderTests
{
    private static readonly string[] MageWeaponCategories = ["STAFF", "WAND"];
    private static readonly string[] MageArmorCategories = ["CLOTH"];
    private static readonly string[] ForestEncounterMonsters =
        ["FOREST_WOLF_L1", "FOREST_BOAR_L2", "GIANT_SPIDER_L3", "FOREST_WOLF_L4", "ALPHA_WOLF_L5"];

    [Fact]
    public async Task PhaseFiveMageAndLocationEncounterPackageLoadsAndValidates()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        Assert.Equal("0.14.0", package.ContentVersion);
        Assert.Equal("0.11.0", package.BalanceVersion);
        Assert.NotNull(package.LevelProgression);
        Assert.Contains(package.Items!, item => item.Id == "RECRUIT_IRON_SWORD");
        Assert.Contains(package.Items!, item => item.Id == "RECRUIT_WOODEN_SHIELD");
        Assert.Contains(package.Items!, item => item.Id == "HUNTER_SHORTBOW");
        Assert.Contains(package.Items!, item => item.Id == "APPRENTICE_STAFF");
        Assert.Equal(6, package.LootTables!.Count);
        Assert.Equal(100, package.ResourceScaling!.ManaBase);
        Assert.Equal(5, package.ResourceScaling.ManaPerIntellect);
        Assert.Equal(40, package.InventoryProfile!.DefaultCapacity);
        Assert.Equal(20, package.Quests!.Count);
        Assert.Contains(package.Quests, quest => quest.Id == "CONTRACT_BROODMOTHER_GATE");
        Assert.All(
            package.Locations,
            location => Assert.Equal(0, location.TravelDurationSeconds));

        ClassProfile mage = Assert.Single(package.ClassProfiles!, profile => profile.Id == "MAGE");
        Assert.Equal("INTELLECT", mage.PrimaryAttribute);
        Assert.Equal("MANA", mage.ResourceProfileId);
        Assert.Equal(MageWeaponCategories, mage.AllowedWeaponCategories);
        Assert.Equal(MageArmorCategories, mage.AllowedArmorCategories);
        Assert.Empty(mage.StartingAbilityIds ?? []);
        Assert.Empty(mage.AbilityUnlocks ?? []);
        Assert.NotNull(mage.CombatAutoAttack);

        TalentTreeDefinition warriorTree = Assert.Single(
            package.TalentTrees!, tree => tree.Id == "WARRIOR_TREE");
        TalentDefinition heavyPresence = Assert.Single(
            warriorTree.Nodes,
            node => node.Id == "G-1-4");
        Assert.True(TalentRuntimeAvailability.IsNodeFullySupported(heavyPresence));
        ResolvedTalentEventHook heavyPresenceHook = Assert.Single(
            TalentModifierResolver.Resolve(
                warriorTree,
                new Dictionary<string, int> { ["G-1-4"] = 4 })
                .EventHooks,
            hook => hook.TalentId == "G-1-4");
        Assert.Equal(15, heavyPresenceHook.Value);

        TalentTreeDefinition mageTree = Assert.Single(
            package.TalentTrees!, tree => tree.Id == "MAGE_TREE");
        Assert.Equal(
            ["FIRE", "ARCANE", "FROST"],
            mageTree.Branches.Select(branch => branch.Id));
        Assert.All(
            mageTree.Branches,
            branch => Assert.Equal(
                32,
                mageTree.Nodes.Count(node => node.BranchId == branch.Id)));
        Assert.Equal(96, mageTree.Nodes.Count);
        Assert.Equal(207, mageTree.Nodes.Sum(node => node.MaxRank));
        Assert.Contains(
            mageTree.Nodes,
            node => node.Id == "A-3-1"
                && node.Modifiers!.Any(modifier =>
                    modifier.Key == TalentModifierKeys.UnlockAbility
                    && modifier.TargetId == "ARCANE_BURST"));
        Assert.Contains(
            mageTree.Nodes,
            node => node.Id == "I-3-1"
                && node.Modifiers!.Any(modifier =>
                    modifier.Key == TalentModifierKeys.UnlockAbility
                    && modifier.TargetId == "ICE_LANCE"));

        LocationDefinition forest = Assert.Single(
            package.Locations,
            location => location.Id == "WHISPERING_FOREST");
        IReadOnlyList<LocationEncounterDefinition> encounters = forest.Encounters!;
        Assert.Equal(
            ForestEncounterMonsters,
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

        AbilityDefinition hunterMark = indexes.AbilitiesById["HUNTER_MARK"];
        Assert.Equal(
            1.05m,
            hunterMark.RuntimeParameters!["physicalDamageMultiplier"]);
        Assert.Equal(
            1.03m,
            hunterMark.RuntimeParameters["magicalDamageMultiplier"]);
        AbilityDefinition heavyArrow = indexes.AbilitiesById["HEAVY_ARROW"];
        Assert.Equal(
            1.75m,
            heavyArrow.RuntimeParameters!["autoAttackDamageMultiplier"]);
        AbilityDefinition beastSurge = indexes.AbilitiesById["BEAST_SURGE"];
        Assert.Equal(
            1.20m,
            beastSurge.RuntimeParameters!["companionDamageMultiplier"]);
        AbilityDefinition enchantedShot = indexes.AbilitiesById["ENCHANTED_SHOT"];
        Assert.Equal(
            0.70m,
            enchantedShot.RuntimeParameters!["spellPowerCoefficient"]);
        AbilityDefinition arcaneFlow = indexes.AbilitiesById["ARCANE_FLOW"];
        Assert.Equal(
            1.15m,
            arcaneFlow.RuntimeParameters!["damageMultiplier"]);
        Assert.Equal("FOREST_WOLF_L1", indexes.MonstersById["FOREST_WOLF_L1"].Id);
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
