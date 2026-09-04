using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Elyndor.Server.Identity;

public sealed record IssuedAccessToken(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);

public sealed class JwtTokenIssuer(
    IOptions<AuthenticationOptions> authenticationOptions,
    TimeProvider timeProvider)
{
    private readonly AuthenticationOptions _authenticationOptions =
        authenticationOptions?.Value
        ?? throw new ArgumentNullException(nameof(authenticationOptions));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public IssuedAccessToken Issue(
        Guid accountId,
        long telegramUserId,
        IReadOnlyCollection<string>? roles = null)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account ID cannot be empty.", nameof(accountId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(telegramUserId);

        DateTimeOffset issuedAtUtc = _timeProvider.GetUtcNow();
        DateTimeOffset expiresAtUtc = issuedAtUtc.AddMinutes(
            AuthenticationOptions.AccessTokenLifetimeMinutes);
        SymmetricSecurityKey securityKey = new(
            Encoding.UTF8.GetBytes(_authenticationOptions.SigningKey));
        SigningCredentials credentials = new(
            securityKey,
            SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, accountId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(
                AuthenticationClaimTypes.TelegramUserId,
                telegramUserId.ToString(
                    global::System.Globalization.CultureInfo.InvariantCulture))
        ];

        foreach (string role in roles ?? [])
        {
            if (!string.IsNullOrWhiteSpace(role))
                claims.Add(new Claim(AuthenticationClaimTypes.Role, role.Trim()));
        }

        JwtSecurityToken token = new(
            _authenticationOptions.Issuer,
            _authenticationOptions.Audience,
            claims,
            issuedAtUtc.UtcDateTime,
            expiresAtUtc.UtcDateTime,
            credentials);

        return new IssuedAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }
}
