using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elyndor.Server.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Elyndor.IntegrationTests.Identity;

public sealed class TelegramWebAuthenticationServiceTests
{
    private const string ClientId = "123456789";
    private const string ClientSecret = "test-client-secret";
    private const string RedirectUri = "https://elyndor.test/world";

    [Fact]
    public async Task ExchangeCodeValidatesTelegramIdTokenAndReturnsIdentity()
    {
        using RSA rsa = RSA.Create(2048);
        const string keyId = "telegram-test-key";
        RsaSecurityKey signingKey = new(rsa) { KeyId = keyId };
        DateTime utcNow = DateTime.UtcNow;
        JwtSecurityToken idToken = new(
            issuer: "https://oauth.telegram.org",
            audience: ClientId,
            claims:
            [
                new Claim("id", "424242"),
                new Claim("preferred_username", "elyndor_player")
            ],
            notBefore: utcNow.AddMinutes(-1),
            expires: utcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                signingKey,
                SecurityAlgorithms.RsaSha256));
        string encodedIdToken =
            new JwtSecurityTokenHandler().WriteToken(idToken);

        RSAParameters publicParameters = rsa.ExportParameters(false);
        string jwksJson = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = keyId,
                    alg = "RS256",
                    n = Base64UrlEncoder.Encode(publicParameters.Modulus!),
                    e = Base64UrlEncoder.Encode(publicParameters.Exponent!)
                }
            }
        });

        FakeTelegramOidcHandler handler = new(encodedIdToken, jwksJson);
        using HttpClient httpClient = new(handler);
        TelegramWebAuthenticationOptions options = new()
        {
            Enabled = true,
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            RedirectUri = RedirectUri,
            SessionLifetimeHours = 12
        };

        TelegramWebIdentity? identity =
            await TelegramWebAuthenticationService.ExchangeCodeAsync(
                httpClient,
                options,
                "authorization-code",
                new string('v', 64),
                CancellationToken.None);

        Assert.NotNull(identity);
        Assert.Equal(424242, identity.TelegramUserId);
        Assert.Equal("elyndor_player", identity.TelegramUsername);
        Assert.Equal(1, handler.TokenRequests);
        Assert.Equal(1, handler.JwksRequests);
        Assert.Equal(
            new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"))).ToString(),
            handler.AuthorizationHeader);
        Assert.Contains("grant_type=authorization_code", handler.TokenRequestBody);
        Assert.Contains("code=authorization-code", handler.TokenRequestBody);
        Assert.Contains("code_verifier=", handler.TokenRequestBody);
    }

    [Fact]
    public async Task ExchangeCodeRejectsIdTokenForAnotherAudience()
    {
        using RSA rsa = RSA.Create(2048);
        const string keyId = "telegram-test-key";
        RsaSecurityKey signingKey = new(rsa) { KeyId = keyId };
        DateTime utcNow = DateTime.UtcNow;
        JwtSecurityToken idToken = new(
            issuer: "https://oauth.telegram.org",
            audience: "another-client",
            claims: [new Claim("id", "424242")],
            notBefore: utcNow.AddMinutes(-1),
            expires: utcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                signingKey,
                SecurityAlgorithms.RsaSha256));

        RSAParameters publicParameters = rsa.ExportParameters(false);
        string jwksJson = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = keyId,
                    alg = "RS256",
                    n = Base64UrlEncoder.Encode(publicParameters.Modulus!),
                    e = Base64UrlEncoder.Encode(publicParameters.Exponent!)
                }
            }
        });
        FakeTelegramOidcHandler handler = new(
            new JwtSecurityTokenHandler().WriteToken(idToken),
            jwksJson);
        using HttpClient httpClient = new(handler);

        TelegramWebIdentity? identity =
            await TelegramWebAuthenticationService.ExchangeCodeAsync(
                httpClient,
                new TelegramWebAuthenticationOptions
                {
                    Enabled = true,
                    ClientId = ClientId,
                    ClientSecret = ClientSecret,
                    RedirectUri = RedirectUri,
                    SessionLifetimeHours = 12
                },
                "authorization-code",
                new string('v', 64),
                CancellationToken.None);

        Assert.Null(identity);
    }

    private sealed class FakeTelegramOidcHandler(
        string idToken,
        string jwksJson) : HttpMessageHandler
    {
        public int TokenRequests { get; private set; }

        public int JwksRequests { get; private set; }

        public string? AuthorizationHeader { get; private set; }

        public string TokenRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string uri = request.RequestUri?.AbsoluteUri ?? string.Empty;
            if (uri == "https://oauth.telegram.org/token")
            {
                TokenRequests++;
                AuthorizationHeader = request.Headers.Authorization?.ToString();
                TokenRequestBody = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                return JsonResponse(new { id_token = idToken });
            }

            if (uri == "https://oauth.telegram.org/.well-known/jwks.json")
            {
                JwksRequests++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        jwksJson,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage JsonResponse(object body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json")
            };
    }
}
