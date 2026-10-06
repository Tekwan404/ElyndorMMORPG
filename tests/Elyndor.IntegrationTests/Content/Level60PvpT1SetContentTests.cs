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

    private static readonly Dictionary<EquipmentSlot, long> HonorPrices = new()
    {
        [EquipmentSlot.Chest] = 120,
        [EquipmentSlot.Legs] = 110,
        [EquipmentSlot.Head] = 90,
        [EquipmentSlot.Shoulders] = 80,
        [EquipmentSlot.Hands] = 70,
        [EquipmentSlot.Feet] = 70,
    };

    [Fact]
    public async Task PvpT1HasTwelveCompleteHonorBoundBranchSets()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] items = package.Items!
            .Where(item => item.SetId is not null && ExpectedSets.ContainsKey(item.SetId))
            .ToArray();

        Assert.Equal(72, items.Length);

        foreach ((string setId, string classId) in ExpectedSets)
        {
            ItemDefinition[] pieces = items.Where(item => item.SetId == setId).ToArray();
            Assert.Equal(6, pieces.Length);
            Assert.Equal(
                HonorPrices.Keys.OrderBy(slot => slot),
                pieces.Select(item => item.Slot!.Value).OrderBy(slot => slot));
            Assert.Equal(540, pieces.Sum(item => item.HonorPrice));

            Assert.All(pieces, item =>
            {
                Assert.Equal(ItemRarity.Legendary, item.Rarity);
                Assert.Equal(60, item.RequiredLevel);
                Assert.Equal(60, item.ItemLevelMin);
                Assert.Equal(60, item.ItemLevelMax);
                Assert.Equal(item.Id, item.ItemFamilyId);
                Assert.Equal("LEVEL_57_60", item.AffixCountProfileId);
                Assert.Equal(2, item.GuaranteedAffixStatIds?.Count ?? 0);
                Assert.Single(item.AllowedClassIds!);
                Assert.Equal(classId, item.AllowedClassIds![0]);
                Assert.Equal(HonorPrices[item.Slot!.Value], item.HonorPrice);
                Assert.Equal("ARENA_BOUND", item.TradePolicyId);
                Assert.False(item.PremiumEligible);
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
    public async Task PvpAndPveT1HaveEqualRawArmorPower()
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

            Assert.Equal(pve.ArmorFlat, pvp.ArmorFlat);
            Assert.Equal(pve.Stats, pvp.Stats);
            Assert.Equal(ItemRarity.Legendary, pve.Rarity);
            Assert.Equal(ItemRarity.Legendary, pvp.Rarity);
        }
    }

    [Fact]
    public async Task PvpT1IsHonorOnlyAndNeverAppearsInLoot()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        HashSet<string> lootItemIds = package.LootTables!
            .SelectMany(table => table.Entries.Select(entry => entry.ItemId)
                .Concat((table.SelectionGroups ?? []).SelectMany(group => group.Entries)
                    .Select(entry => entry.ItemId)))
            .ToHashSet(StringComparer.Ordinal);

        ItemDefinition[] pvpItems = package.Items!
            .Where(item => item.Id.StartsWith("L60_PVP_T1_", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(72, pvpItems.Length);
        Assert.All(pvpItems, item =>
        {
            Assert.True(item.HonorPrice > 0, item.Id);
            Assert.DoesNotContain(item.Id, lootItemIds);
        });
    }
}
