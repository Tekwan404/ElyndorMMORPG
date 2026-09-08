namespace Elyndor.Contracts.Parties;

public sealed record PartyMemberResponse(
    Guid CharacterId,
    string Name,
    int Level,
    string ClassId,
    bool IsLeader,
    DateTimeOffset JoinedAtUtc);

public sealed record PartyResponse(
    Guid PartyId,
    Guid LeaderCharacterId,
    long Version,
    IReadOnlyList<PartyMemberResponse> Members);

public sealed record PartyInviteResponse(
    Guid Id,
    Guid PartyId,
    Guid InviterCharacterId,
    Guid TargetCharacterId,
    string Mode,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record CreatePartyRequest(Guid RequestId);

public sealed record InviteToPartyRequest(
    Guid InviteId,
    Guid TargetCharacterId,
    string Mode = "Friend");
