namespace Elyndor.Core.Combat.Participants;

public enum CombatParticipantStatus
{
    Rostered,
    Active,
    Fled,
    Dead,
    Completed
}

public sealed record CombatParticipantIdentity(
    Guid AccountId,
    Guid CharacterId,
    Guid ActorId);

public sealed record CombatParticipantSnapshot(
    Guid AccountId,
    Guid CharacterId,
    Guid ActorId,
    CombatParticipantStatus Status,
    DateTimeOffset RosteredAtUtc,
    DateTimeOffset? JoinedAtUtc,
    DateTimeOffset? FledAtUtc,
    DateTimeOffset? DiedAtUtc);

public static class CombatParticipantErrorCodes
{
    public const string NotInRoster = "combat_participant_not_in_roster";
    public const string AlreadyActive = "combat_participant_already_active";
    public const string AlreadyFled = "combat_participant_already_fled";
    public const string NoActiveParticipants = "combat_no_active_participants";
}

public sealed class CombatParticipantRoster
{
    public const int DefaultMaximumParticipants = 5;

    private readonly Dictionary<Guid, ParticipantState> _participants;

    public CombatParticipantRoster(
        IReadOnlyList<CombatParticipantIdentity> initialRoster,
        DateTimeOffset rosteredAtUtc,
        int maximumParticipants = DefaultMaximumParticipants)
    {
        ArgumentNullException.ThrowIfNull(initialRoster);
        if (initialRoster.Count == 0)
            throw new ArgumentException("A combat roster requires at least one participant.", nameof(initialRoster));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumParticipants);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            maximumParticipants,
            DefaultMaximumParticipants);
        if (initialRoster.Count > maximumParticipants)
            throw new ArgumentException("The combat roster exceeds its participant limit.", nameof(initialRoster));
        if (initialRoster.Select(item => item.CharacterId).Distinct().Count() != initialRoster.Count
            || initialRoster.Select(item => item.AccountId).Distinct().Count() != initialRoster.Count
            || initialRoster.Select(item => item.ActorId).Distinct().Count() != initialRoster.Count)
        {
            throw new ArgumentException("Combat participant identifiers must be unique.", nameof(initialRoster));
        }

        _participants = initialRoster.ToDictionary(
            item => item.CharacterId,
            item => new ParticipantState(item, rosteredAtUtc));
        RosteredAtUtc = rosteredAtUtc;
        MaximumParticipants = maximumParticipants;
    }

    public DateTimeOffset RosteredAtUtc { get; }
    public int MaximumParticipants { get; }
    public int ActiveCount => _participants.Values.Count(item => item.Status is CombatParticipantStatus.Active);
    public IReadOnlyList<CombatParticipantSnapshot> Participants =>
        _participants.Values
            .OrderBy(item => item.Identity.CharacterId)
            .Select(item => item.ToSnapshot())
            .ToArray();

    public bool Contains(Guid characterId) => _participants.ContainsKey(characterId);

    public CombatParticipantStatus? GetStatus(Guid characterId) =>
        _participants.TryGetValue(characterId, out ParticipantState? participant)
            ? participant.Status
            : null;

    public bool TryAttach(Guid characterId, DateTimeOffset now, out string? errorCode)
    {
        if (!_participants.TryGetValue(characterId, out ParticipantState? participant))
        {
            errorCode = CombatParticipantErrorCodes.NotInRoster;
            return false;
        }

        if (participant.Status is CombatParticipantStatus.Fled)
        {
            errorCode = CombatParticipantErrorCodes.AlreadyFled;
            return false;
        }

        if (participant.Status is CombatParticipantStatus.Active or CombatParticipantStatus.Dead)
        {
            errorCode = CombatParticipantErrorCodes.AlreadyActive;
            return false;
        }

        participant.Status = CombatParticipantStatus.Active;
        participant.JoinedAtUtc ??= now;
        errorCode = null;
        return true;
    }

    public bool TryFlee(Guid characterId, DateTimeOffset now, out string? errorCode)
    {
        if (!_participants.TryGetValue(characterId, out ParticipantState? participant))
        {
            errorCode = CombatParticipantErrorCodes.NotInRoster;
            return false;
        }

        if (participant.Status is CombatParticipantStatus.Fled)
        {
            errorCode = CombatParticipantErrorCodes.AlreadyFled;
            return false;
        }

        if (participant.Status is not CombatParticipantStatus.Active)
        {
            errorCode = CombatParticipantErrorCodes.NotInRoster;
            return false;
        }

        participant.Status = CombatParticipantStatus.Fled;
        participant.FledAtUtc = now;
        errorCode = null;
        return true;
    }

    public bool TryMarkDead(Guid characterId, DateTimeOffset now)
    {
        if (!_participants.TryGetValue(characterId, out ParticipantState? participant)
            || participant.Status is not CombatParticipantStatus.Active)
            return false;

        participant.Status = CombatParticipantStatus.Dead;
        participant.DiedAtUtc = now;
        return true;
    }

    public bool HasActiveParticipants() =>
        _participants.Values.Any(item => item.Status is CombatParticipantStatus.Active);

    public bool IsEligibleRosterMember(Guid characterId) =>
        _participants.TryGetValue(characterId, out ParticipantState? participant)
        && participant.Status is not CombatParticipantStatus.Fled;

    private sealed class ParticipantState(
        CombatParticipantIdentity identity,
        DateTimeOffset rosteredAtUtc)
    {
        public CombatParticipantIdentity Identity { get; } = identity;
        public DateTimeOffset RosteredAtUtc { get; } = rosteredAtUtc;
        public CombatParticipantStatus Status { get; set; } = CombatParticipantStatus.Rostered;
        public DateTimeOffset? JoinedAtUtc { get; set; }
        public DateTimeOffset? FledAtUtc { get; set; }
        public DateTimeOffset? DiedAtUtc { get; set; }

        public CombatParticipantSnapshot ToSnapshot() => new(
            Identity.AccountId,
            Identity.CharacterId,
            Identity.ActorId,
            Status,
            RosteredAtUtc,
            JoinedAtUtc,
            FledAtUtc,
            DiedAtUtc);
    }
}
