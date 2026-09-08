namespace Elyndor.Core.Parties;

public enum PartyState
{
    Active,
    Disbanded
}

public enum PartyMemberState
{
    Active,
    Left,
    Kicked
}

public enum PartyInviteStatus
{
    Pending,
    Accepted,
    Declined,
    Expired,
    Cancelled
}

public enum PartyInviteMode
{
    Friend,
    Direct
}

public sealed class Party
{
    public const int MaxPartySize = 5;

    private readonly List<PartyMember> _members = [];

    private Party()
    {
    }

    private Party(
        Guid id,
        Guid creationRequestId,
        Guid leaderCharacterId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || creationRequestId == Guid.Empty || leaderCharacterId == Guid.Empty)
            throw new ArgumentException("Party identifiers cannot be empty.");
        if (createdAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Party timestamps must be UTC.", nameof(createdAtUtc));

        Id = id;
        CreationRequestId = creationRequestId;
        LeaderCharacterId = leaderCharacterId;
        CreatedAtUtc = createdAtUtc;
        State = PartyState.Active;
        Version = 1;
        _members.Add(PartyMember.Create(id, leaderCharacterId, createdAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid CreationRequestId { get; private set; }
    public Guid LeaderCharacterId { get; private set; }
    public PartyState State { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<PartyMember> Members => _members;

    public static Party Create(
        Guid id,
        Guid creationRequestId,
        Guid leaderCharacterId,
        DateTimeOffset createdAtUtc) =>
        new(id, creationRequestId, leaderCharacterId, createdAtUtc);

    public void AddMember(Guid characterId, DateTimeOffset joinedAtUtc)
    {
        EnsureActive();
        if (characterId == Guid.Empty)
            throw new ArgumentException("Party member identifier cannot be empty.", nameof(characterId));
        if (_members.Any(member => member.CharacterId == characterId))
            throw new InvalidOperationException("Character is already in the party.");
        if (_members.Count >= MaxPartySize)
            throw new InvalidOperationException("Party is full.");
        if (joinedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Party timestamps must be UTC.", nameof(joinedAtUtc));

        _members.Add(PartyMember.Create(Id, characterId, joinedAtUtc));
        Version++;
    }

    public void Leave(Guid characterId, DateTimeOffset leftAtUtc)
    {
        EnsureActive();
        PartyMember member = FindMember(characterId);
        member.MarkLeft(leftAtUtc);
        _members.Remove(member);

        if (characterId == LeaderCharacterId && _members.Count > 0)
        {
            LeaderCharacterId = _members
                .OrderBy(candidate => candidate.JoinedAtUtc)
                .ThenBy(candidate => candidate.CharacterId)
                .First()
                .CharacterId;
        }
        else if (_members.Count == 0)
        {
            State = PartyState.Disbanded;
        }

        Version++;
    }

    public void Kick(Guid leaderCharacterId, Guid targetCharacterId, DateTimeOffset kickedAtUtc)
    {
        EnsureLeader(leaderCharacterId);
        if (targetCharacterId == LeaderCharacterId)
            throw new InvalidOperationException("Leader cannot kick itself.");

        PartyMember member = FindMember(targetCharacterId);
        member.MarkKicked(kickedAtUtc);
        _members.Remove(member);
        Version++;
    }

    public void TransferLeadership(Guid leaderCharacterId, Guid targetCharacterId)
    {
        EnsureLeader(leaderCharacterId);
        FindMember(targetCharacterId);
        LeaderCharacterId = targetCharacterId;
        Version++;
    }

    public void Disband(Guid leaderCharacterId)
    {
        EnsureLeader(leaderCharacterId);
        _members.Clear();
        State = PartyState.Disbanded;
        Version++;
    }

    private PartyMember FindMember(Guid characterId) =>
        _members.SingleOrDefault(member => member.CharacterId == characterId)
        ?? throw new InvalidOperationException("Character is not in the party.");

    private void EnsureLeader(Guid characterId)
    {
        EnsureActive();
        if (LeaderCharacterId != characterId)
            throw new UnauthorizedAccessException("Only the party leader can perform this action.");
    }

    private void EnsureActive()
    {
        if (State != PartyState.Active)
            throw new InvalidOperationException("Party is not active.");
    }
}

public sealed class PartyMember
{
    private PartyMember()
    {
    }

    private PartyMember(Guid partyId, Guid characterId, DateTimeOffset joinedAtUtc)
    {
        PartyId = partyId;
        CharacterId = characterId;
        JoinedAtUtc = joinedAtUtc;
        State = PartyMemberState.Active;
    }

    public Guid PartyId { get; private set; }
    public Guid CharacterId { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }
    public PartyMemberState State { get; private set; }

    public static PartyMember Create(Guid partyId, Guid characterId, DateTimeOffset joinedAtUtc) =>
        new(partyId, characterId, joinedAtUtc);

    public void MarkLeft(DateTimeOffset timestamp)
    {
        EnsureUtc(timestamp);
        State = PartyMemberState.Left;
    }

    public void MarkKicked(DateTimeOffset timestamp)
    {
        EnsureUtc(timestamp);
        State = PartyMemberState.Kicked;
    }

    private static void EnsureUtc(DateTimeOffset timestamp)
    {
        if (timestamp.Offset != TimeSpan.Zero)
            throw new ArgumentException("Party timestamps must be UTC.", nameof(timestamp));
    }
}

public sealed class PartyInvite
{
    private PartyInvite()
    {
    }

    private PartyInvite(
        Guid id,
        Guid partyId,
        Guid inviterCharacterId,
        Guid targetCharacterId,
        PartyInviteMode mode,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (id == Guid.Empty || partyId == Guid.Empty || inviterCharacterId == Guid.Empty
            || targetCharacterId == Guid.Empty)
            throw new ArgumentException("Party invite identifiers cannot be empty.");
        if (inviterCharacterId == targetCharacterId)
            throw new ArgumentException("A character cannot invite itself to a party.");
        if (createdAtUtc.Offset != TimeSpan.Zero || expiresAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Party invite timestamps must be UTC.");
        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Party invite must expire after it is created.");

        Id = id;
        PartyId = partyId;
        InviterCharacterId = inviterCharacterId;
        TargetCharacterId = targetCharacterId;
        Mode = mode;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = PartyInviteStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid PartyId { get; private set; }
    public Guid InviterCharacterId { get; private set; }
    public Guid TargetCharacterId { get; private set; }
    public PartyInviteMode Mode { get; private set; }
    public PartyInviteStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public static PartyInvite Create(
        Guid id,
        Guid partyId,
        Guid inviterCharacterId,
        Guid targetCharacterId,
        PartyInviteMode mode,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc) =>
        new(id, partyId, inviterCharacterId, targetCharacterId, mode, createdAtUtc, expiresAtUtc);

    public void Accept(Guid actorCharacterId, DateTimeOffset decidedAtUtc)
    {
        EnsurePendingTarget(actorCharacterId, decidedAtUtc);
        Status = PartyInviteStatus.Accepted;
        DecidedAtUtc = decidedAtUtc;
    }

    public void Decline(Guid actorCharacterId, DateTimeOffset decidedAtUtc)
    {
        EnsurePendingTarget(actorCharacterId, decidedAtUtc);
        Status = PartyInviteStatus.Declined;
        DecidedAtUtc = decidedAtUtc;
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status == PartyInviteStatus.Pending && now >= ExpiresAtUtc)
            Status = PartyInviteStatus.Expired;
    }

    private void EnsurePendingTarget(Guid actorCharacterId, DateTimeOffset decidedAtUtc)
    {
        if (Status != PartyInviteStatus.Pending)
            throw new InvalidOperationException("Only pending party invites can be decided.");
        if (actorCharacterId != TargetCharacterId)
            throw new UnauthorizedAccessException("Only the target can decide a party invite.");
        if (decidedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Party invite timestamps must be UTC.", nameof(decidedAtUtc));
        if (decidedAtUtc >= ExpiresAtUtc)
        {
            Status = PartyInviteStatus.Expired;
            throw new InvalidOperationException("Party invite has expired.");
        }
    }
}
