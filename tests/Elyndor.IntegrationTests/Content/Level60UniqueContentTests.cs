using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class Level60UniqueContentTests
{
    private static readonly Dictionary<string, string> ExpectedBranches = new(StringComparer.Ordinal)
    {
        ["WARRIOR_BERSERKER"] = "WARRIOR",
        ["WARRIOR_GUARDIAN"] = "WARRIOR",
        ["WARRIOR_WARLORD"] = "WARRIOR",
        ["MAGE_FIRE"] = "MAGE",
        ["MAGE_FROST"] = "MAGE",
        ["MAGE_ARCANE"] = "MAGE",
        ["ARCHER_MARKSMAN"] = "ARCHER",
        ["ARCHER_BEAST_MASTERY"] = "ARCHER",
        ["ARCHER_SURVIVAL"] = "ARCHER",
        ["PALADIN_HOLY"] = "PALADIN",
        ["PALADIN_PROTECTION"] = "PALADIN",
        ["PALADIN_RETRIBUTION"] = "PALADIN",
    };

    private static readonly Dictionary<string, string> LegacyWeaponIds = new(StringComparer.Ordinal)
    {
        ["WARRIOR_BERSERKER"] = "UNIQUE_WARRIOR_BLACKHEART",
        ["MAGE_ARCANE"] = "UNIQUE_MAGE_EYE_OF_DEAD_STAR",
        ["ARCHER_MARKSMAN"] = "UNIQUE_ARCHER_LAST_CONSTELLATION",
    };

    private static string[] MatrixItemIds()
    {
        List<string> ids = [];

        foreach (string branchId in ExpectedBranches.Keys)
        {
            string prefix = $"UNIQUE_L60_{branchId}";
            ids.Add(LegacyWeaponIds.GetValueOrDefault(branchId) ?? $"{prefix}_WEAPON");
            ids.Add($"{prefix}_RING");
            ids.Add($"{prefix}_CLOAK");
        }

        return ids.ToArray();
    }

    [Fact]
    public async Task Level60UniqueMatrixHasWeaponRingAndCloakForEveryBranch()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        string[] matrixIds = MatrixItemIds();
        Assert.Equal(36, matrixIds.Length);
        Assert.Equal(36, matrixIds.Distinct(StringComparer.Ordinal).Count());

        foreach ((string branchId, string classId) in ExpectedBranches)
        {
            string prefix = $"UNIQUE_L60_{branchId}";
            string weaponId = LegacyWeaponIds.GetValueOrDefault(branchId) ?? $"{prefix}_WEAPON";
            string ringId = $"{prefix}_RING";
            string cloakId = $"{prefix}_CLOAK";

            ItemDefinition weapon = Assert.Single(package.Items!, item => item.Id == weaponId);
            ItemDefinition ring = Assert.Single(package.Items!, item => item.Id == ringId);
            ItemDefinition cloak = Assert.Single(package.Items!, item => item.Id == cloakId);

            Assert.Equal(ItemRarity.Unique, weapon.Rarity);
            Assert.Equal(ItemRarity.Unique, ring.Rarity);
            Assert.Equal(ItemRarity.Unique, cloak.Rarity);
            Assert.Equal(EquipmentSlot.MainHand, weapon.Slot);
            Assert.Equal(EquipmentSlot.Ring1, ring.Slot);
            Assert.Equal(EquipmentSlot.Cloak, cloak.Slot);

            Assert.All(new[] { weapon, ring, cloak }, item =>
            {
                Assert.Equal(item.Id, item.ItemFamilyId);
                Assert.Equal(60, item.ItemLevelMax);
                Assert.Equal(3, item.GuaranteedAffixStatIds?.Count ?? 0);
                Assert.Equal(0.08m, item.ExtraAffixBudgetCap);
                Assert.Null(item.PrefixSuffixPolicyId);
                Assert.False(string.IsNullOrWhiteSpace(item.IconId));
                Assert.Single(item.AllowedClassIds!);
                Assert.Equal(classId, item.AllowedClassIds![0]);
            });

            if (LegacyWeaponIds.ContainsKey(branchId))
            {
                Assert.Equal(25, weapon.RequiredLevel);
                Assert.Equal(25, weapon.ItemLevelMin);
                Assert.Equal("UNIQUE_FAMILY", weapon.AffixCountProfileId);
            }
            else
            {
                Assert.Equal(60, weapon.RequiredLevel);
                Assert.Equal(60, weapon.ItemLevelMin);
                Assert.Equal("LEVEL_60_UNIQUE", weapon.AffixCountProfileId);
            }

            Assert.All(new[] { ring, cloak }, item =>
            {
                Assert.Equal(60, item.RequiredLevel);
                Assert.Equal(60, item.ItemLevelMin);
                Assert.Equal("LEVEL_60_UNIQUE", item.AffixCountProfileId);
            });

            Assert.False(string.IsNullOrWhiteSpace(weapon.WeaponCategory));
            Assert.True(weapon.WeaponDamageMin > 0);
            Assert.True(weapon.WeaponDamageMax > weapon.WeaponDamageMin);
        }
    }

    [Fact]
    public async Task Level60UniqueMatrixHasReachableAshArchonAcquisitionAndNoDeadIds()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        string[] matrixIds = MatrixItemIds();
        var normal = package.LootTables!.Single(
            table => table.Id == "WORLD_BOSS_ASH_ARCHON_LOOT");
        var topFive = package.LootTables!.Single(
            table => table.Id == "WORLD_BOSS_ASH_ARCHON_TOP5_LOOT");

        LootSelectionEntry[] normalEntries = normal.SelectionGroups!
            .SelectMany(group => group.Entries)
            .ToArray();
        LootSelectionEntry[] topFiveEntries = topFive.SelectionGroups!
            .SelectMany(group => group.Entries)
            .ToArray();

        Assert.All(matrixIds, itemId =>
        {
            ItemDefinition item = Assert.Single(package.Items!, candidate => candidate.Id == itemId);
            Assert.Equal(ItemRarity.Unique, item.Rarity);

            LootSelectionEntry normalEntry = Assert.Single(
                normalEntries,
                entry => entry.ItemId == itemId);
            LootSelectionEntry topFiveEntry = Assert.Single(
                topFiveEntries,
                entry => entry.ItemId == itemId);

            Assert.Equal(60, normalEntry.ItemLevelMin);
            Assert.Equal(60, normalEntry.ItemLevelMax);
            Assert.Equal(60, topFiveEntry.ItemLevelMin);
            Assert.Equal(60, topFiveEntry.ItemLevelMax);
        });
    }
}
