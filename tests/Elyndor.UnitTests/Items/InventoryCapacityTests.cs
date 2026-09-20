using Elyndor.Core.Content;
using Elyndor.Infrastructure.Items;

namespace Elyndor.UnitTests.Items;

public sealed class InventoryCapacityTests
{
    [Fact]
    public void DefaultCapacityIsThirtySlots()
    {
        Assert.Equal(30, InventoryCapacity.DefaultCapacity);
    }

    [Fact]
    public void MissingProfileFallsBackToThirtySlots()
    {
        Assert.Equal(30, InventoryCapacity.Resolve(CreatePackage(null)));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(40)]
    [InlineData(80)]
    [InlineData(100)]
    [InlineData(120)]
    public void ExplicitCapacityIsPreserved(int configuredCapacity)
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
