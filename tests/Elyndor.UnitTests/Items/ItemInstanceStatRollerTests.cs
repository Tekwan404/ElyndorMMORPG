using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class ItemInstanceStatRollerTests
{
    [Fact]
    public void ResolveRollsConfiguredRangesAndKeepsStaticFallbacks()
    {
        ItemDefinition definition = new(
            "TEST_ROLLING_SWORD",
            "Rolling Sword",
            ItemType.Equipment,
            ItemRarity.Rare,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(1, 2, 7, 4),
            "Test rolling weapon.",
            WeaponCategory: EquipmentCategoryIds.OneHandSword,
            PrimaryStatRanges: new PrimaryStatRanges(
                Strength: new ItemStatRange(2, 4),
                Agility: new ItemStatRange(10, 12),
                Stamina: new ItemStatRange(4, 8, 2)));

        PrimaryStats rolled = ItemInstanceStatRoller.Resolve(
            definition,
            new SequenceGameRandom(0m, 0.999m, 0.5m));

        Assert.Equal(2, rolled.Strength);
        Assert.Equal(12, rolled.Agility);
        Assert.Equal(7, rolled.Intellect);
        Assert.Equal(6, rolled.Stamina);
    }

    [Fact]
    public void GeneratedWeaponDamageAffixChangesEffectiveWeaponDamageRange()
    {
        ItemDefinition definition = new(
            "TEST_PROCEDURAL_BOW",
            "Procedural Bow",
            ItemType.Equipment,
            ItemRarity.Rare,
            10,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(0, 0, 0, 0),
            "Procedural weapon.",
            WeaponCategory: EquipmentCategoryIds.Bow,
            WeaponDamageMin: 20,
            WeaponDamageMax: 30);

        ItemDefinition effective = ItemInstanceGenerator.ApplyGeneratedAffixes(
            definition,
            [
                new GeneratedItemAffix(
                    "BONUS_0",
                    "WEAPON_DAMAGE",
                    ItemStatIds.WeaponDamage,
                    5,
                    1,
                    10,
                    1,
                    1,
                    false,
                    false,
                    0)
            ]);

        Assert.Equal(25, effective.WeaponDamageMin);
        Assert.Equal(35, effective.WeaponDamageMax);
    }

    [Fact]
    public void ResolveKeepsLegacyStaticStatsWhenNoRangesExist()
    {
        ItemDefinition definition = new(
            "TEST_STATIC_STAFF",
            "Static Staff",
            ItemType.Equipment,
            ItemRarity.Common,
            1,
            false,
            1,
            EquipmentSlot.MainHand,
            new PrimaryStats(0, 0, 5, 2),
            "Legacy compatible static item.",
            WeaponCategory: EquipmentCategoryIds.Staff);

        PrimaryStats resolved = ItemInstanceStatRoller.Resolve(
            definition,
            new SequenceGameRandom());

        Assert.Equal(definition.Stats, resolved);
    }
}
