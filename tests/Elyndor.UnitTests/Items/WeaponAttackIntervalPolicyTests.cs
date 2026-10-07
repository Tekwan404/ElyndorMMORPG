using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class WeaponAttackIntervalPolicyTests
{
    [Fact]
    public void WeaponFamiliesHaveDistinctDefaultSwingSpeeds()
    {
        Assert.Equal(1.6m, Resolve(EquipmentCategoryIds.Dagger));
        Assert.Equal(1.8m, Resolve(EquipmentCategoryIds.OneHandSword));
        Assert.Equal(2.0m, Resolve(EquipmentCategoryIds.Axe));
        Assert.Equal(2.1m, Resolve(EquipmentCategoryIds.Mace));
        Assert.Equal(2.6m, Resolve(EquipmentCategoryIds.Bow));
        Assert.Equal(2.8m, Resolve(EquipmentCategoryIds.Staff));
        Assert.Equal(2.9m, Resolve(EquipmentCategoryIds.TwoHandSword));
        Assert.Equal(3.0m, Resolve(EquipmentCategoryIds.TwoHandAxe));
        Assert.Equal(3.1m, Resolve(EquipmentCategoryIds.TwoHandMace));
    }

    [Fact]
    public void AuthoredWeaponSpeedOverridesFamilyDefault()
    {
        decimal resolved = WeaponAttackIntervalPolicy.ResolveSeconds(
            EquipmentCategoryIds.TwoHandMace,
            authoredIntervalSeconds: 2.75m,
            fallbackIntervalSeconds: 2m);

        Assert.Equal(2.75m, resolved);
    }

    [Fact]
    public void UnknownOrUnarmedCategoryKeepsClassFallback()
    {
        Assert.Equal(
            2.15m,
            WeaponAttackIntervalPolicy.ResolveSeconds(
                weaponCategory: null,
                authoredIntervalSeconds: null,
                fallbackIntervalSeconds: 2.15m));
    }

    private static decimal Resolve(string category) =>
        WeaponAttackIntervalPolicy.ResolveSeconds(
            category,
            authoredIntervalSeconds: null,
            fallbackIntervalSeconds: 2m);
}
