namespace Elyndor.Contracts.Administration;

public sealed record AdminWebAuthenticationCodeRequest(long TelegramUserId);

public sealed record AdminWebAuthenticationChallengeResponse(
    Guid ChallengeId,
    DateTimeOffset ExpiresAtUtc);

public sealed record AdminWebAuthenticationVerifyRequest(
    Guid ChallengeId,
    long TelegramUserId,
    string Code);
