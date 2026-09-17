using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class EquipmentCategoryIdsTests
{
    [Theory]
    [InlineData("TWO_HAND_AXE")]
    [InlineData("TWO_HAND_MACE")]
    [InlineData("POLEARM")]
    public void AuthoredTwoHandedWeaponCategoriesAreRecognized(string category)
    {
        Assert.True(EquipmentCategoryIds.IsWeapon(category));
        Assert.True(EquipmentCategoryIds.UsesBothHands(category));
        Assert.False(EquipmentCategoryIds.IsOneHandedWeapon(category));
    }
}
