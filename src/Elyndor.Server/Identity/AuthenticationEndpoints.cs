using System.Security.Claims;
using System.Text.Json;
using Elyndor.Contracts.Identity;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Identity;
using Elyndor.Infrastructure.Identity.Telegram;
using Elyndor.Server.Administration;
using Elyndor.Server.Monitoring;
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

        endpoints.MapPost("/api/v1/presence/heartbeat", RecordPresence)
            .WithTags("System")
            .RequireAuthorization();

        return endpoints;
    }

    private static IResult RecordPresence(
        ClaimsPrincipal user,
        IServerMetricsCollector metricsCollector)
    {
        string? subject = user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subject, out Guid accountId) || accountId == Guid.Empty)
            return Results.Unauthorized();

        metricsCollector.RecordPlayerSeen(accountId);
        return Results.NoContent();
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
        bool issueSessionCredential;

        if (TelegramSessionCredentialService.IsSessionCredential(request.InitData))
        {
            TelegramSessionIdentity? sessionIdentity =
                TelegramSessionCredentialService.ValidateCredential(
                    options,
                    timeProvider,
                    request.InitData);
            if (sessionIdentity is null)
            {
                return CreateProblem(
                    httpContext,
                    StatusCodes.Status401Unauthorized,
                    "telegram_session_credential_invalid");
            }

            telegramUserId = sessionIdentity.TelegramUserId;
            telegramUsername = sessionIdentity.TelegramUsername;
            issueSessionCredential = true;
        }
        else if (TelegramWebAuthenticationService.IsWebCredential(request.InitData))
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
            issueSessionCredential = false;
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
            issueSessionCredential = true;
        }

        Account account = await accountResolver.ResolveAsync(
            telegramUserId,
            telegramUsername,
            cancellationToken);
        IssuedTelegramSessionCredential? sessionCredential =
            issueSessionCredential
                ? TelegramSessionCredentialService.IssueCredential(
                    options,
                    timeProvider,
                    new TelegramSessionIdentity(
                        telegramUserId,
                        telegramUsername))
                : null;
        return Results.Ok(CreateAuthenticationResponse(
            account,
            telegramUserId,
            adminOptions.Value,
            tokenIssuer,
            sessionCredential));
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
        catch (TelegramWebTokenExchangeException exception)
        {
            if (exception.StatusCode == StatusCodes.Status429TooManyRequests
                || exception.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                return CreateProblem(
                    httpContext,
                    StatusCodes.Status503ServiceUnavailable,
                    "telegram_web_auth_unavailable");
            }

            if (string.Equals(
                    exception.ProviderErrorCode,
                    "invalid_client",
                    StringComparison.Ordinal))
            {
                return CreateProblem(
                    httpContext,
                    StatusCodes.Status503ServiceUnavailable,
                    "telegram_web_provider_configuration_invalid");
            }

            if (string.Equals(
                    exception.ProviderErrorCode,
                    "invalid_grant",
                    StringComparison.Ordinal))
            {
                return CreateProblem(
                    httpContext,
                    StatusCodes.Status401Unauthorized,
                    "telegram_web_code_rejected");
            }

            return CreateProblem(
                httpContext,
                StatusCodes.Status401Unauthorized,
                "telegram_web_token_exchange_rejected");
        }
        catch (HttpRequestException)
        {
            return CreateProblem(
                httpContext,
                StatusCodes.Status503ServiceUnavailable,
                "telegram_web_auth_unavailable");
        }
        catch (JsonException)
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
                "telegram_web_id_token_invalid");
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
        JwtTokenIssuer tokenIssuer,
        IssuedTelegramSessionCredential? sessionCredential = null)
    {
        string[] roles = adminOptions.IsAllowedUser(telegramUserId)
            ? [AdminAuthorization.SuperAdminRole]
            : [];
        IssuedAccessToken token =
            tokenIssuer.Issue(account.Id, telegramUserId, roles);
        return new AuthenticationResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            roles,
            sessionCredential?.Value,
            sessionCredential?.ExpiresAtUtc);
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
