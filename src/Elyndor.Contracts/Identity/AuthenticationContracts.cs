namespace Elyndor.Contracts.Identity;

public sealed record TelegramAuthenticationRequest(string InitData);

public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> Roles);

public sealed record ApiErrorResponse(
    string Code,
    string CorrelationId);
