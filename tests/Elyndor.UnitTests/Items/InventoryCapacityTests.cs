using Elyndor.Core.Content;
using Elyndor.Infrastructure.Items;

namespace Elyndor.UnitTests.Items;

public sealed class InventoryCapacityTests
{
    [Fact]
    public void DefaultCapacityIsOneHundredSlots()
    {
        Assert.Equal(100, InventoryCapacity.DefaultCapacity);
    }

    [Fact]
    public void LegacyFortySlotProfileMigratesToOneHundredSlots()
    {
        GameContentPackage package = CreatePackage(new InventoryProfileDefinition(40));

        Assert.Equal(100, InventoryCapacity.Resolve(package));
    }

    [Theory]
    [InlineData(80)]
    [InlineData(120)]
    public void ExplicitNonLegacyCapacityIsPreserved(int configuredCapacity)
    {
        GameContentPackage package = CreatePackage(new InventoryProfileDefinition(configuredCapacity));

        Assert.Equal(configuredCapacity, InventoryCapacity.Resolve(package));
    }

    private static GameContentPackage CreatePackage(InventoryProfileDefinition? inventoryProfile) =>
        new(
            "test-content",
            "test-balance",
            DateTimeOffset.UnixEpoch,
            [],
            [],
            InventoryProfile: inventoryProfile);
}
