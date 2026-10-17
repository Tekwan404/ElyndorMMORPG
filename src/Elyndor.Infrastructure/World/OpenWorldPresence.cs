using System.Collections.Concurrent;
using Elyndor.Core.World;

namespace Elyndor.Infrastructure.World;

public sealed class OpenWorldPresence(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _observed = [];

    public void Observe(Guid accountId) => _observed[accountId] = timeProvider.GetUtcNow();

    public Guid[] ActiveAccounts()
    {
        DateTimeOffset cutoff = timeProvider.GetUtcNow() - OpenWorldPvpRules.PresenceLifetime;
        foreach (var entry in _observed)
            if (entry.Value < cutoff) _observed.TryRemove(entry);
        return _observed.Keys.ToArray();
    }

    public bool IsPresent(Guid accountId) => _observed.TryGetValue(accountId, out var seen)
        && seen >= timeProvider.GetUtcNow() - OpenWorldPvpRules.PresenceLifetime;
}
