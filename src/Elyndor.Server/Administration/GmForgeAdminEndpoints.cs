using System.Security.Cryptography;
using Elyndor.Infrastructure.Administration;
using Elyndor.Server.Identity;

namespace Elyndor.Server.Administration;

public sealed record GmForgeAdminCreateRequest(
    long TargetTelegramUserId,
    string Specification,
    Guid RequestId);

public static class GmForgeAdminEndpoints
{
    public static IEndpointRouteBuilder MapGmForgeAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/admin/gm-forge/create", CreateAsync)
            .WithTags("GM Forge")
            .RequireAuthorization(AdminAuthorization.PolicyName);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        GmForgeAdminCreateRequest request,
        HttpContext context,
        TelegramAdministrationService service,
        CancellationToken cancellationToken)
    {
        string? rawAdminId = context.User.FindFirst(AuthenticationClaimTypes.TelegramUserId)?.Value;
        if (!long.TryParse(rawAdminId, out long adminId) || adminId <= 0)
            return Problem(context, StatusCodes.Status403Forbidden, "admin_gmforge_actor_invalid");
        if (request.TargetTelegramUserId <= 0
            || request.RequestId == Guid.Empty
            || request.Specification is not { Length: > 0 and <= 1024 }
            || !GmForgeSpecification.TryParse(request.Specification, out _))
            return Problem(context, StatusCodes.Status400BadRequest, "admin_gmforge_invalid");

        AdministrationResult result = await service.ExecuteAsync(
            ToAuditUpdateId(request.RequestId),
            adminId,
            new AdministrationOperation(
                AdministrationOperationType.GmForge,
                request.TargetTelegramUserId,
                request.Specification),
            cancellationToken);

        if (!result.IsSuccess)
            return Problem(context, StatusCodes.Status422UnprocessableEntity, result.Code);
        return Results.Ok(new { result.Code, result.Message, result.IsDuplicate });
    }

    // Reserve the high-positive ID space for API requests, away from Telegram update IDs.
    // A stable, random request GUID produces replay-safe admin audit identities.
    private static long ToAuditUpdateId(Guid requestId)
    {
        byte[] hash = SHA256.HashData(requestId.ToByteArray());
        return 0x4000_0000_0000_0000L | (BitConverter.ToInt64(hash, 0) & 0x0FFF_FFFF_FFFF_FFFFL);
    }

    private static IResult Problem(HttpContext context, int status, string code) =>
        Results.Problem(
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = context.TraceIdentifier
            });
}
