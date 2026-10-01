namespace Elyndor.Contracts.Arena;

public sealed record ArenaInviteRequest(Guid RequestId, string TargetName);
public sealed record ArenaInviteActionRequest(string Action);
public sealed record ArenaInvitationResponse(Guid Id, Guid InviterCharacterId, string InviterName,
    Guid TargetCharacterId, string TargetName, string Status, DateTimeOffset ExpiresAtUtc, Guid? MatchId,
    bool Incoming);
