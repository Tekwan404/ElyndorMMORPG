using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Talents;
using Elyndor.Core.Items;
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

        Assert.Equal("0.19.0", package.ContentVersion);
        Assert.Equal("0.14.0", package.BalanceVersion);
        Assert.NotNull(package.LevelProgression);
        Assert.Contains(package.Items!, item => item.Id == "RECRUIT_IRON_SWORD");
        Assert.Contains(package.Items!, item => item.Id == "RECRUIT_WOODEN_SHIELD");
        Assert.Contains(package.Items!, item => item.Id == "HUNTER_SHORTBOW");
        Assert.Contains(package.Items!, item => item.Id == "APPRENTICE_STAFF");
        Assert.True(package.LootTables!.Count >= 11);
        Assert.Equal(100, package.ResourceScaling!.ManaBase);
        Assert.Equal(5, package.ResourceScaling.ManaPerIntellect);
        Assert.Equal(40, package.InventoryProfile!.DefaultCapacity);
        Assert.All(
            package.Locations,
            location => Assert.True(location.TravelDurationSeconds >= 0));

        ClassProfile mage = Assert.Single(package.ClassProfiles!, profile => profile.Id == "MAGE");
        Assert.Equal("INTELLECT", mage.PrimaryAttribute);
        Assert.Equal("MANA", mage.ResourceProfileId);
        Assert.Equal(MageWeaponCategories, mage.AllowedWeaponCategories);
        Assert.Equal(MageArmorCategories, mage.AllowedArmorCategories);
        Assert.Empty(mage.StartingAbilityIds ?? []);
        Assert.Empty(mage.AbilityUnlocks ?? []);
        Assert.NotNull(mage.CombatAutoAttack);

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

        Assert.True(package.EquipmentSets!.Count(set => set.Id.StartsWith("SET_", StringComparison.Ordinal)) >= 12);
        DungeonDefinition ancientMine = Assert.Single(package.Dungeons!, dungeon => dungeon.Id == "ANCIENT_MINE");
        Assert.Equal(
            [
                "ANCIENT_MINE_WOLF_L15",
                "ANCIENT_MINE_BOAR_L15",
                "ANCIENT_MINE_SPIDER_L16",
                "ANCIENT_MINE_GOBLIN_L16",
                "ANCIENT_MINE_BROODMOTHER_L16"
            ],
            ancientMine.Encounters.Select(encounter => encounter.MonsterId));
        Assert.Equal(3500, indexes.MonstersById["ANCIENT_MINE_BROODMOTHER_L16"].MaxHp);

        LootTableDefinition mineBossLoot = Assert.Single(
            package.LootTables!,
            table => table.Id == "ANCIENT_MINE_BOSS_LOOT");
        Assert.Contains(mineBossLoot.Entries, entry => entry.ItemId == "DUNGEON_MINES_WARRIOR_LEGENDARY_SHIELD");
        Assert.Contains(mineBossLoot.Entries, entry => entry.ItemId == "DUNGEON_MINES_MAGE_LEGENDARY_STAFF");
        Assert.Contains(mineBossLoot.Entries, entry => entry.ItemId == "DUNGEON_MINES_ARCHER_LEGENDARY_BOW");

        HashSet<string> obtainableItemIds = package.LootTables
            .SelectMany(table => table.Entries)
            .Select(entry => entry.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        ItemDefinition[] proceduralEquipment = package.Items!
            .Where(item => item.Type == ItemType.Equipment)
            .ToArray();
        Assert.NotEmpty(proceduralEquipment);
        Assert.All(proceduralEquipment, item =>
        {
            Assert.Null(item.PrimaryStatRanges);
            Assert.True(ProceduralItemPolicy.IsEnabled(item), item.Id);
        });

        ItemDefinition[] currentProgressionItems = proceduralEquipment
            .Where(item => item.RequiredLevel >= 2)
            .ToArray();
        Assert.NotEmpty(currentProgressionItems);
        Assert.All(currentProgressionItems, item => Assert.Contains(item.Id, obtainableItemIds));

        DungeonDefinition eclipsedCitadel = Assert.Single(
            package.Dungeons!,
            dungeon => dungeon.Id == "ECLIPSED_CITADEL");
        Assert.Equal(25, eclipsedCitadel.MinimumLevel);
        Assert.Equal(25, eclipsedCitadel.MaximumLevel);
        Assert.Equal(5, eclipsedCitadel.Encounters.Count);
        Assert.Equal("ECLIPSED_CITADEL_ARCHON_L25", eclipsedCitadel.Encounters[^1].MonsterId);

        MerchantDefinition marcus = Assert.Single(
            package.Merchants!,
            merchant => merchant.Id == "MARCUS_SUPPLIES");
        ItemDefinition[] marcusEquipment = package.Items!
            .Where(item => marcus.ItemIds.Contains(item.Id)
                && item.Type == ItemType.Equipment)
            .ToArray();
        Assert.NotEmpty(marcusEquipment);
        Assert.All(marcusEquipment, item =>
        {
            Assert.Equal(ItemRarity.Common, item.Rarity);
            Assert.Equal(2, item.RequiredLevel);
            Assert.True(item.BuyPriceGold > 0);
        });
        Assert.DoesNotContain(
            package.Items!,
            item => marcus.ItemIds.Contains(item.Id)
                && item.Type == ItemType.Equipment
                && item.Rarity is ItemRarity.Rare or ItemRarity.Epic or ItemRarity.Legendary or ItemRarity.Unique);

        LootTableDefinition citadelBossLoot = Assert.Single(
            package.LootTables!,
            table => table.Id == "ECLIPSED_CITADEL_BOSS_LOOT");
        Assert.Contains(citadelBossLoot.Entries, entry => entry.ItemId == "UNIQUE_WARRIOR_BLACKHEART_L25");
        Assert.Contains(citadelBossLoot.Entries, entry => entry.ItemId == "UNIQUE_MAGE_EYE_OF_DEAD_STAR_L25");
        Assert.Contains(citadelBossLoot.Entries, entry => entry.ItemId == "UNIQUE_ARCHER_LAST_CONSTELLATION_L25");
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
