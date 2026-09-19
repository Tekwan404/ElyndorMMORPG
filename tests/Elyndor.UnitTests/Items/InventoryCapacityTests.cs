using Elyndor.Infrastructure.Items;

namespace Elyndor.UnitTests.Items;

public sealed class InventoryCapacityTests
{
    [Fact]
    public void DefaultCapacityIsOneHundredSlots()
    {
        Assert.Equal(100, InventoryCapacity.DefaultCapacity);
    }
}
