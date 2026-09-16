namespace Elyndor.Contracts.Identity;

public sealed record TelegramAuthenticationRequest(string InitData);

public sealed record TelegramWebAuthenticationConfigResponse(
    bool Enabled,
    string? ClientId,
    string? RedirectUri);

public sealed record TelegramWebAuthenticationRequest(
    string Code,
    string CodeVerifier);

public sealed record TelegramWebAuthenticationResponse(
    string WebCredential,
    DateTimeOffset ExpiresAtUtc);

public sealed record AuthenticationResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> Roles);

public sealed record ApiErrorResponse(
    string Code,
    string CorrelationId);
