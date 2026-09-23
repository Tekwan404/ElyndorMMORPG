using System.Collections.Concurrent;

namespace Elyndor.Server.Monitoring;

public sealed class OnlinePlayerPresence
{
    public static readonly TimeSpan DefaultOnlineTtl = TimeSpan.FromSeconds(90);

    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastSeenByAccount = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _onlineTtl;

    public OnlinePlayerPresence(TimeProvider timeProvider)
        : this(timeProvider, DefaultOnlineTtl)
    {
    }

    public OnlinePlayerPresence(TimeProvider timeProvider, TimeSpan onlineTtl)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        if (onlineTtl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(onlineTtl), "Online TTL must be positive.");
        _onlineTtl = onlineTtl;
    }

    public void MarkSeen(Guid accountId)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account ID cannot be empty.", nameof(accountId));

        DateTimeOffset now = _timeProvider.GetUtcNow();
        _lastSeenByAccount[accountId] = now;
        PruneExpired(now);
    }

    public int CountOnline()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        PruneExpired(now);
        return _lastSeenByAccount.Count;
    }

    private void PruneExpired(DateTimeOffset now)
    {
        foreach ((Guid accountId, DateTimeOffset lastSeen) in _lastSeenByAccount)
        {
            if (now - lastSeen <= _onlineTtl)
                continue;

            if (_lastSeenByAccount.TryGetValue(accountId, out DateTimeOffset current)
                && current == lastSeen)
            {
                _lastSeenByAccount.TryRemove(accountId, out _);
            }
        }
    }
}
