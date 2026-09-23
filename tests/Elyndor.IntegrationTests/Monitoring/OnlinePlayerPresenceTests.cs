using Elyndor.Server.Monitoring;

namespace Elyndor.IntegrationTests.Monitoring;

public sealed class OnlinePlayerPresenceTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 23, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CountOnlineCountsUniqueAccountsOnly()
    {
        var timeProvider = new MutableTimeProvider(Start);
        var presence = new OnlinePlayerPresence(timeProvider, TimeSpan.FromSeconds(90));
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        presence.MarkSeen(first);
        presence.MarkSeen(first);
        presence.MarkSeen(second);

        Assert.Equal(2, presence.CountOnline());
    }

    [Fact]
    public void CountOnlineExpiresAccountAfterTtl()
    {
        var timeProvider = new MutableTimeProvider(Start);
        var presence = new OnlinePlayerPresence(timeProvider, TimeSpan.FromSeconds(90));
        Guid accountId = Guid.NewGuid();

        presence.MarkSeen(accountId);
        timeProvider.Advance(TimeSpan.FromSeconds(90));
        Assert.Equal(1, presence.CountOnline());

        timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, presence.CountOnline());
    }

    [Fact]
    public void MarkSeenRefreshesExistingAccountTtl()
    {
        var timeProvider = new MutableTimeProvider(Start);
        var presence = new OnlinePlayerPresence(timeProvider, TimeSpan.FromSeconds(90));
        Guid accountId = Guid.NewGuid();

        presence.MarkSeen(accountId);
        timeProvider.Advance(TimeSpan.FromSeconds(80));
        presence.MarkSeen(accountId);
        timeProvider.Advance(TimeSpan.FromSeconds(80));

        Assert.Equal(1, presence.CountOnline());

        timeProvider.Advance(TimeSpan.FromSeconds(11));
        Assert.Equal(0, presence.CountOnline());
    }

    [Fact]
    public async Task ConcurrentHeartbeatsRemainUniquePerAccount()
    {
        var timeProvider = new MutableTimeProvider(Start);
        var presence = new OnlinePlayerPresence(timeProvider, TimeSpan.FromSeconds(90));
        Guid[] accountIds = Enumerable.Range(0, 16)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        Task[] tasks = Enumerable.Range(0, 512)
            .Select(index => Task.Run(() => presence.MarkSeen(accountIds[index % accountIds.Length])))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(accountIds.Length, presence.CountOnline());
    }

    [Fact]
    public void ConstructorAndMarkSeenRejectInvalidInput()
    {
        var timeProvider = new MutableTimeProvider(Start);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OnlinePlayerPresence(timeProvider, TimeSpan.Zero));

        var presence = new OnlinePlayerPresence(timeProvider);
        Assert.Throws<ArgumentException>(() => presence.MarkSeen(Guid.Empty));
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan value) => _utcNow += value;
    }
}
