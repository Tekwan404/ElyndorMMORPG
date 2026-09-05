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
                ? EquipmentCategoryIds.Medium
                : null);
}
