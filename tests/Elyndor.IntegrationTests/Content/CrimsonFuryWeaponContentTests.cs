using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Content;

namespace Elyndor.IntegrationTests.Content;

public sealed class CrimsonFuryWeaponContentTests
{
    [Fact]
    public async Task CrimsonOathBladeIsOneHandedWithoutChangingItsProgressionStats()
    {
        GameContentPackage package = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        ItemDefinition weapon = Assert.Single(
            package.Items!,
            item => item.Id == "WARRIOR_EPIC_CRIMSON_FURY_WEAPON");

        Assert.Equal("MainHand", weapon.Slot);
        Assert.Equal("ONE_HAND_SWORD", weapon.WeaponCategory);
        Assert.Equal(14, weapon.RequiredLevel);
        Assert.Equal(34, weapon.WeaponDamageMin);
        Assert.Equal(52, weapon.WeaponDamageMax);
        Assert.Equal(14, weapon.ItemLevelMin);
        Assert.Equal(14, weapon.ItemLevelMax);
        Assert.Contains("WARRIOR", weapon.AllowedClassIds ?? []);
    }
}
