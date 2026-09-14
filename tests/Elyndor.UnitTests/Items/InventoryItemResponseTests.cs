using Elyndor.Contracts.Items;

namespace Elyndor.UnitTests.Items;

public sealed class InventoryItemResponseTests
{
    [Fact]
    public void WeaponDamageRangeIsIncludedInStructuredResponseAndDescription()
    {
        InventoryItemResponse item = new(
            Id: Guid.NewGuid(),
            DefinitionId: "TEST_SWORD",
            Name: "Тестовый меч",
            Type: "Equipment",
            Rarity: "Rare",
            RequiredLevel: 10,
            Quantity: 1,
            Slot: "MainHand",
            EquippedSlot: null,
            Stats: new ItemStatsResponse(
                0, 0, 0, 0,
                0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0),
            Description: "Клинок для проверки.",
            SetId: null,
            WeaponCategory: "ONE_HAND_SWORD",
            ArmorCategory: null,
            AllowedClassIds: ["WARRIOR"],
            WeaponBaseAttackIntervalSeconds: 2m,
            AttackSpeedPercent: 0,
            DodgePercent: 0,
            ConsumableActions: [],
            ConsumableCooldownCategoryId: null,
            ConsumableCooldownSeconds: 0,
            BuyPriceGold: 0,
            SellPriceGold: 0,
            IsLocked: false,
            WeaponDamageMin: 12.5m,
            WeaponDamageMax: 18.75m);

        Assert.Equal(12.5m, item.WeaponDamageMin);
        Assert.Equal(18.75m, item.WeaponDamageMax);
        Assert.Contains("Урон оружия: 12.5–18.75.", item.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void WeaponAndBlockPresentationCanBeShownTogether()
    {
        InventoryItemResponse item = new(
            Id: Guid.NewGuid(),
            DefinitionId: "TEST_SHIELD_WEAPON",
            Name: "Тестовый предмет",
            Type: "Equipment",
            Rarity: "Epic",
            RequiredLevel: 10,
            Quantity: 1,
            Slot: "MainHand",
            EquippedSlot: null,
            Stats: new ItemStatsResponse(
                0, 0, 0, 0,
                0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0),
            Description: "Проверка нескольких боевых параметров.",
            SetId: null,
            WeaponCategory: "ONE_HAND_SWORD",
            ArmorCategory: null,
            AllowedClassIds: ["WARRIOR"],
            WeaponBaseAttackIntervalSeconds: 2m,
            AttackSpeedPercent: 0,
            DodgePercent: 0,
            ConsumableActions: [],
            ConsumableCooldownCategoryId: null,
            ConsumableCooldownSeconds: 0,
            BuyPriceGold: 0,
            SellPriceGold: 0,
            IsLocked: false,
            BlockChancePercent: 15,
            BlockValueMin: 20,
            BlockValueMax: 30,
            WeaponDamageMin: 10,
            WeaponDamageMax: 16);

        Assert.Contains("Урон оружия: 10–16.", item.Description, StringComparison.Ordinal);
        Assert.Contains("Шанс блока: 15%. Сила блока: 20–30.", item.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvedItemLevelIsSeparateFromRequiredLevel()
    {
        GeneratedItemSummaryResponse generated = new(
            ItemLevel: 24,
            ItemPower: 188,
            MaxItemPower: 200,
            RollQuality: 94,
            Stars: 4,
            IsPerfect: false,
            PerfectOrigin: null,
            GeneratedPrefixId: null,
            GeneratedSuffixId: null,
            DisplayName: "Оплот Хранителя Глубин",
            Affixes: []);

        InventoryItemResponse item = new(
            Id: Guid.NewGuid(),
            DefinitionId: "GUARDIAN_SHIELD",
            Name: "Оплот Хранителя Глубин",
            Type: "Equipment",
            Rarity: "Legendary",
            RequiredLevel: 16,
            Quantity: 1,
            Slot: "OffHand",
            EquippedSlot: "OffHand",
            Stats: new ItemStatsResponse(
                0, 0, 0, 18,
                0, 0, 0, 0, 0, 0,
                148, 0, 0, 0, 0, 0, 0),
            Description: "Щит Хранителя.",
            SetId: null,
            WeaponCategory: null,
            ArmorCategory: "HEAVY",
            AllowedClassIds: ["WARRIOR"],
            WeaponBaseAttackIntervalSeconds: null,
            AttackSpeedPercent: 0,
            DodgePercent: 0,
            ConsumableActions: [],
            ConsumableCooldownCategoryId: null,
            ConsumableCooldownSeconds: 0,
            BuyPriceGold: 0,
            SellPriceGold: 0,
            IsLocked: false,
            GeneratedItem: generated);

        Assert.Equal(16, item.RequiredLevel);
        Assert.Equal(24, item.ItemLevel);
        Assert.NotEqual(item.RequiredLevel, item.ItemLevel);
    }
}
