using Elyndor.Contracts.Administration;
using Elyndor.Contracts.Identity;
using Elyndor.Core.Identity;

namespace Elyndor.Server.Administration;

public static class AdminWebAuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAdminWebAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v1/admin/auth")
            .WithTags("Admin Authentication");

        group.MapPost("/request-code", RequestCodeAsync);
        group.MapPost("/verify-code", VerifyCodeAsync);
        return endpoints;
    }

    private static async Task<IResult> RequestCodeAsync(
        AdminWebAuthenticationCodeRequest request,
        HttpContext context,
        AdminWebAuthenticationService service,
        CancellationToken cancellationToken)
    {
        if (request.TelegramUserId <= 0)
        {
            return Problem(
                context,
                StatusCodes.Status400BadRequest,
                "admin_login_telegram_id_invalid");
        }

        AdminWebAuthenticationIssueResult result =
            await service.IssueCodeAsync(
                request.TelegramUserId,
                cancellationToken);

        return result.Status switch
        {
            AdminWebAuthenticationIssueStatus.Issued =>
                Results.Ok(new AdminWebAuthenticationChallengeResponse(
                    result.ChallengeId!.Value,
                    result.ExpiresAtUtc!.Value)),
            AdminWebAuthenticationIssueStatus.NotAllowed =>
                Problem(
                    context,
                    StatusCodes.Status403Forbidden,
                    "admin_login_not_allowed"),
            AdminWebAuthenticationIssueStatus.RateLimited =>
                Problem(
                    context,
                    StatusCodes.Status429TooManyRequests,
                    "admin_login_rate_limited"),
            AdminWebAuthenticationIssueStatus.DeliveryFailed =>
                Problem(
                    context,
                    StatusCodes.Status503ServiceUnavailable,
                    "admin_login_delivery_failed"),
            _ => throw new InvalidOperationException(
                "Unknown admin authentication issue status.")
        };
    }

    private static async Task<IResult> VerifyCodeAsync(
        AdminWebAuthenticationVerifyRequest request,
        HttpContext context,
        AdminWebAuthenticationService service,
        AccountResolver accountResolver,
        JwtTokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        if (request.ChallengeId == Guid.Empty
            || request.TelegramUserId <= 0
            || string.IsNullOrWhiteSpace(request.Code))
        {
            return Problem(
                context,
                StatusCodes.Status400BadRequest,
                "admin_login_request_invalid");
        }

        AdminWebAuthenticationVerificationStatus verification =
            service.VerifyCode(
                request.ChallengeId,
                request.TelegramUserId,
                request.Code);

        if (verification != AdminWebAuthenticationVerificationStatus.Success)
        {
            string code = verification switch
            {
                AdminWebAuthenticationVerificationStatus.Expired =>
                    "admin_login_code_expired",
                AdminWebAuthenticationVerificationStatus.NotAllowed =>
                    "admin_login_not_allowed",
                _ => "admin_login_code_invalid"
            };

            int status = verification
                == AdminWebAuthenticationVerificationStatus.NotAllowed
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status401Unauthorized;

            return Problem(context, status, code);
        }

        Account account = await accountResolver.ResolveAsync(
            request.TelegramUserId,
            cancellationToken);
        IssuedAccessToken token = tokenIssuer.Issue(
            account.Id,
            request.TelegramUserId,
            [AdminAuthorization.SuperAdminRole]);

        return Results.Ok(new AuthenticationResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            [AdminAuthorization.SuperAdminRole]));
    }

    private static IResult Problem(
        HttpContext context,
        int statusCode,
        string code) =>
        Results.Problem(
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.TraceIdentifier
            });
}
