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
    public async Task PveT1HasTwelveCompleteLegendaryIlvl65Sets()
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
                Assert.Equal(65, item.ItemLevelMin);
                Assert.Equal(65, item.ItemLevelMax);
                Assert.Equal(60, ItemRequiredLevelPolicy.Resolve(item, 65, 60));
                Assert.Equal(item.Id, item.ItemFamilyId);
                Assert.Equal("LEVEL_57_60", item.AffixCountProfileId);
                Assert.Equal(2, item.GuaranteedAffixStatIds?.Count ?? 0);
                Assert.Single(item.AllowedClassIds!);
                Assert.Equal(classId, item.AllowedClassIds![0]);
                if (classId == "PALADIN")
                    Assert.Equal("PALADIN_ARMOR", item.RandomAffixPoolId);
            });

            EquipmentSetDefinition definition = Assert.Single(
                package.EquipmentSets!,
                set => set.Id == setId);
            Assert.Equal(3, definition.Bonuses.Count);
            Assert.Equal(
                ExpectedBonusThresholds,
                definition.Bonuses.Select(bonus => bonus.RequiredPieces).ToArray());
            Assert.Single(definition.AllowedClassIds!);
            Assert.Equal(classId, definition.AllowedClassIds![0]);
        }
    }

    [Fact]
    public async Task TopDeadReachesBossesKeepNormalDropAndAddRarePveT1Drops()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        LootTableDefinition table = Assert.Single(
            package.LootTables!,
            candidate => candidate.Id == "DEAD_REACHES_L60_PVE_T1_BOSS_LOOT");

        Assert.Equal(72, table.Entries.Count);
        Assert.All(table.Entries, entry =>
        {
            Assert.Equal(0.0015m, entry.DropChance);
            Assert.Equal(65, entry.ItemLevelMin);
            Assert.Equal(65, entry.ItemLevelMax);
        });

        string[] familyIds = package.Items!
            .Where(item => item.SetId is not null && ExpectedSets.ContainsKey(item.SetId))
            .Select(item => item.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            familyIds,
            table.Entries.Select(entry => entry.ItemId).OrderBy(id => id, StringComparer.Ordinal).ToArray());

        LootSelectionGroup normalGroup = Assert.Single(table.SelectionGroups!);
        Assert.Equal("NORMAL_L60_SET_PIECE", normalGroup.Id);
        Assert.Equal(72, normalGroup.Entries.Count);
        Assert.All(normalGroup.Entries, entry =>
        {
            Assert.Equal(60, entry.ItemLevelMin);
            Assert.Equal(60, entry.ItemLevelMax);
        });

        string[] bossIds =
        [
            "DEAD_REACHES_MORDREK_LAST_GATE_L60",
            "DEAD_REACHES_NAMELESS_KING_L60",
            "DEAD_REACHES_NERZAR_L60",
        ];
        foreach (string bossId in bossIds)
        {
            Assert.Equal(
                "DEAD_REACHES_L60_PVE_T1_BOSS_LOOT",
                package.Monsters!.Single(monster => monster.Id == bossId).LootTableId);
        }
    }
}
