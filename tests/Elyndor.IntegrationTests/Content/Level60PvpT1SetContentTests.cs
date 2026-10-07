using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class Level60PvpT1SetContentTests
{
    private static readonly int[] ExpectedBonusThresholds = [2, 4, 6];

    private static readonly Dictionary<string, string> ExpectedSets = new(StringComparer.Ordinal)
    {
        ["SET_L60_PVP_T1_WARRIOR_GUARDIAN"] = "WARRIOR",
        ["SET_L60_PVP_T1_WARRIOR_BERSERKER"] = "WARRIOR",
        ["SET_L60_PVP_T1_WARRIOR_WARLORD"] = "WARRIOR",
        ["SET_L60_PVP_T1_MAGE_FIRE"] = "MAGE",
        ["SET_L60_PVP_T1_MAGE_ARCANE"] = "MAGE",
        ["SET_L60_PVP_T1_MAGE_FROST"] = "MAGE",
        ["SET_L60_PVP_T1_ARCHER_MARKSMAN"] = "ARCHER",
        ["SET_L60_PVP_T1_ARCHER_BEAST_MASTERY"] = "ARCHER",
        ["SET_L60_PVP_T1_ARCHER_SURVIVAL"] = "ARCHER",
        ["SET_L60_PVP_T1_PALADIN_HOLY"] = "PALADIN",
        ["SET_L60_PVP_T1_PALADIN_PROTECTION"] = "PALADIN",
        ["SET_L60_PVP_T1_PALADIN_RETRIBUTION"] = "PALADIN",
    };

    private static readonly Dictionary<EquipmentSlot, long> ExpectedHonorPrices = new()
    {
        [EquipmentSlot.Chest] = 100,
        [EquipmentSlot.Legs] = 90,
        [EquipmentSlot.Head] = 80,
        [EquipmentSlot.Shoulders] = 70,
        [EquipmentSlot.Hands] = 60,
        [EquipmentSlot.Feet] = 60,
    };

    [Fact]
    public async Task PvpT1HasTwelveCompleteLegendaryBranchSetsSoldForHonor()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] items = package.Items!
            .Where(item => item.SetId is not null && ExpectedSets.ContainsKey(item.SetId))
            .ToArray();

        Assert.Equal(72, items.Length);

        EquipmentSlot[] expectedSlots =
        [
            EquipmentSlot.Head,
            EquipmentSlot.Shoulders,
            EquipmentSlot.Chest,
            EquipmentSlot.Hands,
            EquipmentSlot.Legs,
            EquipmentSlot.Feet,
        ];

        foreach ((string setId, string classId) in ExpectedSets)
        {
            ItemDefinition[] pieces = items.Where(item => item.SetId == setId).ToArray();
            Assert.Equal(6, pieces.Length);
            Assert.Equal(
                expectedSlots.OrderBy(slot => slot),
                pieces.Select(item => item.Slot!.Value).OrderBy(slot => slot));
            Assert.Equal(460, pieces.Sum(item => item.HonorPrice));

            Assert.All(pieces, item =>
            {
                Assert.Equal(ItemRarity.Legendary, item.Rarity);
                Assert.Equal(60, item.RequiredLevel);
                Assert.Equal(60, item.ItemLevelMin);
                Assert.Equal(60, item.ItemLevelMax);
                Assert.Equal(item.Id, item.ItemFamilyId);
                Assert.Equal("LEVEL_57_60", item.AffixCountProfileId);
                Assert.Equal(2, item.GuaranteedAffixStatIds?.Count ?? 0);
                Assert.Equal(ExpectedHonorPrices[item.Slot!.Value], item.HonorPrice);
                Assert.Equal("PVP_HONOR_BOUND", item.TradePolicyId);
                Assert.Single(item.AllowedClassIds!);
                Assert.Equal(classId, item.AllowedClassIds![0]);
            });

            EquipmentSetDefinition definition = Assert.Single(
                package.EquipmentSets!,
                set => set.Id == setId);
            Assert.Equal(
                ExpectedBonusThresholds,
                definition.Bonuses.Select(bonus => bonus.RequiredPieces).ToArray());
            Assert.Single(definition.AllowedClassIds!);
            Assert.Equal(classId, definition.AllowedClassIds![0]);
        }
    }

    [Fact]
    public async Task PvpT1KeepsPeerBaseBudgetWithPveT1WhileUsingSeparateSetDefinitions()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] pvpItems = package.Items!
            .Where(item => item.Id.StartsWith("L60_PVP_T1_", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(72, pvpItems.Length);

        foreach (ItemDefinition pvp in pvpItems)
        {
            string pveId = pvp.Id.Replace("L60_PVP_T1_", "L60_PVE_T1_", StringComparison.Ordinal);
            ItemDefinition pve = Assert.Single(package.Items!, item => item.Id == pveId);

            Assert.Equal(pve.Rarity, pvp.Rarity);
            Assert.Equal(pve.ArmorFlat, pvp.ArmorFlat);
            Assert.Equal(pve.ItemLevelMin, pvp.ItemLevelMin);
            Assert.Equal(pve.ItemLevelMax, pvp.ItemLevelMax);
            Assert.Equal(pve.RandomAffixPoolId, pvp.RandomAffixPoolId);
            Assert.Equal(pve.AffixCountProfileId, pvp.AffixCountProfileId);
            Assert.Equal(pve.ExtraAffixBudgetCap, pvp.ExtraAffixBudgetCap);
            Assert.NotEqual(pve.SetId, pvp.SetId);
        }
    }

    [Fact]
    public async Task PvpT1IsNotReferencedByAnyLootTable()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        string[] directLoot = package.LootTables!
            .SelectMany(table => table.Entries)
            .Select(entry => entry.ItemId)
            .Where(itemId => itemId.StartsWith("L60_PVP_T1_", StringComparison.Ordinal))
            .ToArray();
        string[] groupedLoot = package.LootTables!
            .SelectMany(table => table.SelectionGroups ?? [])
            .SelectMany(group => group.Entries)
            .Select(entry => entry.ItemId)
            .Where(itemId => itemId.StartsWith("L60_PVP_T1_", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(directLoot);
        Assert.Empty(groupedLoot);
    }

    [Fact]
    public async Task EachClassCanSeeAllThreeOfItsBranchSetsWithoutSpecRestriction()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] items = package.Items!
            .Where(item => item.Id.StartsWith("L60_PVP_T1_", StringComparison.Ordinal))
            .ToArray();

        foreach (string classId in new[] { "WARRIOR", "MAGE", "ARCHER", "PALADIN" })
        {
            ItemDefinition[] classItems = items
                .Where(item => item.AllowedClassIds is { Count: 1 }
                    && item.AllowedClassIds[0] == classId)
                .ToArray();

            Assert.Equal(18, classItems.Length);
            Assert.Equal(3, classItems.Select(item => item.SetId).Distinct(StringComparer.Ordinal).Count());
        }
    }
}
