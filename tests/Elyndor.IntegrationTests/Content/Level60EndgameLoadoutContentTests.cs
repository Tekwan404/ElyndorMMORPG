using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class Level60EndgameLoadoutContentTests
{
    [Fact]
    public async Task NormalLevel60FillerGearClosesTheNonSetLoadoutSlots()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] fillers = package.Items!
            .Where(item => item.Id.StartsWith("L60_NORMAL_", StringComparison.Ordinal)
                && item.SetId is null)
            .ToArray();

        Assert.Equal(28, fillers.Length);
        Assert.Equal(12, fillers.Count(item => item.Slot == EquipmentSlot.MainHand));
        Assert.Equal(4, fillers.Count(item => item.Slot == EquipmentSlot.OffHand));
        Assert.Equal(4, fillers.Count(item => item.Slot == EquipmentSlot.Cloak));
        Assert.Equal(4, fillers.Count(item => item.Slot == EquipmentSlot.Amulet));
        Assert.Equal(4, fillers.Count(item => item.Slot == EquipmentSlot.Ring1));

        Assert.All(fillers, item =>
        {
            Assert.Equal(ItemRarity.Epic, item.Rarity);
            Assert.Equal(60, item.RequiredLevel);
            Assert.Equal(60, item.ItemLevelMin);
            Assert.Equal(60, item.ItemLevelMax);
            Assert.Equal(item.Id, item.ItemFamilyId);
            Assert.Equal("LEVEL_57_60", item.AffixCountProfileId);
            Assert.Single(item.AllowedClassIds!);
        });

        string[] classes = ["WARRIOR", "MAGE", "ARCHER", "PALADIN"];
        foreach (string classId in classes)
        {
            ItemDefinition[] classFillers = fillers
                .Where(item => item.AllowedClassIds![0] == classId)
                .ToArray();

            Assert.Contains(classFillers, item => item.Slot == EquipmentSlot.Cloak);
            Assert.Contains(classFillers, item => item.Slot == EquipmentSlot.Amulet);
            Assert.Contains(classFillers, item => item.Slot == EquipmentSlot.Ring1);
            Assert.Contains(classFillers, item => item.Slot == EquipmentSlot.OffHand);
            Assert.Equal(3, classFillers.Count(item => item.Slot == EquipmentSlot.MainHand));
        }

        ItemDefinition warriorShield = Assert.Single(
            fillers,
            item => item.Id == "L60_NORMAL_WARRIOR_SHIELD");
        Assert.Equal("SHIELD", warriorShield.OffHandCategory);
        Assert.Equal("WARRIOR_SHIELD", warriorShield.RandomAffixPoolId);

        ItemDefinition paladinShield = Assert.Single(
            fillers,
            item => item.Id == "L60_NORMAL_PALADIN_SHIELD");
        Assert.Equal("SHIELD", paladinShield.OffHandCategory);
        Assert.Equal("PALADIN_SHIELD", paladinShield.RandomAffixPoolId);

        ItemDefinition mageFocus = Assert.Single(
            fillers,
            item => item.Id == "L60_NORMAL_MAGE_FOCUS");
        Assert.Equal("FOCUS", mageFocus.OffHandCategory);
        Assert.Equal("MAGE_FOCUS", mageFocus.RandomAffixPoolId);

        ItemDefinition archerQuiver = Assert.Single(
            fillers,
            item => item.Id == "L60_NORMAL_ARCHER_QUIVER");
        Assert.Equal("QUIVER", archerQuiver.OffHandCategory);
        Assert.Equal("ARCHER_QUIVER", archerQuiver.RandomAffixPoolId);
    }

    [Fact]
    public async Task PaladinAndClassAccessoriesUseFocusedAffixPools()
    {
        var package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] paladinWeapons = package.Items!
            .Where(item => item.Id.StartsWith("L60_NORMAL_PALADIN_", StringComparison.Ordinal)
                && item.Slot == EquipmentSlot.MainHand)
            .Concat(package.Items!.Where(item =>
                item.Id.StartsWith("UNIQUE_L60_PALADIN_", StringComparison.Ordinal)
                && item.Slot == EquipmentSlot.MainHand))
            .ToArray();

        Assert.Equal(6, paladinWeapons.Length);
        Assert.All(paladinWeapons, item => Assert.Equal(
            item.Id.Contains("PALADIN_HOLY", StringComparison.Ordinal) ? "PALADIN_HOLY"
                : item.Id.StartsWith("L60_NORMAL_PALADIN_PROTECTION_", StringComparison.Ordinal) ? "PALADIN_PROTECTION"
                : "PALADIN_WEAPON", item.RandomAffixPoolId));

        Dictionary<string, string> expectedAccessoryPools = new(StringComparer.Ordinal)
        {
            ["WARRIOR"] = "WARRIOR_ACCESSORY",
            ["MAGE"] = "MAGE_ACCESSORY",
            ["ARCHER"] = "ARCHER_ACCESSORY",
            ["PALADIN"] = "PALADIN_ACCESSORY",
        };

        ItemDefinition[] classAccessories = package.Items!
            .Where(item =>
                (item.Id.StartsWith("L60_NORMAL_", StringComparison.Ordinal)
                    || item.Id.StartsWith("UNIQUE_L60_", StringComparison.Ordinal))
                && item.Slot is EquipmentSlot.Ring1 or EquipmentSlot.Cloak or EquipmentSlot.Amulet)
            .ToArray();

        Assert.All(classAccessories, item =>
        {
            string classId = Assert.Single(item.AllowedClassIds!);
            string expected = item.Id.Contains("PALADIN_HOLY", StringComparison.Ordinal) ? "PALADIN_HOLY"
                : item.Id.StartsWith("L60_NORMAL_WARRIOR_GUARDIAN_", StringComparison.Ordinal) ? "WARRIOR_GUARDIAN"
                : item.Id.StartsWith("L60_NORMAL_PALADIN_PROTECTION_", StringComparison.Ordinal) ? "PALADIN_PROTECTION"
                : item.Id.StartsWith("L60_NORMAL_ARCHER_BEAST_MASTERY_", StringComparison.Ordinal) ? "ARCHER_BEAST_MASTERY"
                : expectedAccessoryPools[classId];
            Assert.Equal(expected, item.RandomAffixPoolId);
        });
    }
}
