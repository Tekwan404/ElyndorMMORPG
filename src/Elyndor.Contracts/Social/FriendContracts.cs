namespace Elyndor.Contracts.Social;

public sealed record PlayerSearchResponse(
    Guid CharacterId,
    string Name,
    int Level,
    string ClassId,
    string PublicCode,
    string? TelegramUsername,
    string Relationship,
    Guid? PendingRequestId);

public sealed record FriendProfileResponse(
    Guid CharacterId,
    string Name,
    int Level,
    string ClassId,
    string PublicCode,
    string? TelegramUsername);

public sealed record FriendRequestResponse(
    Guid Id,
    Guid RequesterCharacterId,
    Guid TargetCharacterId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    string? RequesterName = null,
    string? TargetName = null);

public sealed record FriendsSnapshotResponse(
    IReadOnlyList<FriendProfileResponse> Friends,
    IReadOnlyList<FriendRequestResponse> IncomingRequests,
    IReadOnlyList<FriendRequestResponse> OutgoingRequests);

public sealed record SendFriendRequestRequest(Guid RequestId, Guid TargetCharacterId);
