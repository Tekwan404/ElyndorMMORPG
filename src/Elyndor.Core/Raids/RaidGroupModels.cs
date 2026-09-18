namespace Elyndor.Core.Raids;

public enum RaidState
{
    Active,
    Disbanded
}

public enum RaidMemberRole
{
    Leader,
    Assistant,
    Member
}

public enum RaidMemberState
{
    Active,
    Left,
    Kicked
}

public enum RaidInviteStatus
{
    Pending,
    Accepted,
    Declined,
    Expired,
    Cancelled
}

public enum RaidReadyCheckState
{
    Open,
    Completed,
    Expired
}

public enum RaidReadyState
{
    NoResponse,
    Ready,
    NotReady
}

public sealed class RaidGroup
{
    public const int DefaultMaximumMembers = 20;

    private readonly List<RaidMember> _members = [];

    private RaidGroup()
    {
    }

    private RaidGroup(
        Guid id,
        Guid creationRequestId,
        Guid leaderCharacterId,
        int maximumMembers,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || creationRequestId == Guid.Empty || leaderCharacterId == Guid.Empty)
            throw new ArgumentException("Raid identifiers cannot be empty.");
        if (maximumMembers <= 0 || maximumMembers > DefaultMaximumMembers)
            throw new ArgumentOutOfRangeException(nameof(maximumMembers));
        EnsureUtc(createdAtUtc);

        Id = id;
        CreationRequestId = creationRequestId;
        LeaderCharacterId = leaderCharacterId;
        MaximumMembers = maximumMembers;
        CreatedAtUtc = createdAtUtc;
        State = RaidState.Active;
        Version = 1;
        _members.Add(RaidMember.Create(id, leaderCharacterId, RaidMemberRole.Leader, createdAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid CreationRequestId { get; private set; }
    public Guid LeaderCharacterId { get; private set; }
    public int MaximumMembers { get; private set; }
    public RaidState State { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<RaidMember> Members => _members;

    public static RaidGroup Create(
        Guid id,
        Guid creationRequestId,
        Guid leaderCharacterId,
        int maximumMembers,
        DateTimeOffset createdAtUtc) =>
        new(id, creationRequestId, leaderCharacterId, maximumMembers, createdAtUtc);

    public void AddMember(Guid characterId, DateTimeOffset joinedAtUtc)
    {
        EnsureActive();
        if (characterId == Guid.Empty)
            throw new ArgumentException("Raid member identifier cannot be empty.", nameof(characterId));
        if (_members.Any(member => member.CharacterId == characterId))
            throw new InvalidOperationException("Character is already in the raid.");
        if (_members.Count >= MaximumMembers)
            throw new InvalidOperationException("Raid is full.");
        EnsureUtc(joinedAtUtc);

        _members.Add(RaidMember.Create(Id, characterId, RaidMemberRole.Member, joinedAtUtc));
        Version++;
    }

    private void EnsureActive()
    {
        if (State != RaidState.Active)
            throw new InvalidOperationException("Raid is not active.");
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Raid timestamps must be UTC.", nameof(value));
    }
}

public sealed class RaidMember
{
    private RaidMember()
    {
    }

    private RaidMember(
        Guid raidId,
        Guid characterId,
        RaidMemberRole role,
        DateTimeOffset joinedAtUtc)
    {
        RaidId = raidId;
        CharacterId = characterId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
        State = RaidMemberState.Active;
    }

    public Guid RaidId { get; private set; }
    public Guid CharacterId { get; private set; }
    public RaidMemberRole Role { get; private set; }
    public RaidMemberState State { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }

    internal static RaidMember Create(
        Guid raidId,
        Guid characterId,
        RaidMemberRole role,
        DateTimeOffset joinedAtUtc) =>
        new(raidId, characterId, role, joinedAtUtc);
}

public sealed class RaidInvite
{
    private RaidInvite()
    {
    }

    private RaidInvite(
        Guid id,
        Guid raidId,
        Guid inviterCharacterId,
        Guid targetCharacterId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (id == Guid.Empty || raidId == Guid.Empty || inviterCharacterId == Guid.Empty
            || targetCharacterId == Guid.Empty)
            throw new ArgumentException("Raid invite identifiers cannot be empty.");
        if (inviterCharacterId == targetCharacterId)
            throw new ArgumentException("A character cannot invite itself to a raid.");
        if (createdAtUtc.Offset != TimeSpan.Zero || expiresAtUtc.Offset != TimeSpan.Zero
            || expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Raid invite timestamps are invalid.");

        Id = id;
        RaidId = raidId;
        InviterCharacterId = inviterCharacterId;
        TargetCharacterId = targetCharacterId;
        Status = RaidInviteStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid RaidId { get; private set; }
    public Guid InviterCharacterId { get; private set; }
    public Guid TargetCharacterId { get; private set; }
    public RaidInviteStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public static RaidInvite Create(
        Guid id,
        Guid raidId,
        Guid inviterCharacterId,
        Guid targetCharacterId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc) =>
        new(id, raidId, inviterCharacterId, targetCharacterId, createdAtUtc, expiresAtUtc);

    public void Accept(Guid actorCharacterId, DateTimeOffset decidedAtUtc)
    {
        EnsurePendingTarget(actorCharacterId, decidedAtUtc);
        Status = RaidInviteStatus.Accepted;
        DecidedAtUtc = decidedAtUtc;
    }

    private void EnsurePendingTarget(Guid actorCharacterId, DateTimeOffset decidedAtUtc)
    {
        if (Status != RaidInviteStatus.Pending)
            throw new InvalidOperationException("Only pending raid invites can be accepted.");
        if (actorCharacterId != TargetCharacterId)
            throw new UnauthorizedAccessException("Only the target can accept a raid invite.");
        if (decidedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Raid invite timestamps must be UTC.", nameof(decidedAtUtc));
        if (decidedAtUtc >= ExpiresAtUtc)
        {
            Status = RaidInviteStatus.Expired;
            throw new InvalidOperationException("Raid invite has expired.");
        }
    }
}

public sealed class RaidReadyCheck
{
    private RaidReadyCheck()
    {
    }

    public Guid Id { get; private set; }
    public Guid RaidId { get; private set; }
    public Guid StartedByCharacterId { get; private set; }
    public RaidReadyCheckState State { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
}
