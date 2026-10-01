using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Pvp;
using Microsoft.Extensions.Options;

namespace Elyndor.Infrastructure.Pvp;

public sealed record ArenaFinishedMatch(Guid MatchId, ArenaMatchOutcome Outcome,
    Guid FirstAccountId, Guid SecondAccountId, long Sequence);

public sealed record ArenaMatchUpdate(Guid MatchId, Guid AccountId, long Sequence);

public sealed record ArenaTickResult(
    IReadOnlyList<ArenaFinishedMatch> Finished,
    IReadOnlyList<ArenaMatchUpdate> Updates);

/// <summary>
/// In-memory owner of active arena combat. One lock guards every mutation, so each match has
/// single-writer semantics. PostgreSQL stays the source of truth for queue, result and rewards;
/// after a restart orphaned matches are cancelled by <see cref="ArenaRecoveryService"/>.
/// </summary>
public sealed class ArenaMatchRuntime(
    TimeProvider time,
    IGameRandomFactory randomFactory,
    ArenaPresenceTracker presence,
    IOptions<ArenaOptions> options)
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, LiveMatch> _matches = [];
    private readonly Dictionary<Guid, Guid> _activeByAccount = [];

    public void Register(Guid matchId, ArenaTestEntrant first, ArenaTestEntrant second)
    {
        lock (_gate)
        {
            if (_activeByAccount.ContainsKey(first.Fighter.AccountId)
                || _activeByAccount.ContainsKey(second.Fighter.AccountId))
                throw new InvalidOperationException("Account is already in an arena match.");
            var session = new ArenaCombatSession(matchId, first.Fighter, second.Fighter,
                randomFactory.Create(), time.GetUtcNow(), options.Value.MatchDuration);
            _matches.Add(matchId, new LiveMatch(session, first, second));
            _activeByAccount[first.Fighter.AccountId] = matchId;
            _activeByAccount[second.Fighter.AccountId] = matchId;
        }
    }

    public bool IsInMatch(Guid accountId)
    {
        lock (_gate) return _activeByAccount.ContainsKey(accountId);
    }

    public Guid? ActiveMatchId(Guid accountId)
    {
        lock (_gate) return _activeByAccount.TryGetValue(accountId, out Guid id) ? id : null;
    }

    public Guid? OpponentAccountId(Guid matchId, Guid accountId)
    {
        lock (_gate)
        {
            if (!_matches.TryGetValue(matchId, out LiveMatch? match) || !match.HasParticipant(accountId))
                return null;
            return match.Opponent(accountId).Fighter.AccountId;
        }
    }

    public ArenaTestState? GetState(Guid accountId, Guid matchId, long afterSequence)
    {
        lock (_gate)
        {
            if (!TryGetParticipantMatch(accountId, matchId, out LiveMatch? match)) return null;
            DateTimeOffset now = time.GetUtcNow();
            match.Session.AdvanceTo(now);
            return Project(match, accountId, afterSequence, now);
        }
    }

    public ArenaTestCommandResult UseAbility(Guid accountId, Guid matchId, string commandId,
        string abilityId, Guid targetActorId)
    {
        lock (_gate)
        {
            if (!TryGetParticipantMatch(accountId, matchId, out LiveMatch? match))
                return new ArenaTestCommandResult(false, "arena_match_not_found", null);
            DateTimeOffset now = time.GetUtcNow();
            ArenaCommandResult result = match.Session.UseAbility(accountId, commandId, abilityId,
                targetActorId, now);
            return new ArenaTestCommandResult(result.Succeeded, result.ErrorCode,
                Project(match, accountId, result.Snapshot.Sequence - result.Events.Count, now));
        }
    }

    public ArenaTestCommandResult Surrender(Guid accountId, Guid matchId)
    {
        lock (_gate)
        {
            if (!TryGetParticipantMatch(accountId, matchId, out LiveMatch? match))
                return new ArenaTestCommandResult(false, "arena_match_not_found", null);
            DateTimeOffset now = time.GetUtcNow();
            match.Session.AdvanceTo(now);
            bool forfeited = match.Session.Forfeit(accountId, now);
            return new ArenaTestCommandResult(forfeited, forfeited ? null : "arena_ended",
                Project(match, accountId, 0, now));
        }
    }

    public ArenaTestCommandResult SetAutoAttack(Guid accountId, Guid matchId, string commandId, bool enabled)
    {
        lock (_gate)
        {
            if (!TryGetParticipantMatch(accountId, matchId, out LiveMatch? match))
                return new ArenaTestCommandResult(false, "arena_match_not_found", null);
            DateTimeOffset now = time.GetUtcNow();
            ArenaCommandResult result = match.Session.SetAutoAttack(accountId, commandId, enabled, now);
            return new ArenaTestCommandResult(result.Succeeded, result.ErrorCode,
                Project(match, accountId, result.Snapshot.Sequence - result.Events.Count, now));
        }
    }

    /// <summary>Advances every live match, applies disconnect forfeits and reports results.</summary>
    public ArenaTickResult Tick()
    {
        var finished = new List<ArenaFinishedMatch>();
        var updates = new List<ArenaMatchUpdate>();
        lock (_gate)
        {
            DateTimeOffset now = time.GetUtcNow();
            foreach ((Guid matchId, LiveMatch match) in _matches)
            {
                if (match.Finalized) continue;
                try
                {
                    match.Session.AdvanceTo(now);
                    if (match.Session.Outcome == ArenaMatchOutcome.Active)
                        ApplyDisconnects(match, now);
                }
                catch (InvalidOperationException)
                {
                    // Runaway simulation: never guess a winner.
                    match.Session.Cancel(now);
                }

                long sequence = match.Session.Snapshot.Sequence;
                if (sequence > match.PublishedSequence)
                {
                    match.PublishedSequence = sequence;
                    updates.Add(new ArenaMatchUpdate(matchId, match.First.Fighter.AccountId, sequence));
                    updates.Add(new ArenaMatchUpdate(matchId, match.Second.Fighter.AccountId, sequence));
                }
                if (match.Session.Outcome != ArenaMatchOutcome.Active)
                    finished.Add(new ArenaFinishedMatch(matchId, match.Session.Outcome,
                        match.First.Fighter.AccountId, match.Second.Fighter.AccountId, sequence));
            }
        }
        return new ArenaTickResult(finished, updates);
    }

    /// <summary>Call after the result is durably settled; frees the participants.</summary>
    public void MarkFinalized(Guid matchId)
    {
        lock (_gate)
        {
            if (!_matches.TryGetValue(matchId, out LiveMatch? match) || match.Finalized) return;
            match.Finalized = true;
            match.FinalizedAtUtc = time.GetUtcNow();
            _activeByAccount.Remove(match.First.Fighter.AccountId);
            _activeByAccount.Remove(match.Second.Fighter.AccountId);
        }
    }

    public void PurgeFinalized()
    {
        lock (_gate)
        {
            DateTimeOffset cutoff = time.GetUtcNow() - options.Value.CompletedMatchRetention;
            foreach (Guid id in _matches.Where(x => x.Value.Finalized && x.Value.FinalizedAtUtc <= cutoff)
                         .Select(x => x.Key).ToArray())
                _matches.Remove(id);
        }
    }

    private void ApplyDisconnects(LiveMatch match, DateTimeOffset now)
    {
        Guid a = match.First.Fighter.AccountId;
        Guid b = match.Second.Fighter.AccountId;
        bool absentA = IsAbsent(a, match.Session.StartedAtUtc, now);
        bool absentB = IsAbsent(b, match.Session.StartedAtUtc, now);
        if (absentA && absentB) match.Session.Cancel(now);
        else if (absentA) match.Session.Forfeit(a, now);
        else if (absentB) match.Session.Forfeit(b, now);
    }

    private bool IsAbsent(Guid accountId, DateTimeOffset matchStart, DateTimeOffset now)
    {
        if (presence.IsConnected(accountId)) return false;
        DateTimeOffset since = presence.LastSeenUtc(accountId) is { } seen && seen > matchStart
            ? seen : matchStart;
        return now - since >= options.Value.ReconnectGrace;
    }

    private bool TryGetParticipantMatch(Guid accountId, Guid matchId, out LiveMatch match)
    {
        if (_matches.TryGetValue(matchId, out LiveMatch? found) && found.HasParticipant(accountId))
        {
            match = found;
            return true;
        }
        match = null!;
        return false;
    }

    private static ArenaTestState Project(LiveMatch match, Guid accountId, long afterSequence,
        DateTimeOffset now)
    {
        bool first = match.First.Fighter.AccountId == accountId;
        return ArenaStateProjector.Project(first ? match.First : match.Second,
            first ? match.Second : match.First, first, match.Session, now, afterSequence);
    }

    private sealed class LiveMatch(ArenaCombatSession session, ArenaTestEntrant first, ArenaTestEntrant second)
    {
        public ArenaCombatSession Session { get; } = session;
        public ArenaTestEntrant First { get; } = first;
        public ArenaTestEntrant Second { get; } = second;
        public bool Finalized { get; set; }
        public DateTimeOffset FinalizedAtUtc { get; set; }
        public long PublishedSequence { get; set; }

        public bool HasParticipant(Guid accountId) =>
            First.Fighter.AccountId == accountId || Second.Fighter.AccountId == accountId;

        public ArenaTestEntrant Opponent(Guid accountId) =>
            First.Fighter.AccountId == accountId ? Second : First;
    }
}
