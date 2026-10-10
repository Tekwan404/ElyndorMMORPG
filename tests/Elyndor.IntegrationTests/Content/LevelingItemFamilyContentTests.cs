using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class LevelingItemFamilyContentTests
{
    private static readonly EquipmentSlot[] AdditionalSlots = [EquipmentSlot.Shoulders, EquipmentSlot.Feet];

    private static readonly Dictionary<string, string> CanonicalSets =
        new(StringComparer.Ordinal)
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
    public async Task CanonicalLevelingSetsUseProgressiveFourAndSixPieceTiers()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        foreach ((string setId, string classId) in CanonicalSets)
        {
            ItemDefinition[] pieces = package.Items!
                .Where(item => item.SetId == setId)
                .OrderBy(item => item.Slot)
                .ToArray();

            int level = pieces.First().RequiredLevel;
            Assert.True(level is 18 or 35 or 45 or 55);
            Assert.Equal(level == 18 ? 4 : 6, pieces.Length);
            EquipmentSlot[] expectedSlots =
            [
                EquipmentSlot.Head,

                EquipmentSlot.Chest,
                EquipmentSlot.Hands,
                EquipmentSlot.Legs,

            ];

            Assert.Equal(
                (level == 18 ? expectedSlots : expectedSlots.Concat(AdditionalSlots)).OrderBy(slot => slot),
                pieces.Select(item => item.Slot!.Value).OrderBy(slot => slot));

            Assert.All(pieces, item =>
            {
                Assert.Equal(item.Id, item.ItemFamilyId);
                Assert.Equal([classId], item.AllowedClassIds);
                Assert.Equal(item.RequiredLevel, item.ItemLevelMin);
                Assert.True(item.ItemLevelMax >= item.ItemLevelMin, item.Id);

                ItemAffixCountProfileDefinition profile = package.Itemization!.AffixCountProfiles
                    .Single(candidate => candidate.Id == item.AffixCountProfileId);
                Assert.Equal(
                    profile.GuaranteedCount,
                    item.GuaranteedAffixStatIds?.Count ?? 0);
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

        Assert.Equal(358, families.Length);
        Assert.Equal(88, levelingEquipment.Count(item => CanonicalSets.ContainsKey(item.SetId ?? string.Empty)));
        Assert.Equal(270, levelingEquipment.Count(item => !CanonicalSets.ContainsKey(item.SetId ?? string.Empty)));

        string[] removedCompatibilityIds =
        [
            "WOLF_FANG_BLADE",
            "BOAR_HIDE_VEST",
            "SPIDER_SILK_HOOD",
        ];
        Assert.DoesNotContain(levelingEquipment, item => removedCompatibilityIds.Contains(item.Id));
        Assert.DoesNotContain(
            levelingEquipment,
            item => (item.Description ?? string.Empty).Contains("СЃРѕРІРјРµСЃС‚РёРј", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(18, 29)]
    [InlineData(35, 44)]
    [InlineData(45, 54)]
    [InlineData(55, 59)]
    public async Task LevelingFamilyTierOwnsItsItemLevelWindow(int requiredLevel, int maximumItemLevel)
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition[] pieces = package.Items!
            .Where(item => CanonicalSets.ContainsKey(item.SetId ?? string.Empty)
                && item.RequiredLevel == requiredLevel)
            .ToArray();

        Assert.Equal(requiredLevel == 18 ? 16 : 24, pieces.Length);
        Assert.All(pieces, item =>
        {
            Assert.Equal(requiredLevel, item.ItemLevelMin);
            Assert.Equal(maximumItemLevel, item.ItemLevelMax);
        });
    }
}
