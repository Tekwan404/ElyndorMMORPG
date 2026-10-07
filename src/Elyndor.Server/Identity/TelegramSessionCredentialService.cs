using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Elyndor.Server.Identity;

public sealed record TelegramSessionIdentity(
    long TelegramUserId,
    string? TelegramUsername);

public sealed record IssuedTelegramSessionCredential(
    string Value,
    DateTimeOffset ExpiresAtUtc);

public static class TelegramSessionCredentialService
{
    private const string CredentialPrefix = "session:";
    private const string CredentialAudience = "Elyndor.TelegramSession";
    private const string PurposeClaim = "purpose";
    private const string PurposeValue = "telegram_session";

    public static bool IsSessionCredential(string? value) =>
        value?.StartsWith(CredentialPrefix, StringComparison.Ordinal) == true;

    public static IssuedTelegramSessionCredential IssueCredential(
        AuthenticationOptions options,
        TimeProvider timeProvider,
        TelegramSessionIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(identity);

        DateTimeOffset issuedAtUtc = timeProvider.GetUtcNow();
        DateTimeOffset expiresAtUtc = issuedAtUtc.AddHours(
            options.Telegram.SessionLifetimeHours);
        SymmetricSecurityKey securityKey = new(
            Encoding.UTF8.GetBytes(options.SigningKey));
        SigningCredentials credentials = new(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            new(
                JwtRegisteredClaimNames.Sub,
                identity.TelegramUserId.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(PurposeClaim, PurposeValue)
        ];
        if (!string.IsNullOrWhiteSpace(identity.TelegramUsername))
            claims.Add(new Claim("preferred_username", identity.TelegramUsername));

        JwtSecurityToken token = new(
            options.Issuer,
            CredentialAudience,
            claims,
            issuedAtUtc.UtcDateTime,
            expiresAtUtc.UtcDateTime,
            credentials);

        return new IssuedTelegramSessionCredential(
            CredentialPrefix + new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }

    public static TelegramSessionIdentity? ValidateCredential(
        AuthenticationOptions options,
        TimeProvider timeProvider,
        string credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (!IsSessionCredential(credential))
            return null;

        string token = credential[CredentialPrefix.Length..];
        if (string.IsNullOrWhiteSpace(token))
            return null;

        try
        {
            JwtSecurityTokenHandler handler = new()
            {
                MapInboundClaims = false
            };
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = options.Issuer,
                ValidateAudience = true,
                ValidAudience = CredentialAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(options.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(
                    AuthenticationOptions.TokenValidationClockSkewSeconds),
                LifetimeValidator = (notBefore, expires, _, parameters) =>
                    ValidateLifetime(
                        notBefore,
                        expires,
                        timeProvider,
                        parameters.ClockSkew)
            };

            ClaimsPrincipal principal = handler.ValidateToken(
                token,
                validationParameters,
                out _);
            if (!string.Equals(
                    principal.FindFirst(PurposeClaim)?.Value,
                    PurposeValue,
                    StringComparison.Ordinal))
            {
                return null;
            }

            string? telegramId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!long.TryParse(
                    telegramId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long telegramUserId)
                || telegramUserId <= 0)
            {
                return null;
            }

            string? username = principal.FindFirst("preferred_username")?.Value;
            return new TelegramSessionIdentity(
                telegramUserId,
                string.IsNullOrWhiteSpace(username) ? null : username);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool ValidateLifetime(
        DateTime? notBefore,
        DateTime? expires,
        TimeProvider timeProvider,
        TimeSpan clockSkew)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        return expires.HasValue
            && expires.Value >= utcNow - clockSkew
            && (!notBefore.HasValue || notBefore.Value <= utcNow + clockSkew);
    }
}
