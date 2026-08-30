using Elyndor.Contracts.Identity;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Identity;
using Elyndor.Infrastructure.Identity.Telegram;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Identity;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints,
        bool mapDevelopmentEndpoint)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        group.MapPost("/telegram", AuthenticateTelegramAsync);

        if (mapDevelopmentEndpoint)
        {
            group.MapPost("/development", AuthenticateDevelopmentAsync);
        }

        return endpoints;
    }

    private static async Task<IResult> AuthenticateTelegramAsync(
        TelegramAuthenticationRequest request,
        HttpContext httpContext,
        TelegramInitDataValidator validator,
        IOptions<AuthenticationOptions> authenticationOptions,
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
            return Results.Json(
                CreateError(validation.ErrorCode!, httpContext),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        Account account = await accountResolver.ResolveAsync(
            validation.Data!.TelegramUserId,
            cancellationToken);
        return CreateSuccess(account, tokenIssuer);
    }

    private static async Task<IResult> AuthenticateDevelopmentAsync(
        IOptions<AuthenticationOptions> authenticationOptions,
        AccountResolver accountResolver,
        JwtTokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        Account account = await accountResolver.ResolveAsync(
            authenticationOptions.Value.Development.TelegramUserId,
            cancellationToken);
        return CreateSuccess(account, tokenIssuer);
    }

    private static IResult CreateSuccess(Account account, JwtTokenIssuer tokenIssuer)
    {
        IssuedAccessToken token = tokenIssuer.Issue(account.Id);
        return Results.Ok(new AuthenticationResponse(
            token.AccessToken,
            token.ExpiresAtUtc));
    }

    private static ApiErrorResponse CreateError(string code, HttpContext httpContext) =>
        new(code, httpContext.TraceIdentifier);
}
