namespace Elyndor.Infrastructure.Pvp;

/// <summary>Tracks live arena hub connections per account (queue liveness and reconnect grace).</summary>
public sealed class ArenaPresenceTracker(TimeProvider time)
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Presence> _accounts = [];

    public void Connected(Guid accountId, string connectionId)
    {
        lock (_gate)
        {
            if (!_accounts.TryGetValue(accountId, out Presence? presence))
                _accounts[accountId] = presence = new Presence();
            presence.Connections.Add(connectionId);
            presence.LastSeenUtc = time.GetUtcNow();
        }
    }

    public void Disconnected(Guid accountId, string connectionId)
    {
        lock (_gate)
        {
            if (!_accounts.TryGetValue(accountId, out Presence? presence)) return;
            presence.Connections.Remove(connectionId);
            presence.LastSeenUtc = time.GetUtcNow();
        }
    }

    public bool IsConnected(Guid accountId)
    {
        lock (_gate) return _accounts.TryGetValue(accountId, out Presence? p) && p.Connections.Count > 0;
    }

    /// <summary>Last time the account connected or disconnected; null if never seen.</summary>
    public DateTimeOffset? LastSeenUtc(Guid accountId)
    {
        lock (_gate) return _accounts.TryGetValue(accountId, out Presence? p) ? p.LastSeenUtc : null;
    }

    private sealed class Presence
    {
        public HashSet<string> Connections { get; } = [];
        public DateTimeOffset LastSeenUtc { get; set; }
    }
}
