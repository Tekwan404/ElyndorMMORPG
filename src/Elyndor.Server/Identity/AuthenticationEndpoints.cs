using Elyndor.Contracts.Identity;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Identity;
using Elyndor.Infrastructure.Identity.Telegram;
using Elyndor.Server.Administration;
using Microsoft.AspNetCore.RateLimiting;
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
        group.MapGet("/telegram-web/config", GetTelegramWebConfiguration);
        group.MapPost("/telegram-web", AuthenticateTelegramWebAsync);

        if (mapDevelopmentEndpoint)
            group.MapPost("/development", AuthenticateDevelopmentAsync);

        return endpoints;
    }

    private static IResult GetTelegramWebConfiguration(
        IOptions<AuthenticationOptions> authenticationOptions)
    {
        TelegramWebAuthenticationOptions web =
            authenticationOptions.Value.Telegram.Web;
        return Results.Ok(new TelegramWebAuthenticationConfigResponse(
            web.Enabled,
            web.Enabled ? web.ClientId : null,
            web.Enabled ? web.RedirectUri : null));
    }

    private static async Task<IResult> AuthenticateTelegramAsync(
        TelegramAuthenticationRequest request,
        HttpContext httpContext,
        TelegramInitDataValidator validator,
        IOptions<AuthenticationOptions> authenticationOptions,
        IOptions<TelegramAdminOptions> adminOptions,
        AccountResolver accountResolver,
        JwtTokenIssuer tokenIssuer,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        AuthenticationOptions options = authenticationOptions.Value;
        long telegramUserId;
        string? telegramUsername;

        if (TelegramWebAuthenticationService.IsWebCredential(request.InitData))
        {
            if (!options.Telegram.Web.Enabled)
            {
                return CreateProblem(
                    httpContext,
                    StatusCodes.Status401Unauthorized,
                    "telegram_web_credential_invalid");
            }

            TelegramWebIdentity? webIdentity =
                TelegramWebAuthenticationService.ValidateCredential(
                    options,
                    timeProvider,
                    request.InitData);
            if (webIdentity is null)
            {
                return CreateProblem(
                    httpContext,
                    StatusCodes.Status401Unauthorized,
                    "telegram_web_credential_invalid");
            }

            telegramUserId = webIdentity.TelegramUserId;
            telegramUsername = webIdentity.TelegramUsername;
        }
        else
        {
            TelegramInitDataValidationResult validation = validator.Validate(
                request.InitData,
                options.Telegram.BotToken,
                TimeSpan.FromSeconds(options.Telegram.InitDataMaxAgeSeconds),
                TimeSpan.FromSeconds(options.Telegram.MaxFutureSkewSeconds));

            if (!validation.IsValid)
            {
                return CreateProblem(
                    httpContext,
                    StatusCodes.Status401Unauthorized,
                    validation.ErrorCode!);
            }

            telegramUserId = validation.Data!.TelegramUserId;
            telegramUsername = validation.Data.TelegramUsername;
        }

        Account account = await accountResolver.ResolveAsync(
            telegramUserId,
            telegramUsername,
            cancellationToken);
        return Results.Ok(CreateAuthenticationResponse(
            account,
            telegramUserId,
            adminOptions.Value,
            tokenIssuer));
    }

    private static async Task<IResult> AuthenticateTelegramWebAsync(
        TelegramWebAuthenticationRequest request,
        HttpContext httpContext,
        HttpClient httpClient,
        IOptions<AuthenticationOptions> authenticationOptions,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        AuthenticationOptions options = authenticationOptions.Value;
        TelegramWebAuthenticationOptions web = options.Telegram.Web;
        if (!web.Enabled)
        {
            return CreateProblem(
                httpContext,
                StatusCodes.Status503ServiceUnavailable,
                "telegram_web_auth_not_configured");
        }

        if (!IsValidTelegramWebRequest(request))
        {
            return CreateProblem(
                httpContext,
                StatusCodes.Status400BadRequest,
                "telegram_web_request_invalid");
        }

        TelegramWebIdentity? identity;
        try
        {
            identity = await TelegramWebAuthenticationService.ExchangeCodeAsync(
                httpClient,
                web,
                request.Code,
                request.CodeVerifier,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return CreateProblem(
                httpContext,
                StatusCodes.Status503ServiceUnavailable,
                "telegram_web_auth_unavailable");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CreateProblem(
                httpContext,
                StatusCodes.Status503ServiceUnavailable,
                "telegram_web_auth_unavailable");
        }

        if (identity is null)
        {
            return CreateProblem(
                httpContext,
                StatusCodes.Status401Unauthorized,
                "telegram_web_auth_invalid");
        }

        IssuedTelegramWebCredential credential =
            TelegramWebAuthenticationService.IssueCredential(
                options,
                timeProvider,
                identity);
        return Results.Ok(new TelegramWebAuthenticationResponse(
            credential.Value,
            credential.ExpiresAtUtc));
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
        return Results.Ok(CreateAuthenticationResponse(
            account,
            telegramUserId,
            adminOptions.Value,
            tokenIssuer));
    }

    private static AuthenticationResponse CreateAuthenticationResponse(
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
        return new AuthenticationResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            roles);
    }

    private static bool IsValidTelegramWebRequest(
        TelegramWebAuthenticationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code)
            || string.IsNullOrWhiteSpace(request.CodeVerifier)
            || request.Code.Length > 4096
            || request.CodeVerifier.Length is < 43 or > 128)
        {
            return false;
        }

        return request.CodeVerifier.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '-' or '.' or '_' or '~');
    }

    private static IResult CreateProblem(
        HttpContext httpContext,
        int statusCode,
        string code) =>
        Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = httpContext.TraceIdentifier
            });
}
