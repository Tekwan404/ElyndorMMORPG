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

    public RaidMember Leave(Guid characterId, DateTimeOffset leftAtUtc)
    {
        EnsureActive();
        EnsureUtc(leftAtUtc);
        RaidMember member = FindMember(characterId);
        member.MarkLeft(leftAtUtc);
        _members.Remove(member);

        if (_members.Count == 0)
        {
            State = RaidState.Disbanded;
        }
        else if (characterId == LeaderCharacterId)
        {
            RaidMember nextLeader = _members
                .OrderBy(candidate => candidate.Role == RaidMemberRole.Assistant ? 0 : 1)
                .ThenBy(candidate => candidate.JoinedAtUtc)
                .ThenBy(candidate => candidate.CharacterId)
                .First();
            nextLeader.SetRole(RaidMemberRole.Leader);
            LeaderCharacterId = nextLeader.CharacterId;
        }

        Version++;
        return member;
    }

    public RaidMember Kick(
        Guid leaderCharacterId,
        Guid targetCharacterId,
        DateTimeOffset kickedAtUtc)
    {
        EnsureLeader(leaderCharacterId);
        EnsureUtc(kickedAtUtc);
        if (targetCharacterId == LeaderCharacterId)
            throw new InvalidOperationException("Raid leader cannot kick itself.");

        RaidMember member = FindMember(targetCharacterId);
        member.MarkKicked(kickedAtUtc);
        _members.Remove(member);
        Version++;
        return member;
    }

    public void PromoteAssistant(Guid leaderCharacterId, Guid targetCharacterId)
    {
        EnsureLeader(leaderCharacterId);
        RaidMember target = FindMember(targetCharacterId);
        if (target.Role == RaidMemberRole.Leader)
            throw new InvalidOperationException("Raid leader cannot be promoted to assistant.");

        target.SetRole(RaidMemberRole.Assistant);
        Version++;
    }

    public void TransferLeadership(Guid leaderCharacterId, Guid targetCharacterId)
    {
        EnsureLeader(leaderCharacterId);
        if (targetCharacterId == LeaderCharacterId)
            throw new InvalidOperationException("Target is already the raid leader.");

        RaidMember currentLeader = FindMember(leaderCharacterId);
        RaidMember target = FindMember(targetCharacterId);
        currentLeader.SetRole(RaidMemberRole.Member);
        target.SetRole(RaidMemberRole.Leader);
        LeaderCharacterId = targetCharacterId;
        Version++;
    }

    public void Disband(Guid leaderCharacterId)
    {
        EnsureLeader(leaderCharacterId);
        _members.Clear();
        State = RaidState.Disbanded;
        Version++;
    }

    public void BeginReadyCheck(Guid actorCharacterId)
    {
        EnsureLeaderOrAssistant(actorCharacterId);
        foreach (RaidMember member in _members)
            member.ResetReadyState();
        Version++;
    }

    public void SetReadyState(Guid characterId, RaidReadyState readyState)
    {
        EnsureActive();
        if (readyState == RaidReadyState.NoResponse)
            throw new ArgumentException("Ready state must be Ready or NotReady.", nameof(readyState));

        FindMember(characterId).SetReadyState(readyState);
        Version++;
    }

    private RaidMember FindMember(Guid characterId) =>
        _members.SingleOrDefault(member => member.CharacterId == characterId)
        ?? throw new InvalidOperationException("Character is not in the raid.");

    private void EnsureLeader(Guid characterId)
    {
        EnsureActive();
        if (LeaderCharacterId != characterId)
            throw new UnauthorizedAccessException("Only the raid leader can perform this action.");
    }

    private void EnsureLeaderOrAssistant(Guid characterId)
    {
        EnsureActive();
        RaidMember member = FindMember(characterId);
        if (member.Role is not (RaidMemberRole.Leader or RaidMemberRole.Assistant))
            throw new UnauthorizedAccessException("Only the raid leader or an assistant can perform this action.");
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
        ReadyState = RaidReadyState.NoResponse;
    }

    public Guid RaidId { get; private set; }
    public Guid CharacterId { get; private set; }
    public RaidMemberRole Role { get; private set; }
    public RaidMemberState State { get; private set; }
    public RaidReadyState ReadyState { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }

    internal static RaidMember Create(
        Guid raidId,
        Guid characterId,
        RaidMemberRole role,
        DateTimeOffset joinedAtUtc) =>
        new(raidId, characterId, role, joinedAtUtc);

    internal void SetRole(RaidMemberRole role) => Role = role;

    internal void ResetReadyState() => ReadyState = RaidReadyState.NoResponse;

    internal void SetReadyState(RaidReadyState readyState) => ReadyState = readyState;

    internal void MarkLeft(DateTimeOffset timestamp)
    {
        EnsureUtc(timestamp);
        State = RaidMemberState.Left;
    }

    internal void MarkKicked(DateTimeOffset timestamp)
    {
        EnsureUtc(timestamp);
        State = RaidMemberState.Kicked;
    }

    private static void EnsureUtc(DateTimeOffset timestamp)
    {
        if (timestamp.Offset != TimeSpan.Zero)
            throw new ArgumentException("Raid timestamps must be UTC.", nameof(timestamp));
    }
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

    public void Decline(Guid actorCharacterId, DateTimeOffset decidedAtUtc)
    {
        EnsurePendingTarget(actorCharacterId, decidedAtUtc);
        Status = RaidInviteStatus.Declined;
        DecidedAtUtc = decidedAtUtc;
    }

    public void Expire(DateTimeOffset now)
    {
        EnsureUtc(now);
        if (Status == RaidInviteStatus.Pending && now >= ExpiresAtUtc)
            Status = RaidInviteStatus.Expired;
    }

    private void EnsurePendingTarget(Guid actorCharacterId, DateTimeOffset decidedAtUtc)
    {
        if (Status != RaidInviteStatus.Pending)
            throw new InvalidOperationException("Only pending raid invites can be decided.");
        if (actorCharacterId != TargetCharacterId)
            throw new UnauthorizedAccessException("Only the target can decide a raid invite.");
        EnsureUtc(decidedAtUtc);
        if (decidedAtUtc >= ExpiresAtUtc)
        {
            Status = RaidInviteStatus.Expired;
            throw new InvalidOperationException("Raid invite has expired.");
        }
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Raid invite timestamps must be UTC.", nameof(value));
    }
}

public sealed class RaidReadyCheck
{
    private RaidReadyCheck()
    {
    }

    private RaidReadyCheck(
        Guid id,
        Guid raidId,
        Guid startedByCharacterId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (id == Guid.Empty || raidId == Guid.Empty || startedByCharacterId == Guid.Empty)
            throw new ArgumentException("Raid ready-check identifiers cannot be empty.");
        EnsureUtc(startedAtUtc);
        EnsureUtc(expiresAtUtc);
        if (expiresAtUtc <= startedAtUtc)
            throw new ArgumentException("Raid ready check must expire after it starts.");

        Id = id;
        RaidId = raidId;
        StartedByCharacterId = startedByCharacterId;
        State = RaidReadyCheckState.Open;
        StartedAtUtc = startedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid RaidId { get; private set; }
    public Guid StartedByCharacterId { get; private set; }
    public RaidReadyCheckState State { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public static RaidReadyCheck Create(
        Guid id,
        Guid raidId,
        Guid startedByCharacterId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset expiresAtUtc) =>
        new(id, raidId, startedByCharacterId, startedAtUtc, expiresAtUtc);

    public void Complete(DateTimeOffset completedAtUtc)
    {
        EnsureUtc(completedAtUtc);
        if (State != RaidReadyCheckState.Open)
            throw new InvalidOperationException("Only an open ready check can be completed.");
        if (completedAtUtc < StartedAtUtc || completedAtUtc >= ExpiresAtUtc)
            throw new InvalidOperationException("Ready check cannot complete outside its active window.");

        State = RaidReadyCheckState.Completed;
        CompletedAtUtc = completedAtUtc;
    }

    public void Expire(DateTimeOffset now)
    {
        EnsureUtc(now);
        if (State == RaidReadyCheckState.Open && now >= ExpiresAtUtc)
            State = RaidReadyCheckState.Expired;
    }

    public void Cancel(DateTimeOffset now)
    {
        EnsureUtc(now);
        if (State == RaidReadyCheckState.Open)
            State = RaidReadyCheckState.Expired;
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Raid ready-check timestamps must be UTC.", nameof(value));
    }
}
