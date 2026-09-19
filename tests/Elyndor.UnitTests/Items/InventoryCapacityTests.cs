using Elyndor.Infrastructure.Items;

namespace Elyndor.UnitTests.Items;

public sealed class InventoryCapacityTests
{
    [Fact]
    public void DefaultCapacity_IsOneHundredSlots()
    {
        Assert.Equal(100, InventoryCapacity.DefaultCapacity);
    }
}
