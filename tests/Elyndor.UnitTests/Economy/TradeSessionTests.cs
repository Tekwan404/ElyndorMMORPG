using Elyndor.Core.Economy;

namespace Elyndor.UnitTests.Economy;

public sealed class TradeSessionTests
{
    [Fact]
    public void ChangedOfferInvalidatesBothLocksAndConfirmations()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var trade = new PlayerTrade(Guid.NewGuid(), a, b, DateTimeOffset.UtcNow);
        trade.Connect(a, "a");
        trade.Connect(b, "b");
        trade.Lock(a, 0);
        trade.Lock(b, 0);
        trade.Confirm(a, 0);
        trade.ChangeOffer(b, [Guid.NewGuid()], 10);
        Assert.Equal(1, trade.Revision);
        Assert.False(trade.LockedA);
        Assert.False(trade.LockedB);
        Assert.False(trade.ConfirmedA);
        Assert.False(trade.ConfirmedB);
        Assert.Throws<CommerceRuleException>(() => trade.Confirm(a, 0));
    }

    [Fact]
    public void DisconnectInvalidatesTradeAndLateConfirmation()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var trade = new PlayerTrade(Guid.NewGuid(), a, b, DateTimeOffset.UtcNow);
        trade.Connect(a, "a");
        trade.Connect(b, "b");
        trade.Lock(a, 0);
        trade.Lock(b, 0);
        trade.Confirm(a, 0);
        trade.Disconnect("b");
        Assert.Equal("CANCELLED", trade.State);
        Assert.Throws<CommerceRuleException>(() => trade.Confirm(b, 0));
    }

    [Fact]
    public void BothCurrentRevisionConfirmationsAreRequired()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var trade = new PlayerTrade(Guid.NewGuid(), a, b, DateTimeOffset.UtcNow);
        trade.Connect(a, "a");
        trade.Connect(b, "b");
        Assert.Throws<CommerceRuleException>(() => trade.Confirm(a, 0));
        trade.Lock(a, 0);
        trade.Lock(b, 0);
        trade.Confirm(a, 0);
        Assert.False(trade.Ready);
        trade.Confirm(b, 0);
        Assert.True(trade.Ready);
        trade.Complete();
        Assert.Throws<CommerceRuleException>(() => trade.ChangeOffer(a, [], 0));
    }
}
