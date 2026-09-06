using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class EquipmentStatModifierResolverTests
{
    [Fact]
    public void AggregatesPrimaryAndSecondaryItemAndSetModifiers()
    {
        ItemDefinition[] items =
        [
            Item(
                "WOLF_CHEST",
                EquipmentSlot.Chest,
                stats: new PrimaryStats(2, 0, 0, 4),
                setId: "WOLF_SET",
                attackPowerFlat: 5,
                armorFlat: 12,
                criticalChancePercent: 1),
            Item(
                "WOLF_HANDS",
                EquipmentSlot.Hands,
                stats: new PrimaryStats(1, 1, 0, 2),
                setId: "WOLF_SET",
                dodgePercent: 2,
                maxResourceFlat: 10)
        ];
        EquipmentSetDefinition[] sets =
        [
            new(
                "WOLF_SET",
                "Wolfguard",
                [
                    new EquipmentSetBonusDefinition(
                        RequiredPieces: 2,
                        AttackSpeedPercent: 3,
                        ArmorFlat: 8,
                        CriticalChancePercent: 2)
                ])
        ];

        EquipmentModifierSummary result =
            EquipmentStatModifierResolver.ResolveDetailed(items, sets);

        Assert.Equal(new PrimaryStats(3, 1, 0, 6), result.PrimaryStats);
        Assert.Equal(5, result.AttackPowerFlat);
        Assert.Equal(20, result.ArmorFlat);
        Assert.Equal(3, result.CriticalChancePercent);
        Assert.Equal(3, result.AttackSpeedPercent);
        Assert.Equal(2, result.DodgePercent);
        Assert.Equal(10, result.MaxResourceFlat);
        Assert.Single(result.ActiveSetBonuses);
    }

    [Fact]
    public void ResolvesMainHandWeaponDamageRange()
    {
        ItemDefinition weapon = new(
            "TEST_SWORD",
            "Test Sword",
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(1, 0, 0, 0),
            "Test",
            WeaponCategory: EquipmentCategoryIds.OneHandSword,
            WeaponDamageMin: 8,
            WeaponDamageMax: 12);

        EquipmentModifierSummary result =
            EquipmentStatModifierResolver.ResolveDetailed([weapon], []);

        Assert.Equal(8m, result.WeaponDamageMin);
        Assert.Equal(12m, result.WeaponDamageMax);
    }

    [Fact]
    public void ResolvesShieldBlockProfileFromOffHand()
    {
        ItemDefinition shield = new(
            "TEST_SHIELD",
            "Test Shield",
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.OffHand,
            new PrimaryStats(0, 0, 0, 1),
            "Test",
            ArmorFlat: 6,
            OffHandCategory: EquipmentCategoryIds.Shield,
            BlockChancePercent: 15,
            BlockValueMin: 3,
            BlockValueMax: 6);

        EquipmentModifierSummary result =
            EquipmentStatModifierResolver.ResolveDetailed([shield], []);

        Assert.Equal(6m, result.ArmorFlat);
        Assert.Equal(15m, result.BlockChancePercent);
        Assert.Equal(3m, result.BlockValueMin);
        Assert.Equal(6m, result.BlockValueMax);
    }

    private static ItemDefinition Item(
        string id,
        EquipmentSlot slot,
        PrimaryStats stats,
        string? setId = null,
        decimal attackPowerFlat = 0,
        decimal armorFlat = 0,
        decimal criticalChancePercent = 0,
        decimal dodgePercent = 0,
        decimal maxResourceFlat = 0) =>
        new(
            id,
            id,
            ItemType.Equipment,
            ItemRarity.Rare,
            RequiredLevel: 1,
            Stackable: false,
            MaxStack: 1,
            Slot: slot,
            Stats: stats,
            Description: id,
            SetId: setId,
            AttackPowerFlat: attackPowerFlat,
            ArmorFlat: armorFlat,
            CriticalChancePercent: criticalChancePercent,
            DodgePercent: dodgePercent,
            MaxResourceFlat: maxResourceFlat,
            ArmorCategory: slot is EquipmentSlot.Chest or EquipmentSlot.Hands
                ? EquipmentCategoryIds.Leather
                : null);
}
