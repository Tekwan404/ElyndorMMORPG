using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class Level60PveT1SetContentTests
{
    private static readonly int[] ExpectedBonusThresholds = [2, 4, 6];

    private static readonly Dictionary<string, string> ExpectedSets = new(StringComparer.Ordinal)
    {
        ["SET_L60_PVE_T1_WARRIOR_GUARDIAN"] = "WARRIOR",
        ["SET_L60_PVE_T1_WARRIOR_BERSERKER"] = "WARRIOR",
        ["SET_L60_PVE_T1_WARRIOR_WARLORD"] = "WARRIOR",
        ["SET_L60_PVE_T1_MAGE_FIRE"] = "MAGE",
        ["SET_L60_PVE_T1_MAGE_ARCANE"] = "MAGE",
        ["SET_L60_PVE_T1_MAGE_FROST"] = "MAGE",
        ["SET_L60_PVE_T1_ARCHER_MARKSMAN"] = "ARCHER",
        ["SET_L60_PVE_T1_ARCHER_BEAST_MASTERY"] = "ARCHER",
        ["SET_L60_PVE_T1_ARCHER_SURVIVAL"] = "ARCHER",
        ["SET_L60_PVE_T1_PALADIN_HOLY"] = "PALADIN",
        ["SET_L60_PVE_T1_PALADIN_PROTECTION"] = "PALADIN",
        ["SET_L60_PVE_T1_PALADIN_RETRIBUTION"] = "PALADIN",
    };

    [Fact]
    public async Task PveT1HasTwelveCompleteLegendaryBranchSets()
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
    public async Task EveryPveT1PieceIsStrongerThanItsNormalTierCounterpart()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] t1Items = package.Items!
            .Where(item => item.Id.StartsWith("L60_PVE_T1_", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(72, t1Items.Length);

        foreach (ItemDefinition t1 in t1Items)
        {
            string normalId = t1.Id.Replace("L60_PVE_T1_", "L60_NORMAL_", StringComparison.Ordinal);
            ItemDefinition normal = Assert.Single(package.Items!, item => item.Id == normalId);

            Assert.True(t1.ArmorFlat > normal.ArmorFlat, t1.Id);
            Assert.Equal(ItemRarity.Epic, normal.Rarity);
            Assert.Equal(ItemRarity.Legendary, t1.Rarity);
        }
    }

    [Fact]
    public async Task DeadReachesCapstoneElitesOwnThePveT1LootSource()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        LootTableDefinition normalTable = Assert.Single(
            package.LootTables!,
            candidate => candidate.Id == "DEAD_REACHES_L60_NORMAL_SET_LOOT");
        LootTableDefinition t1Table = Assert.Single(
            package.LootTables!,
            candidate => candidate.Id == "DEAD_REACHES_L60_PVE_T1_LOOT");

        Assert.DoesNotContain(
            normalTable.Entries,
            entry => entry.ItemId.StartsWith("L60_PVE_T1_", StringComparison.Ordinal));
        Assert.DoesNotContain(
            normalTable.SelectionGroups!.SelectMany(group => group.Entries),
            entry => entry.ItemId.StartsWith("L60_PVE_T1_", StringComparison.Ordinal));

        Assert.Empty(t1Table.SelectionGroups ?? []);
        LootTableEntry[] t1Entries = t1Table.Entries
            .Where(entry => entry.ItemId.StartsWith("L60_PVE_T1_", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(72, t1Entries.Length);
        Assert.All(t1Entries, entry =>
        {
            Assert.Equal(0.002m, entry.DropChance);
            Assert.Equal(1, entry.MinQuantity);
            Assert.Equal(1, entry.MaxQuantity);
            Assert.Equal(60, entry.ItemLevelMin);
            Assert.Equal(60, entry.ItemLevelMax);
        });

        string[] expectedIds = package.Items!
            .Where(item => item.Id.StartsWith("L60_PVE_T1_", StringComparison.Ordinal))
            .Select(item => item.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedIds,
            t1Entries.Select(entry => entry.ItemId).OrderBy(id => id, StringComparer.Ordinal).ToArray());

        string[] capstoneEliteIds =
        [
            "DEAD_REACHES_MORDREK_LAST_GATE_L60",
            "DEAD_REACHES_NAMELESS_KING_L60",
            "DEAD_REACHES_NERZAR_L60",
        ];

        foreach (string eliteId in capstoneEliteIds)
        {
            Assert.Equal(
                "DEAD_REACHES_L60_PVE_T1_LOOT",
                package.Monsters!.Single(monster => monster.Id == eliteId).LootTableId);
        }
    }}
}
