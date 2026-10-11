using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Items;

public sealed class ItemEnchantmentTests
{
    [Fact]
    public void ReplacingEnchantmentDoesNotStackOrChangeBaseRoll()
    {
        var item = new CharacterItem(Guid.NewGuid(), Guid.NewGuid(), "CHEST", 1, DateTimeOffset.UtcNow);
        var stats = new PrimaryStats(4, 3, 2, 1);
        item.SetEnchantment("STAMINA", "STAMINA", 2);
        Assert.Equal(new PrimaryStats(4, 3, 2, 3), item.ApplyEnchantment(stats));
        item.SetEnchantment("STRENGTH", "STRENGTH", 1);
        Assert.Equal(new PrimaryStats(5, 3, 2, 1), item.ApplyEnchantment(stats));
        Assert.Equal(new PrimaryStats(4, 3, 2, 1), stats);
    }

    [Fact]
    public void LockedItemsAndInvalidStatsRejectEnchantment()
    {
        var item = new CharacterItem(Guid.NewGuid(), Guid.NewGuid(), "CHEST", 1, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => item.SetEnchantment("BAD", "GOLD", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => item.SetEnchantment("BAD", "STAMINA", -1));
        item.SetLocked(true);
        Assert.Throws<InvalidOperationException>(() => item.SetEnchantment("STAMINA", "STAMINA", 1));
    }
}
