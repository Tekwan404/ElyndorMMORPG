using System.Collections.Concurrent;
using Elyndor.Core.Combat.Sessions;

namespace Elyndor.Infrastructure.Combat;

public interface ICombatUpdatePublisher
{
    Task PublishAsync(Guid accountId, CombatCommandResult update, CancellationToken cancellationToken);
}

public sealed class CombatSessionRegistry(
    TimeProvider timeProvider,
    ICombatUpdatePublisher publisher) : IDisposable
{
    private readonly ConcurrentDictionary<Guid, SessionEntry> _byAccount = [];
    private readonly ConcurrentDictionary<Guid, SessionEntry> _byCharacter = [];
    private readonly ConcurrentDictionary<Guid, SessionEntry> _bySession = [];
    private bool _disposed;

    public bool TryAdd(Guid accountId, Guid characterId, CombatSession session)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SessionEntry entry = new(accountId, characterId, session);
        if (!_byAccount.TryAdd(accountId, entry)) return false;
        if (!_byCharacter.TryAdd(characterId, entry))
        {
            _byAccount.TryRemove(accountId, out _);
            return false;
        }
        if (_bySession.TryAdd(session.SessionId, entry))
        {
            Schedule(entry);
            return true;
        }

        _byAccount.TryRemove(accountId, out _);
        _byCharacter.TryRemove(characterId, out _);
        return false;
    }

    public async Task<CombatOperationResult> ExecuteAsync(
        Guid accountId,
        Func<CombatSession, DateTimeOffset, CombatCommandResult> operation,
        CancellationToken cancellationToken)
    {
        if (!_byAccount.TryGetValue(accountId, out SessionEntry? entry))
            return CombatOperationResult.Failure(CombatErrorCodes.NotFound);

        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            CombatCommandResult result = operation(entry.Session, timeProvider.GetUtcNow());
            Schedule(entry);
            await publisher.PublishAsync(accountId, result, cancellationToken);
            return CombatOperationResult.From(result);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public CombatOperationResult Resume(Guid accountId) =>
        _byAccount.TryGetValue(accountId, out SessionEntry? entry)
            ? CombatOperationResult.FromSnapshot(entry.Session.Snapshot())
            : CombatOperationResult.Failure(CombatErrorCodes.NotFound);

    public void ClearFinished(Guid accountId)
    {
        if (_byAccount.TryGetValue(accountId, out SessionEntry? entry)
            && entry.Session.Status != CombatSessionStatus.Active)
            Remove(accountId);
    }

    public async Task<CombatOperationResult> LeaveAsync(Guid accountId, CancellationToken cancellationToken)
    {
        CombatOperationResult result = await ExecuteAsync(
            accountId, (session, now) => session.Cancel(now), cancellationToken);
        Remove(accountId);
        return result;
    }

    private void Schedule(SessionEntry entry)
    {
        entry.Timer?.Dispose();
        entry.Timer = null;
        DateTimeOffset? dueAt = entry.Session.NextDueAtUtc;
        if (dueAt is null) return;
        TimeSpan due = dueAt.Value - timeProvider.GetUtcNow();
        if (due < TimeSpan.Zero) due = TimeSpan.Zero;
        entry.Timer = timeProvider.CreateTimer(
            _ => _ = TickAsync(entry), null, due, Timeout.InfiniteTimeSpan);
    }

    private async Task TickAsync(SessionEntry entry)
    {
        await entry.Gate.WaitAsync();
        try
        {
            if (!_byAccount.TryGetValue(entry.AccountId, out SessionEntry? current)
                || !ReferenceEquals(current, entry)) return;
            CombatCommandResult result = entry.Session.AdvanceTo(timeProvider.GetUtcNow());
            Schedule(entry);
            await publisher.PublishAsync(entry.AccountId, result, CancellationToken.None);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    private void Remove(Guid accountId)
    {
        if (!_byAccount.TryRemove(accountId, out SessionEntry? entry)) return;
        _byCharacter.TryRemove(entry.CharacterId, out _);
        _bySession.TryRemove(entry.Session.SessionId, out _);
        entry.Timer?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (SessionEntry entry in _byAccount.Values) entry.Timer?.Dispose();
        _byAccount.Clear();
        _byCharacter.Clear();
        _bySession.Clear();
    }

    private sealed class SessionEntry(Guid accountId, Guid characterId, CombatSession session)
    {
        public Guid AccountId { get; } = accountId;
        public Guid CharacterId { get; } = characterId;
        public CombatSession Session { get; } = session;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public ITimer? Timer { get; set; }
    }
}

public sealed record CombatOperationResult(
    bool Succeeded,
    string? ErrorCode,
    CombatSessionSnapshot? Snapshot,
    IReadOnlyList<Core.Combat.CombatEvent> Events)
{
    public static CombatOperationResult Failure(string errorCode) => new(false, errorCode, null, []);
    public static CombatOperationResult FromSnapshot(CombatSessionSnapshot snapshot) => new(true, null, snapshot, []);
    public static CombatOperationResult From(CombatCommandResult result) =>
        new(result.Succeeded, result.ErrorCode, result.Snapshot, result.Events);
}
