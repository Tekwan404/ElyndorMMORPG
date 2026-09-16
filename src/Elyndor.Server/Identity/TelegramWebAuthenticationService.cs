using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Elyndor.Server.Identity;

public sealed record TelegramWebIdentity(
    long TelegramUserId,
    string? TelegramUsername);

public sealed record IssuedTelegramWebCredential(
    string Value,
    DateTimeOffset ExpiresAtUtc);

public static class TelegramWebAuthenticationService
{
    private const string TelegramIssuer = "https://oauth.telegram.org";
    private const string TelegramTokenEndpoint = "https://oauth.telegram.org/token";
    private const string TelegramJwksEndpoint =
        "https://oauth.telegram.org/.well-known/jwks.json";
    private const string WebCredentialPrefix = "web:";
    private const string WebCredentialAudience = "Elyndor.TelegramWebBootstrap";
    private const string PurposeClaim = "purpose";
    private const string PurposeValue = "telegram_web";

    public static bool IsWebCredential(string? value) =>
        value?.StartsWith(WebCredentialPrefix, StringComparison.Ordinal) == true;

    public static async Task<TelegramWebIdentity?> ExchangeCodeAsync(
        HttpClient httpClient,
        TelegramWebAuthenticationOptions options,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        using HttpRequestMessage tokenRequest = new(
            HttpMethod.Post,
            TelegramTokenEndpoint);
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}")));
        tokenRequest.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = options.RedirectUri,
                ["client_id"] = options.ClientId,
                ["code_verifier"] = codeVerifier
            });

        using HttpResponseMessage tokenResponse =
            await httpClient.SendAsync(tokenRequest, cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
            return null;

        await using Stream tokenStream =
            await tokenResponse.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument tokenDocument =
            await JsonDocument.ParseAsync(tokenStream, cancellationToken: cancellationToken);
        if (!tokenDocument.RootElement.TryGetProperty("id_token", out JsonElement idTokenElement))
            return null;

        string? idToken = idTokenElement.GetString();
        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        using HttpResponseMessage jwksResponse =
            await httpClient.GetAsync(TelegramJwksEndpoint, cancellationToken);
        jwksResponse.EnsureSuccessStatusCode();
        string jwksJson =
            await jwksResponse.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            JsonWebKeySet keySet = new(jwksJson);
            JwtSecurityTokenHandler handler = new()
            {
                MapInboundClaims = false
            };
            TokenValidationParameters validationParameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = TelegramIssuer,
                ValidateAudience = true,
                ValidAudience = options.ClientId,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keySet.GetSigningKeys(),
                ValidateLifetime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
            };

            ClaimsPrincipal principal = handler.ValidateToken(
                idToken,
                validationParameters,
                out SecurityToken validatedToken);
            if (validatedToken is not JwtSecurityToken jwt
                || !string.Equals(
                    jwt.Header.Alg,
                    SecurityAlgorithms.RsaSha256,
                    StringComparison.Ordinal))
            {
                return null;
            }

            string? telegramId = principal.FindFirst("id")?.Value;
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
            return new TelegramWebIdentity(
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

    public static IssuedTelegramWebCredential IssueCredential(
        AuthenticationOptions options,
        TimeProvider timeProvider,
        TelegramWebIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(identity);

        DateTimeOffset issuedAtUtc = timeProvider.GetUtcNow();
        DateTimeOffset expiresAtUtc = issuedAtUtc.AddHours(
            options.Telegram.Web.SessionLifetimeHours);
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
            WebCredentialAudience,
            claims,
            issuedAtUtc.UtcDateTime,
            expiresAtUtc.UtcDateTime,
            credentials);

        return new IssuedTelegramWebCredential(
            WebCredentialPrefix + new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }

    public static TelegramWebIdentity? ValidateCredential(
        AuthenticationOptions options,
        TimeProvider timeProvider,
        string credential)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (!IsWebCredential(credential))
            return null;

        string token = credential[WebCredentialPrefix.Length..];
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
                ValidAudience = WebCredentialAudience,
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
            return new TelegramWebIdentity(
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
