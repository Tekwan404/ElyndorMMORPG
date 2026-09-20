using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class ItemEnhancementV2Tests
{
    [Fact]
    public void EnhancementScalesOnlyStructuralStats()
    {
        ItemDefinition item = new(
            "TEST_SWORD",
            "Test Sword",
            ItemType.Equipment,
            ItemRarity.Rare,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(5, 0, 0, 0),
            "",
            WeaponCategory: EquipmentCategoryIds.OneHandSword,
            ArmorFlat: 100m,
            CriticalChancePercent: 7m,
            WeaponDamageMin: 10m,
            WeaponDamageMax: 20m);

        ItemDefinition enhanced = ItemEnhancementRules.ApplyStructuralEnhancement(item, 5);

        Assert.Equal(110m, enhanced.ArmorFlat);
        Assert.Equal(11m, enhanced.WeaponDamageMin);
        Assert.Equal(22m, enhanced.WeaponDamageMax);
        Assert.Equal(5m, enhanced.Stats.Strength);
        Assert.Equal(7m, enhanced.CriticalChancePercent);
    }

    [Fact]
    public void EnhancementScalesSpellPowerOnlyForMagicStructuralGear()
    {
        ItemDefinition staff = new(
            "TEST_STAFF",
            "Test Staff",
            ItemType.Equipment,
            ItemRarity.Rare,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(0, 0, 3, 0),
            "",
            WeaponCategory: EquipmentCategoryIds.Staff,
            SpellPowerFlat: 50m);
        ItemDefinition sword = staff with
        {
            Id = "TEST_NON_MAGIC",
            WeaponCategory = EquipmentCategoryIds.OneHandSword
        };

        Assert.Equal(55m, ItemEnhancementRules.ApplyStructuralEnhancement(staff, 5).SpellPowerFlat);
        Assert.Equal(50m, ItemEnhancementRules.ApplyStructuralEnhancement(sword, 5).SpellPowerFlat);
    }

    [Fact]
    public void EnhancementNeverChangesBirthMetadata()
    {
        Guid itemId = Guid.NewGuid();
        CharacterItem item = new(
            itemId,
            Guid.NewGuid(),
            "TEST_ITEM",
            1,
            DateTimeOffset.UtcNow,
            1);
        GeneratedItemAffix affix = new(
            "AFFIX_1",
            "STRENGTH",
            ItemStatIds.Strength,
            7m,
            2m,
            10m,
            1m,
            1,
            false,
            false,
            0);
        GeneratedItemInstance generated = new(
            12,
            [affix],
            100m,
            150m,
            180m,
            62.5m,
            3,
            false,
            null,
            "PREFIX_STRENGTH",
            null,
            "Mighty Test Item",
            1);
        item.ApplyGeneratedInstance(
            generated,
            "HASH",
            "DROP",
            Guid.NewGuid(),
            "TEST_SOURCE");

        item.ApplyEnhancement(1);

        Assert.Equal(1, item.EnhancementLevel);
        Assert.Equal(generated.ItemLevel, item.ItemLevel);
        Assert.Equal(generated.ActualItemPower, item.ActualItemPower);
        Assert.Equal(generated.RollQuality, item.RollQuality);
        Assert.Equal(generated.Stars, item.Stars);
        Assert.Equal(generated.IsPerfect, item.IsPerfect);
        Assert.Equal(generated.PerfectOrigin, item.PerfectOrigin);
        Assert.Equal(generated.GeneratedPrefixId, item.GeneratedPrefixId);
        Assert.Equal(generated.GeneratedSuffixId, item.GeneratedSuffixId);
        Assert.Equal(generated.DisplayName, item.GeneratedDisplayName);
        Assert.Single(item.Affixes);
        Assert.Equal(affix.Value, item.Affixes.Single().Value);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 0)]
    [InlineData(2, 2, 0)]
    [InlineData(3, 5, 0)]
    [InlineData(4, 11, 0)]
    [InlineData(5, 18, 1)]
    public void SalvageRefundReturnsDeterministicPartialEnhancementInvestment(
        int enhancementLevel,
        int expectedOre,
        int expectedCatalyst)
    {
        ItemStarUpgradeProfileDefinition profile = CreateEnhancementProfile();

        ItemEnhancementSalvageRefund first = ItemEnhancementSalvageRefundRules.Resolve(profile, enhancementLevel);
        ItemEnhancementSalvageRefund replay = ItemEnhancementSalvageRefundRules.Resolve(profile, enhancementLevel);

        Assert.Equal(first, replay);
        Assert.Equal(expectedOre, first.EnhancementMaterialQuantity);
        Assert.Equal(expectedOre > 0 ? ItemEnhancementCostRules.EnhancementMaterialItemId : null, first.EnhancementMaterialItemId);
        Assert.Equal(expectedCatalyst, first.CatalystQuantity);
        Assert.Equal(expectedCatalyst > 0 ? "DUNGEON_CATALYST" : null, first.CatalystItemId);
    }

    [Fact]
    public void ReforgeStaysNearReplacedAffixQualityAndCannotCreateMaximumRoll()
    {
        GeneratedItemAffix previous = new(
            "AFFIX_1",
            "STRENGTH",
            ItemStatIds.Strength,
            65m,
            0m,
            100m,
            1m,
            1,
            false,
            false,
            0);
        GeneratedItemAffix candidate = new(
            "AFFIX_1",
            "AGILITY",
            ItemStatIds.Agility,
            100m,
            0m,
            100m,
            1m,
            1,
            false,
            true,
            0);

        GeneratedItemAffix reforged = ItemReforgeQualityPolicy.Constrain(
            previous,
            candidate,
            new SeededGameRandom(12345));
        decimal quality = ItemReforgeQualityPolicy.Normalize(
            reforged.Value,
            reforged.MinAtGeneration,
            reforged.MaxAtGeneration);

        Assert.InRange(quality, 0.54m, 0.76m);
        Assert.True(reforged.Value < reforged.MaxAtGeneration);
        Assert.True(reforged.IsReforgeSlot);
    }

    private static ItemStarUpgradeProfileDefinition CreateEnhancementProfile() => new(
        "STAR_UPGRADE_V1",
        "REFORGE_STONE",
        new Dictionary<int, int>
        {
            [2] = 50,
            [3] = 100,
            [4] = 180,
            [5] = 300
        },
        new Dictionary<int, int>
        {
            [2] = 2,
            [3] = 4,
            [4] = 7,
            [5] = 12
        },
        "DUNGEON_CATALYST",
        1);
}
