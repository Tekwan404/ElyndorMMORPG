using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class LevelingItemFamilyContentTests
{
    private static readonly IReadOnlyDictionary<string, string> CanonicalSets =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SET_WARRIOR_GREY_FANG"] = "WARRIOR",
            ["SET_WARRIOR_CRIMSON_FURY"] = "WARRIOR",
            ["SET_WARRIOR_FIRST_GUARD"] = "WARRIOR",
            ["SET_WARRIOR_BLACK_BASTION"] = "WARRIOR",
            ["SET_MAGE_THREE_ELEMENTS"] = "MAGE",
            ["SET_MAGE_SHATTERED_STAR"] = "MAGE",
            ["SET_MAGE_SILENT_ARCHON"] = "MAGE",
            ["SET_MAGE_ECLIPSED_ORACLE"] = "MAGE",
            ["SET_ARCHER_THORN_TRAIL"] = "ARCHER",
            ["SET_ARCHER_MOONLEAF_SHADOW"] = "ARCHER",
            ["SET_ARCHER_STAR_HUNTER"] = "ARCHER",
            ["SET_ARCHER_BLACK_CONSTELLATION"] = "ARCHER",
            ["SET_PALADIN_LIVING_HEART"] = "PALADIN",
            ["SET_PALADIN_GROVE_DAWN"] = "PALADIN",
            ["SET_PALADIN_SHATTERED_DAWN"] = "PALADIN",
            ["SET_PALADIN_FIRST_GUARD"] = "PALADIN",
        };

    [Fact]
    public async Task CanonicalLevelingSetsAreSixPieceItemFamilies()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        foreach ((string setId, string classId) in CanonicalSets)
        {
            ItemDefinition[] pieces = package.Items!
                .Where(item => item.SetId == setId)
                .OrderBy(item => item.Slot)
                .ToArray();

            Assert.Equal(6, pieces.Length);
            Assert.Equal(
                [
                    EquipmentSlot.Head,
                    EquipmentSlot.Shoulders,
                    EquipmentSlot.Chest,
                    EquipmentSlot.Hands,
                    EquipmentSlot.Legs,
                    EquipmentSlot.Feet,
                ].OrderBy(slot => slot),
                pieces.Select(item => item.Slot!.Value).OrderBy(slot => slot));

            Assert.All(pieces, item =>
            {
                Assert.Equal(item.Id, item.ItemFamilyId);
                Assert.Equal([classId], item.AllowedClassIds);
                Assert.Equal(item.RequiredLevel, item.ItemLevelMin);
                Assert.True(item.ItemLevelMax >= item.ItemLevelMin, item.Id);
            });
        }
    }

    [Fact]
    public async Task LevelingCatalogHasTheTargetFamilyShapeAndNoCompatibilityItems()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] levelingEquipment = package.Items!
            .Where(item => item.Type == ItemType.Equipment && item.RequiredLevel <= 59)
            .ToArray();

        string[] families = levelingEquipment
            .Select(item => item.ItemFamilyId ?? item.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(286, families.Length);
        Assert.Equal(96, levelingEquipment.Count(item => CanonicalSets.ContainsKey(item.SetId ?? string.Empty)));
        Assert.Equal(190, levelingEquipment.Count(item => !CanonicalSets.ContainsKey(item.SetId ?? string.Empty)));

        string[] removedCompatibilityIds =
        [
            "WOLF_FANG_BLADE",
            "BOAR_HIDE_VEST",
            "SPIDER_SILK_HOOD",
        ];
        Assert.DoesNotContain(levelingEquipment, item => removedCompatibilityIds.Contains(item.Id));
        Assert.DoesNotContain(
            levelingEquipment,
            item => (item.Description ?? string.Empty).Contains("совместим", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(10, 13)]
    [InlineData(14, 17)]
    [InlineData(18, 22)]
    [InlineData(23, 59)]
    public async Task LevelingFamilyTierOwnsItsItemLevelWindow(int requiredLevel, int maximumItemLevel)
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] pieces = package.Items!
            .Where(item => CanonicalSets.ContainsKey(item.SetId ?? string.Empty)
                && item.RequiredLevel == requiredLevel)
            .ToArray();

        Assert.Equal(24, pieces.Length);
        Assert.All(pieces, item =>
        {
            Assert.Equal(requiredLevel, item.ItemLevelMin);
            Assert.Equal(maximumItemLevel, item.ItemLevelMax);
        });
    }
}
