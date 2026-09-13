using Elyndor.Contracts.Dungeons;

namespace Elyndor.Contracts.Parties;

public sealed record PartyMemberResponse(
    Guid CharacterId,
    string Name,
    int Level,
    string ClassId,
    bool IsLeader,
    DateTimeOffset JoinedAtUtc,
    string? LocationId,
    Guid? ActiveDungeonRunId);

public sealed record PartyResponse(
    Guid PartyId,
    Guid LeaderCharacterId,
    long Version,
    IReadOnlyList<PartyMemberResponse> Members,
    Guid? ActiveDungeonRunId);

public sealed record PartyDungeonStateResponse(
    PartyResponse? Party,
    DungeonRunResponse? CurrentRun,
    Guid CharacterId,
    string? LocationId,
    bool NeedsEntry,
    bool CanEnter,
    string? EnterBlockedReason);

public sealed record PartyInviteResponse(
    Guid Id,
    Guid PartyId,
    Guid InviterCharacterId,
    Guid TargetCharacterId,
    string Mode,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string? InviterName = null);

public sealed record CreatePartyRequest(Guid RequestId);

public sealed record InviteToPartyRequest(
    Guid InviteId,
    Guid TargetCharacterId,
    string Mode = "Friend");
