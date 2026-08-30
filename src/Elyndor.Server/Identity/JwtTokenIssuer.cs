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

    public IssuedAccessToken Issue(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account ID cannot be empty.", nameof(accountId));
        }

        DateTimeOffset issuedAtUtc = _timeProvider.GetUtcNow();
        DateTimeOffset expiresAtUtc = issuedAtUtc.AddMinutes(
            AuthenticationOptions.AccessTokenLifetimeMinutes);
        SymmetricSecurityKey securityKey = new(
            Encoding.UTF8.GetBytes(_authenticationOptions.SigningKey));
        SigningCredentials credentials = new(
            securityKey,
            SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(
            _authenticationOptions.Issuer,
            _authenticationOptions.Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
            ],
            issuedAtUtc.UtcDateTime,
            expiresAtUtc.UtcDateTime,
            credentials);

        return new IssuedAccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }
}
