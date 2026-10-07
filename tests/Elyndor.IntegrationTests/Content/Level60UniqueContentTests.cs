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

    [Fact]
    public async Task Level60UniqueMatrixHasWeaponRingAndCloakForEveryBranch()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] uniques = package.Items!
            .Where(item => item.Rarity == ItemRarity.Unique && item.RequiredLevel == 60)
            .ToArray();

        Assert.Equal(36, uniques.Length);

        foreach ((string branchId, string classId) in ExpectedBranches)
        {
            string prefix = $"UNIQUE_L60_{branchId}";
            string weaponId = LegacyWeaponIds.GetValueOrDefault(branchId) ?? $"{prefix}_WEAPON";
            string ringId = $"{prefix}_RING";
            string cloakId = $"{prefix}_CLOAK";

            ItemDefinition weapon = Assert.Single(uniques, item => item.Id == weaponId);
            ItemDefinition ring = Assert.Single(uniques, item => item.Id == ringId);
            ItemDefinition cloak = Assert.Single(uniques, item => item.Id == cloakId);

            Assert.Equal(EquipmentSlot.MainHand, weapon.Slot);
            Assert.Equal(EquipmentSlot.Ring1, ring.Slot);
            Assert.Equal(EquipmentSlot.Cloak, cloak.Slot);

            Assert.All(new[] { weapon, ring, cloak }, item =>
            {
                Assert.Equal(item.Id, item.ItemFamilyId);
                Assert.Equal(60, item.ItemLevelMin);
                Assert.Equal(60, item.ItemLevelMax);
                Assert.Equal("LEVEL_60_UNIQUE", item.AffixCountProfileId);
                Assert.Equal(3, item.GuaranteedAffixStatIds?.Count ?? 0);
                Assert.Equal(0.08m, item.ExtraAffixBudgetCap);
                Assert.Null(item.PrefixSuffixPolicyId);
                Assert.False(string.IsNullOrWhiteSpace(item.IconId));
                Assert.Single(item.AllowedClassIds!);
                Assert.Equal(classId, item.AllowedClassIds![0]);
            });

            Assert.False(string.IsNullOrWhiteSpace(weapon.WeaponCategory));
            Assert.True(weapon.WeaponDamageMin > 0);
            Assert.True(weapon.WeaponDamageMax > weapon.WeaponDamageMin);
        }
    }

    [Fact]
    public async Task Level60UniquesHaveReachableAshArchonAcquisitionAndNoDeadIds()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] uniques = package.Items!
            .Where(item => item.Rarity == ItemRarity.Unique && item.RequiredLevel == 60)
            .ToArray();
        Assert.Equal(36, uniques.Length);
        Assert.Equal(36, uniques.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());

        var normal = package.LootTables!.Single(
            table => table.Id == "WORLD_BOSS_ASH_ARCHON_LOOT");
        var topFive = package.LootTables!.Single(
            table => table.Id == "WORLD_BOSS_ASH_ARCHON_TOP5_LOOT");

        string[] normalIds = normal.SelectionGroups!
            .SelectMany(group => group.Entries)
            .Select(entry => entry.ItemId)
            .ToArray();
        string[] topFiveIds = topFive.SelectionGroups!
            .SelectMany(group => group.Entries)
            .Select(entry => entry.ItemId)
            .ToArray();

        Assert.All(uniques, item =>
        {
            Assert.Equal(1, normalIds.Count(id => id == item.Id));
            Assert.Equal(1, topFiveIds.Count(id => id == item.Id));
        });

        Assert.Contains(uniques, item => item.Id == "UNIQUE_WARRIOR_BLACKHEART");
        Assert.Contains(uniques, item => item.Id == "UNIQUE_MAGE_EYE_OF_DEAD_STAR");
        Assert.Contains(uniques, item => item.Id == "UNIQUE_ARCHER_LAST_CONSTELLATION");
    }
}
