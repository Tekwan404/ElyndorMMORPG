using Elyndor.Contracts.Identity;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Identity;
using Elyndor.Infrastructure.Identity.Telegram;
using Elyndor.Server.Administration;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Identity;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints,
        bool mapDevelopmentEndpoint)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/auth")
            .WithTags("Authentication")
            .RequireRateLimiting(ServerRateLimitPolicies.Authentication);

        group.MapPost("/telegram", AuthenticateTelegramAsync);

        if (mapDevelopmentEndpoint)
            group.MapPost("/development", AuthenticateDevelopmentAsync);

        return endpoints;
    }

    private static async Task<IResult> AuthenticateTelegramAsync(
        TelegramAuthenticationRequest request,
        HttpContext httpContext,
        TelegramInitDataValidator validator,
        IOptions<AuthenticationOptions> authenticationOptions,
        IOptions<TelegramAdminOptions> adminOptions,
        AccountResolver accountResolver,
        JwtTokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        AuthenticationOptions options = authenticationOptions.Value;
        TelegramInitDataValidationResult validation = validator.Validate(
            request.InitData,
            options.Telegram.BotToken,
            TimeSpan.FromSeconds(options.Telegram.InitDataMaxAgeSeconds),
            TimeSpan.FromSeconds(options.Telegram.MaxFutureSkewSeconds));

        if (!validation.IsValid)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = validation.ErrorCode!,
                    ["correlationId"] = httpContext.TraceIdentifier
                });
        }

        long telegramUserId = validation.Data!.TelegramUserId;
        Account account = await accountResolver.ResolveAsync(
            telegramUserId,
            cancellationToken);
        return CreateSuccess(
            account,
            telegramUserId,
            adminOptions.Value,
            tokenIssuer);
    }

    private static async Task<IResult> AuthenticateDevelopmentAsync(
        IOptions<AuthenticationOptions> authenticationOptions,
        IOptions<TelegramAdminOptions> adminOptions,
        AccountResolver accountResolver,
        JwtTokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        long telegramUserId =
            authenticationOptions.Value.Development.TelegramUserId;
        Account account = await accountResolver.ResolveAsync(
            telegramUserId,
            cancellationToken);
        return CreateSuccess(
            account,
            telegramUserId,
            adminOptions.Value,
            tokenIssuer);
    }

    private static IResult CreateSuccess(
        Account account,
        long telegramUserId,
        TelegramAdminOptions adminOptions,
        JwtTokenIssuer tokenIssuer)
    {
        string[] roles = adminOptions.IsAllowedUser(telegramUserId)
            ? [AdminAuthorization.SuperAdminRole]
            : [];
        IssuedAccessToken token =
            tokenIssuer.Issue(account.Id, telegramUserId, roles);
        return Results.Ok(new AuthenticationResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            roles));
    }
}
