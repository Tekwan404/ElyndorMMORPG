using System.Collections.Concurrent;
using Elyndor.Core.World;

namespace Elyndor.Infrastructure.World;

public sealed record OpenWorldRosterEntry(Guid AccountId, OpenWorldPlayer Player);

public sealed class OpenWorldPresence(TimeProvider timeProvider) : IDisposable
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _observed = [];
    private readonly SemaphoreSlim _rosterGate = new(1, 1);
    private IReadOnlyList<OpenWorldRosterEntry> _roster = [];
    private DateTimeOffset _rosterValidUntil;

    // Shared across all viewers: caller-specific self filtering happens after this cache.
    public async Task<IReadOnlyList<OpenWorldRosterEntry>> GetRosterAsync(
        Func<CancellationToken, Task<IReadOnlyList<OpenWorldRosterEntry>>> load,
        CancellationToken ct)
    {
        await _rosterGate.WaitAsync(ct);
        try
        {
            if (timeProvider.GetUtcNow() >= _rosterValidUntil)
            {
                _roster = await load(ct);
                _rosterValidUntil = timeProvider.GetUtcNow().AddSeconds(3);
            }
            return _roster;
        }
        finally { _rosterGate.Release(); }
    }

    public long RotationWindow => timeProvider.GetUtcNow().ToUnixTimeSeconds() / 30;


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

    public void Dispose() => _rosterGate.Dispose();
}
