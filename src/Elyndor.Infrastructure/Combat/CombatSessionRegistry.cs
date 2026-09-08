using System.Collections.Concurrent;
using Elyndor.Core.Combat.Participants;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Progression;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.Combat;

public interface ICombatUpdatePublisher
{
    Task PublishAsync(Guid accountId, CombatOperationResult update, CancellationToken cancellationToken);

    async Task PublishAsync(
        IReadOnlyCollection<Guid> accountIds,
        CombatOperationResult update,
        CancellationToken cancellationToken)
    {
        foreach (Guid accountId in accountIds)
            await PublishAsync(accountId, update, cancellationToken);
    }
}

public interface ICombatActivityReader
{
    bool HasActiveCombat(Guid accountId);
}

public sealed record CombatParticipantBinding(Guid AccountId, Guid CharacterId);

public sealed class CombatSessionRegistry(
    TimeProvider timeProvider,
    ICombatUpdatePublisher publisher,
    ICombatSessionFinalizer finalizer,
    ILogger<CombatSessionRegistry> logger) : IDisposable, ICombatActivityReader
{
    private static readonly Action<ILogger, Guid, Guid, Guid, CombatSessionStatus, Exception?> TickFailed =
        LoggerMessage.Define<Guid, Guid, Guid, CombatSessionStatus>(
            LogLevel.Error,
            new EventId(2001, nameof(TickFailed)),
            "Combat timer tick failed for account {AccountId}, character {CharacterId}, "
            + "session {SessionId}, status {Status}.");

    private readonly ConcurrentDictionary<Guid, SessionEntry> _byAccount = [];
    private readonly ConcurrentDictionary<Guid, SessionEntry> _byCharacter = [];
    private readonly ConcurrentDictionary<Guid, SessionEntry> _bySession = [];
    private readonly object _indexGate = new();
    private bool _disposed;

    public bool TryAdd(
        Guid accountId,
        Guid characterId,
        CombatSession session,
        GameContentSnapshot? contentSnapshot = null,
        IReadOnlyCollection<CombatParticipantBinding>? additionalParticipants = null,
        string? locationId = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(session);
        ValidateContentIdentity(session, contentSnapshot);

        CombatParticipantBinding[] bindings =
        [
            new(accountId, characterId),
            .. additionalParticipants ?? []
        ];
        lock (_indexGate)
        {
            if (bindings.Any(binding => binding.AccountId == Guid.Empty || binding.CharacterId == Guid.Empty)
                || bindings.Select(binding => binding.AccountId).Distinct().Count() != bindings.Length
                || bindings.Select(binding => binding.CharacterId).Distinct().Count() != bindings.Length
                || bindings.Any(binding => _byAccount.ContainsKey(binding.AccountId)
                    || _byCharacter.ContainsKey(binding.CharacterId)))
                return false;

            SessionEntry entry = new(session, contentSnapshot, locationId);
            foreach (CombatParticipantBinding binding in bindings)
                entry.AddBinding(binding);

            foreach (CombatParticipantBinding binding in bindings)
            {
                _byAccount.TryAdd(binding.AccountId, entry);
                _byCharacter.TryAdd(binding.CharacterId, entry);
            }

            if (!_bySession.TryAdd(session.SessionId, entry))
            {
                foreach (CombatParticipantBinding rollback in bindings)
                {
                    _byAccount.TryRemove(rollback.AccountId, out _);
                    _byCharacter.TryRemove(rollback.CharacterId, out _);
                }
                return false;
            }

            Schedule(entry);
            return true;
        }
    }

    public Task<CombatOperationResult> ExecuteAsync(
        Guid accountId,
        Func<CombatSession, DateTimeOffset, CombatCommandResult> operation,
        CancellationToken cancellationToken) => ExecuteAsync(
            accountId,
            (session, _, now) => Task.FromResult(operation(session, now)),
            cancellationToken);

    public Task<CombatOperationResult> ExecuteAsync(
        Guid accountId,
        Func<CombatSession, GameContentSnapshot?, DateTimeOffset, CombatCommandResult> operation,
        CancellationToken cancellationToken) => ExecuteAsync(
            accountId,
            (session, contentSnapshot, now) =>
                Task.FromResult(operation(session, contentSnapshot, now)),
            cancellationToken);

    public Task<CombatOperationResult> ExecuteAsync(
        Guid accountId,
        Func<CombatSession, DateTimeOffset, Task<CombatCommandResult>> operation,
        CancellationToken cancellationToken) => ExecuteAsync(
            accountId,
            (session, _, now) => operation(session, now),
            cancellationToken);

    public Task<CombatOperationResult> ExecuteAsync(
        Guid accountId,
        Func<CombatSession, GameContentSnapshot?, DateTimeOffset, Task<CombatCommandResult>> operation,
        CancellationToken cancellationToken) => ExecuteCoreAsync(
            accountId,
            (entry, _, now) => operation(entry.Session, entry.ContentSnapshot, now),
            cancellationToken);

    public Task<CombatOperationResult> ExecuteParticipantAsync(
        Guid accountId,
        Func<CombatSession, Guid, DateTimeOffset, CombatCommandResult> operation,
        CancellationToken cancellationToken) => ExecuteParticipantAsync(
            accountId,
            (session, characterId, _, now) =>
                Task.FromResult(operation(session, characterId, now)),
            cancellationToken);

    public Task<CombatOperationResult> ExecuteParticipantAsync(
        Guid accountId,
        Func<CombatSession, Guid, GameContentSnapshot?, DateTimeOffset, CombatCommandResult> operation,
        CancellationToken cancellationToken) => ExecuteParticipantAsync(
            accountId,
            (session, characterId, contentSnapshot, now) =>
                Task.FromResult(operation(session, characterId, contentSnapshot, now)),
            cancellationToken);

    public Task<CombatOperationResult> ExecuteParticipantAsync(
        Guid accountId,
        Func<CombatSession, Guid, GameContentSnapshot?, DateTimeOffset, Task<CombatCommandResult>> operation,
        CancellationToken cancellationToken) => ExecuteCoreAsync(
            accountId,
            (entry, binding, now) => operation(
                entry.Session,
                binding.CharacterId,
                entry.ContentSnapshot,
                now),
            cancellationToken);

    private async Task<CombatOperationResult> ExecuteCoreAsync(
        Guid accountId,
        Func<SessionEntry, ParticipantBinding, DateTimeOffset, Task<CombatCommandResult>> operation,
        CancellationToken cancellationToken)
    {
        if (!_byAccount.TryGetValue(accountId, out SessionEntry? entry)
            || !entry.TryGetBinding(accountId, out ParticipantBinding? binding))
            return CombatOperationResult.Failure(CombatErrorCodes.NotFound);

        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            if (!_byAccount.TryGetValue(accountId, out SessionEntry? current)
                || !ReferenceEquals(current, entry)
                || !entry.TryGetBinding(accountId, out binding))
                return CombatOperationResult.Failure(CombatErrorCodes.NotFound);

            ParticipantBinding activeBinding = binding!;
            CombatCommandResult result = await operation(entry, activeBinding, timeProvider.GetUtcNow());
            Schedule(entry);
            await FinalizeIfNeededAsync(entry, result.Snapshot, cancellationToken);
            CombatOperationResult operationResult =
                CombatOperationResult.From(result, entry.ContentSnapshot) with
                {
                    Reward = entry.GetReward(activeBinding.CharacterId)
                };
            await PublishToParticipantsAsync(entry, operationResult, cancellationToken);
            return operationResult;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public async Task<CombatOperationResult> AttachAsync(
        Guid accountId,
        Guid sessionId,
        Guid characterId,
        string? currentLocationId,
        CancellationToken cancellationToken)
    {
        if (!_bySession.TryGetValue(sessionId, out SessionEntry? entry))
            return CombatOperationResult.Failure(CombatErrorCodes.NotFound);

        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            if (!_bySession.TryGetValue(sessionId, out SessionEntry? current)
                || !ReferenceEquals(current, entry))
                return CombatOperationResult.Failure(CombatErrorCodes.NotFound);
            if (string.IsNullOrWhiteSpace(entry.LocationId)
                || !string.Equals(entry.LocationId, currentLocationId, StringComparison.Ordinal))
                return CombatOperationResult.Failure(CombatErrorCodes.InvalidLocation);
            lock (_indexGate)
            {
                bool existingBinding = entry.TryGetBinding(accountId, out ParticipantBinding? binding)
                    && binding is not null
                    && binding.CharacterId == characterId;
                if ((!existingBinding && _byAccount.ContainsKey(accountId))
                    || (!existingBinding && _byCharacter.ContainsKey(characterId)))
                    return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);

                DateTimeOffset now = timeProvider.GetUtcNow();
                CombatParticipantStatus? status = entry.Session.ParticipantRoster.GetStatus(characterId);
                if (status is not CombatParticipantStatus.Active
                    && !entry.Session.TryAttachParticipant(characterId, now, out string? errorCode))
                    return CombatOperationResult.Failure(errorCode ?? CombatParticipantErrorCodes.NotInRoster);

                if (!existingBinding)
                {
                    _byAccount.TryAdd(accountId, entry);
                    _byCharacter.TryAdd(characterId, entry);
                }
            }

            if (!entry.TryGetBinding(accountId, out _))
                entry.AddBinding(new CombatParticipantBinding(accountId, characterId));
            Schedule(entry);
            CombatOperationResult result = CombatOperationResult.FromSnapshot(
                entry.Session.Snapshot(characterId),
                entry.ContentSnapshot);
            await PublishToParticipantsAsync(entry, result, cancellationToken);
            return result;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public bool HasActiveCombat(Guid accountId)
    {
        if (!_byAccount.TryGetValue(accountId, out SessionEntry? entry)
            || entry.Session.Status != CombatSessionStatus.Active
            || !entry.TryGetBinding(accountId, out ParticipantBinding? binding)
            || binding is null)
        {
            return false;
        }

        CombatParticipantStatus? status = entry.Session.ParticipantRoster.GetStatus(binding.CharacterId);
        return status is CombatParticipantStatus.Active or CombatParticipantStatus.Dead;
    }

    public CombatOperationResult Resume(Guid accountId)
    {
        if (!_byAccount.TryGetValue(accountId, out SessionEntry? entry)
            || !entry.TryGetBinding(accountId, out ParticipantBinding? binding)
            || binding is null)
            return CombatOperationResult.Failure(CombatErrorCodes.NotFound);

        return entry.GetPublishedSnapshot(accountId);
    }

    public async Task PublishCurrentAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (!_byAccount.TryGetValue(accountId, out SessionEntry? entry)) return;
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            await PublishToParticipantsAsync(entry, entry.GetPublishedSnapshot(accountId), cancellationToken);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public void ClearFinished(Guid accountId)
    {
        if (_byAccount.TryGetValue(accountId, out SessionEntry? entry)
            && entry.Session.Status != CombatSessionStatus.Active)
            Remove(entry);
        else if (entry is not null && entry.TryGetBinding(accountId, out ParticipantBinding? binding)
            && binding is not null && entry.FinalizedParticipants.ContainsKey(binding.CharacterId)
            && entry.Session.ParticipantRoster.GetStatus(binding.CharacterId) == CombatParticipantStatus.Fled)
        {
            lock (_indexGate)
            {
                _byAccount.TryRemove(new KeyValuePair<Guid, SessionEntry>(accountId, entry));
                _byCharacter.TryRemove(new KeyValuePair<Guid, SessionEntry>(binding.CharacterId, entry));
            }
        }
    }

    public async Task<bool> DiscardAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (!_byAccount.TryGetValue(accountId, out SessionEntry? entry)) return false;

        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            if (!_byAccount.TryGetValue(accountId, out SessionEntry? current)
                || !ReferenceEquals(current, entry))
                return false;

            Remove(entry);
            return true;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    public async Task<CombatOperationResult> LeaveAsync(Guid accountId, CancellationToken cancellationToken)
    {
        CombatOperationResult result = await ExecuteAsync(
            accountId,
            (session, now) => session.Cancel(now),
            cancellationToken);
        if (_byAccount.TryGetValue(accountId, out SessionEntry? entry))
            Remove(entry);
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
            _ => _ = TickSafelyAsync(entry), null, due, Timeout.InfiniteTimeSpan);
    }

    private async Task TickSafelyAsync(SessionEntry entry)
    {
        try
        {
            await TickAsync(entry);
        }
        catch (Exception exception)
        {
            TickFailed(
                logger,
                entry.LeaderAccountId,
                entry.LeaderCharacterId,
                entry.Session.SessionId,
                entry.Session.Status,
                exception);
        }
    }

    private async Task TickAsync(SessionEntry entry)
    {
        await entry.Gate.WaitAsync();
        try
        {
            if (!_bySession.TryGetValue(entry.Session.SessionId, out SessionEntry? current)
                || !ReferenceEquals(current, entry)) return;

            entry.Timer?.Dispose();
            entry.Timer = null;
            CombatCommandResult result = entry.Session.AdvanceTo(timeProvider.GetUtcNow());
            Schedule(entry);
            await FinalizeIfNeededAsync(entry, result.Snapshot, CancellationToken.None);
            await PublishToParticipantsAsync(
                entry,
                CombatOperationResult.From(result, entry.ContentSnapshot) with
                {
                    Reward = entry.GetReward(entry.LeaderCharacterId)
                },
                CancellationToken.None);
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    private async Task FinalizeIfNeededAsync(
        SessionEntry entry,
        CombatSessionSnapshot? snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot is null
            || entry.Finalized)
            return;

        foreach (ParticipantBinding binding in entry.Bindings)
        {
            CombatParticipantSnapshot? participant = snapshot.ParticipantRoster?
                .FirstOrDefault(item => item.CharacterId == binding.CharacterId);
            if (participant?.Status == CombatParticipantStatus.Rostered
                || entry.FinalizedParticipants.ContainsKey(binding.CharacterId)
                || snapshot.Status == CombatSessionStatus.Active && participant?.Status != CombatParticipantStatus.Fled)
                continue;

            CombatRewardApplicationResult? reward = await finalizer.FinalizeAsync(
                binding.CharacterId,
                entry.Session.Snapshot(binding.CharacterId),
                entry.ContentSnapshot,
                cancellationToken);
            entry.SetReward(binding.CharacterId, reward);
            entry.FinalizedParticipants.TryAdd(binding.CharacterId, true);
        }

        entry.Finalized = snapshot.Status != CombatSessionStatus.Active;
    }

    private async Task PublishToParticipantsAsync(
        SessionEntry entry,
        CombatOperationResult operationResult,
        CancellationToken cancellationToken)
    {
        foreach (ParticipantBinding binding in entry.Bindings)
        {
            if (!_byAccount.TryGetValue(binding.AccountId, out SessionEntry? current) || !ReferenceEquals(current, entry))
                continue;
            CombatOperationResult participantResult = operationResult with
            {
                Snapshot = entry.Session.Snapshot(binding.CharacterId),
                Reward = entry.GetReward(binding.CharacterId)
            };
            entry.SetPublishedSnapshot(binding.AccountId, participantResult);
            await publisher.PublishAsync(
                binding.AccountId,
                participantResult,
                cancellationToken);
        }
    }

    private static void ValidateContentIdentity(
        CombatSession session,
        GameContentSnapshot? contentSnapshot)
    {
        if (contentSnapshot is not null
            && (!string.Equals(session.ContentVersion, contentSnapshot.ContentVersion, StringComparison.Ordinal)
                || !string.Equals(session.BalanceVersion, contentSnapshot.BalanceVersion, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Combat session content identity does not match its pinned snapshot.");
        }
    }

    private void Remove(SessionEntry entry)
    {
        lock (_indexGate)
        {
            foreach (ParticipantBinding binding in entry.Bindings)
            {
                _byAccount.TryRemove(new KeyValuePair<Guid, SessionEntry>(binding.AccountId, entry));
                _byCharacter.TryRemove(new KeyValuePair<Guid, SessionEntry>(binding.CharacterId, entry));
            }

            _bySession.TryRemove(entry.Session.SessionId, out _);
        }
        entry.Timer?.Dispose();
        entry.Timer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (SessionEntry entry in _bySession.Values)
        {
            entry.Timer?.Dispose();
            entry.Timer = null;
        }

        _byAccount.Clear();
        _byCharacter.Clear();
        _bySession.Clear();
    }

    private sealed class SessionEntry(
        CombatSession session,
        GameContentSnapshot? contentSnapshot,
        string? locationId)
    {
        private readonly Dictionary<Guid, ParticipantBinding> _bindingsByAccount = [];
        private readonly Dictionary<Guid, CombatRewardApplicationResult?> _rewardsByCharacter = [];
        private readonly ConcurrentDictionary<Guid, CombatOperationResult> _publishedSnapshots = [];

        public CombatSession Session { get; } = session;
        public GameContentSnapshot? ContentSnapshot { get; } = contentSnapshot;
        public string? LocationId { get; } = locationId;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public ITimer? Timer { get; set; }
        public bool Finalized { get; set; }
        public ConcurrentDictionary<Guid, bool> FinalizedParticipants { get; } = [];
        public Guid LeaderAccountId => _bindingsByAccount.Values.First().AccountId;
        public Guid LeaderCharacterId => _bindingsByAccount.Values.First().CharacterId;
        public IReadOnlyCollection<Guid> AccountIds => _bindingsByAccount.Keys.ToArray();
        public IReadOnlyCollection<ParticipantBinding> Bindings => _bindingsByAccount.Values.ToArray();

        public void AddBinding(CombatParticipantBinding binding)
        {
            _bindingsByAccount.Add(
                binding.AccountId,
                new ParticipantBinding(binding.AccountId, binding.CharacterId));
            SetPublishedSnapshot(binding.AccountId,
                CombatOperationResult.FromSnapshot(Session.Snapshot(binding.CharacterId), ContentSnapshot));
        }

        public void SetPublishedSnapshot(Guid accountId, CombatOperationResult result) =>
            _publishedSnapshots[accountId] = result with { Succeeded = true, ErrorCode = null, Events = [] };

        public CombatOperationResult GetPublishedSnapshot(Guid accountId) =>
            _publishedSnapshots.GetValueOrDefault(accountId)
                ?? CombatOperationResult.Failure(CombatErrorCodes.NotFound);

        public bool TryGetBinding(Guid accountId, out ParticipantBinding? binding) =>
            _bindingsByAccount.TryGetValue(accountId, out binding);

        public CombatRewardApplicationResult? GetReward(Guid characterId) =>
            _rewardsByCharacter.GetValueOrDefault(characterId);

        public void SetReward(Guid characterId, CombatRewardApplicationResult? reward) =>
            _rewardsByCharacter[characterId] = reward;
    }

    private sealed record ParticipantBinding(Guid AccountId, Guid CharacterId);
}

public sealed record CombatOperationResult(
    bool Succeeded,
    string? ErrorCode,
    CombatSessionSnapshot? Snapshot,
    IReadOnlyList<Core.Combat.CombatEvent> Events,
    CombatRewardApplicationResult? Reward = null,
    GameContentSnapshot? ContentSnapshot = null)
{
    public static CombatOperationResult Failure(string errorCode) => new(false, errorCode, null, []);

    public static CombatOperationResult FromSnapshot(
        CombatSessionSnapshot snapshot,
        GameContentSnapshot? contentSnapshot = null) =>
        new(true, null, snapshot, [], ContentSnapshot: contentSnapshot);

    public static CombatOperationResult From(
        CombatCommandResult result,
        GameContentSnapshot? contentSnapshot = null) =>
        new(result.Succeeded, result.ErrorCode, result.Snapshot, result.Events, ContentSnapshot: contentSnapshot);
}
