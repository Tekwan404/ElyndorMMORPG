using Elyndor.Core.Professions;

namespace Elyndor.UnitTests.World;

public sealed class ResourceNodeStateTests
{
    [Fact]
    public void SharedNodeCannotBeHarvestedAgainBeforeRespawnBoundary()
    {
        var now = DateTimeOffset.UnixEpoch;
        var node = new ResourceNodeState("FOREST_MINT_1");
        Assert.True(node.TryHarvest(now, 30));
        Assert.False(node.TryHarvest(now.AddSeconds(29.999), 30));
        Assert.Equal(now.AddSeconds(30), node.AvailableAtUtc);
        Assert.True(node.TryHarvest(now.AddSeconds(30), 30));
        Assert.Equal(now.AddSeconds(60), node.AvailableAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidRespawnCannotAdvanceNode(int seconds)
    {
        var node = new ResourceNodeState("FOREST_MINT_1");
        Assert.Throws<ArgumentOutOfRangeException>(() => node.TryHarvest(DateTimeOffset.UnixEpoch, seconds));
        Assert.Equal(DateTimeOffset.UnixEpoch, node.AvailableAtUtc);
    }
}
