namespace Elyndor.Contracts.Raids;

public sealed record RaidMemberResponse(
    Guid CharacterId,
    string Name,
    int Level,
    string ClassId,
    string Role,
    string ReadyState,
    DateTimeOffset JoinedAtUtc);

public sealed record RaidReadyCheckResponse(
    Guid Id,
    Guid StartedByCharacterId,
    string State,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record RaidResponse(
    Guid RaidId,
    Guid LeaderCharacterId,
    int MaximumMembers,
    string State,
    long Version,
    IReadOnlyList<RaidMemberResponse> Members,
    RaidReadyCheckResponse? ReadyCheck);

public sealed record RaidInviteResponse(
    Guid Id,
    Guid RaidId,
    Guid InviterCharacterId,
    Guid TargetCharacterId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record CreateRaidRequest(Guid RequestId);

public sealed record InviteToRaidRequest(
    Guid InviteId,
    Guid TargetCharacterId);

public sealed record BeginRaidReadyCheckRequest(Guid ReadyCheckId);

public sealed record SetRaidReadyStateRequest(string State);
