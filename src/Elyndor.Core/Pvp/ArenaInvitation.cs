namespace Elyndor.Core.Pvp;

public enum ArenaInvitationStatus { Pending, Accepted, Declined, Cancelled, Expired }

public sealed class ArenaInvitation
{
    private ArenaInvitation() { }

    public ArenaInvitation(Guid id, Guid inviterCharacterId, Guid targetCharacterId, DateTimeOffset now)
    {
        if (id == Guid.Empty || inviterCharacterId == Guid.Empty || targetCharacterId == Guid.Empty
            || inviterCharacterId == targetCharacterId || now.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid arena invitation.");
        Id = id;
        InviterCharacterId = inviterCharacterId;
        TargetCharacterId = targetCharacterId;
        CreatedAtUtc = now;
        ExpiresAtUtc = now.AddMinutes(5);
    }

    public Guid Id { get; private set; }
    public Guid InviterCharacterId { get; private set; }
    public Guid TargetCharacterId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public ArenaInvitationStatus Status { get; private set; }
    public Guid? MatchId { get; private set; }

    public ArenaInvitationStatus StatusAt(DateTimeOffset now) =>
        Status == ArenaInvitationStatus.Pending && now >= ExpiresAtUtc ? ArenaInvitationStatus.Expired : Status;

    public bool Accept(Guid matchId, DateTimeOffset now)
    {
        if (matchId == Guid.Empty) throw new ArgumentException("Match id is required.");
        if (!IsPending(now)) return false;
        MatchId = matchId;
        Status = ArenaInvitationStatus.Accepted;
        return true;
    }

    public bool Close(ArenaInvitationStatus status, DateTimeOffset now)
    {
        if (status is not (ArenaInvitationStatus.Declined or ArenaInvitationStatus.Cancelled))
            throw new ArgumentException("Invalid invitation action.");
        if (!IsPending(now)) return false;
        Status = status;
        return true;
    }

    private bool IsPending(DateTimeOffset now)
    {
        Status = StatusAt(now);
        return Status == ArenaInvitationStatus.Pending;
    }
}
