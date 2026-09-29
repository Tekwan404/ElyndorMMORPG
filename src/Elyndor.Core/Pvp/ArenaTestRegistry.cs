using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Pvp;

public enum ArenaTestStatus { Searching, Active, Completed }

public sealed record ArenaTestEntrant(ArenaFighter Fighter, int Level, string Name,
    string ClassId, string GenderId, string? SkinId, string ResourceType = "NONE");

public sealed record ArenaTestState(ArenaTestStatus Status, Guid? MatchId, Guid CharacterId,
    Guid? OpponentCharacterId, string? OpponentName, decimal PlayerHp, decimal OpponentHp,
    ArenaMatchOutcome Outcome, long Sequence, IReadOnlyList<CombatEvent> Events,
    CombatSessionSnapshot? Battle = null);

public sealed record ArenaTestCommandResult(bool Succeeded, string? ErrorCode, ArenaTestState? State);

public sealed class ArenaTestRegistry(TimeProvider time, Func<IGameRandom> randomFactory)
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Entry> _byAccount = [];
    private readonly List<Entry> _queue = [];

    public int ActiveParticipantCount
    {
        get { lock (_gate) return _byAccount.Count; }
    }

    public ArenaTestState Join(ArenaTestEntrant entrant)
    {
        if (entrant.Fighter.AccountId == Guid.Empty || entrant.Fighter.CharacterId == Guid.Empty)
            throw new ArgumentException("Arena entrant must have an account and character.");
        lock (_gate)
        {
            if (_byAccount.TryGetValue(entrant.Fighter.AccountId, out Entry? existing))
                return State(existing, 0);
            if (_byAccount.Values.Any(x => x.Entrant.Fighter.CharacterId == entrant.Fighter.CharacterId))
                throw new InvalidOperationException("Character is already in a test arena.");

            Entry entry = new(entrant);
            Entry? opponent = _queue.FirstOrDefault(candidate => ArenaMatchRules.CanPair(
                candidate.Entrant.Fighter.CharacterId, candidate.Entrant.Level,
                entrant.Fighter.CharacterId, entrant.Level));
            if (opponent is not null)
            {
                var session = new ArenaCombatSession(Guid.NewGuid(), opponent.Entrant.Fighter,
                    entrant.Fighter, randomFactory(), time.GetUtcNow());
                var match = new Match(session, opponent, entry);
                opponent.Match = match;
                entry.Match = match;
                _queue.Remove(opponent);
            }
            else _queue.Add(entry);
            _byAccount.Add(entrant.Fighter.AccountId, entry);
            return State(entry, 0);
        }
    }

    public ArenaTestState? Get(Guid accountId, long afterSequence = 0)
    {
        lock (_gate)
        {
            if (!_byAccount.TryGetValue(accountId, out Entry? entry)) return null;
            entry.Match?.Session.AdvanceTo(time.GetUtcNow());
            return State(entry, afterSequence);
        }
    }

    public ArenaTestCommandResult UseAbility(Guid accountId, Guid matchId, string commandId,
        string abilityId, Guid targetActorId)
    {
        lock (_gate)
        {
            if (!_byAccount.TryGetValue(accountId, out Entry? entry)
                || entry.Match is null || entry.Match.Session.MatchId != matchId)
                return new ArenaTestCommandResult(false, "arena_match_not_found", null);
            ArenaCommandResult result = entry.Match.Session.UseAbility(accountId, commandId,
                abilityId, targetActorId, time.GetUtcNow());
            return new ArenaTestCommandResult(result.Succeeded, result.ErrorCode,
                State(entry, result.Snapshot.Sequence - result.Events.Count));
        }
    }

    public bool Leave(Guid accountId)
    {
        lock (_gate)
        {
            if (!_byAccount.TryGetValue(accountId, out Entry? entry)) return false;
            if (entry.Match is { } match && match.Session.Outcome == ArenaMatchOutcome.Active)
                return false;
            _queue.Remove(entry);
            _byAccount.Remove(accountId);
            return true;
        }
    }

    public bool Surrender(Guid accountId)
    {
        lock (_gate)
        {
            return _byAccount.TryGetValue(accountId, out Entry? entry)
                && entry.Match?.Session.Forfeit(accountId, time.GetUtcNow()) == true;
        }
    }

    private ArenaTestState State(Entry entry, long afterSequence)
    {
        if (entry.Match is not { } match)
            return new ArenaTestState(ArenaTestStatus.Searching, null,
                entry.Entrant.Fighter.CharacterId, null, null, 0, 0,
                ArenaMatchOutcome.Active, 0, []);
        bool first = ReferenceEquals(entry, match.First);
        return ArenaStateProjector.Project(first ? match.First.Entrant : match.Second.Entrant,
            first ? match.Second.Entrant : match.First.Entrant, first, match.Session,
            time.GetUtcNow(), afterSequence);
    }

    private sealed class Entry(ArenaTestEntrant entrant)
    {
        public ArenaTestEntrant Entrant { get; } = entrant;
        public Match? Match { get; set; }
    }

    private sealed record Match(ArenaCombatSession Session, Entry First, Entry Second);
}
