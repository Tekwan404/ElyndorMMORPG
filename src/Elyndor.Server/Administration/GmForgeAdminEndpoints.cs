using System.Security.Cryptography;
using Elyndor.Infrastructure.Administration;
using Elyndor.Server.Identity;

namespace Elyndor.Server.Administration;

public sealed record GmForgeAdminCreateRequest(
    long TargetTelegramUserId,
    string Specification,
    Guid RequestId);

public sealed record GmForgeBatchItem(string Mode, string Specification);

public sealed record GmForgeBatchRequest(long TargetTelegramUserId, Guid RequestId, IReadOnlyList<GmForgeBatchItem> Items);

public static class GmForgeAdminEndpoints
{
    public static IEndpointRouteBuilder MapGmForgeAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/admin/gm-forge/batch", BatchAsync)
            .WithTags("GM Forge")
            .RequireAuthorization(AdminAuthorization.PolicyName);
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

    private static async Task<IResult> BatchAsync(
        GmForgeBatchRequest request,
        HttpContext context,
        TelegramAdministrationService service,
        CancellationToken cancellationToken)
    {
        string? actor = context.User.FindFirst(AuthenticationClaimTypes.TelegramUserId)?.Value;
        if (!long.TryParse(actor, out long adminId) || adminId <= 0)
            return Problem(context, StatusCodes.Status403Forbidden, "admin_gmforge_actor_invalid");
        if (request.TargetTelegramUserId <= 0 || request.RequestId == Guid.Empty
            || request.Items is not { Count: >= 1 and <= 20 }
            || request.Items.Any(item => item is null
                || item.Specification is not { Length: > 0 and <= 1024 }
                || !(item.Mode == "regular" && IsOrdinaryGrant(item.Specification)
                    || item.Mode == "custom" && GmForgeSpecification.TryParse(item.Specification, out _))))
            return Problem(context, StatusCodes.Status400BadRequest, "admin_gmforge_invalid");

        List<object> results = new(request.Items.Count);
        for (int index = 0; index < request.Items.Count; index++)
        {
            GmForgeBatchItem line = request.Items[index];
            byte[] seed = [.. request.RequestId.ToByteArray(), .. BitConverter.GetBytes(index)];
            byte[] hash = SHA256.HashData(seed);
            long updateId = 0x5000_0000_0000_0000L
                | (BitConverter.ToInt64(hash, 0) & 0x0FFF_FFFF_FFFF_FFFFL);
            AdministrationResult outcome = await service.ExecuteAsync(
                updateId,
                adminId,
                new AdministrationOperation(
                    line.Mode == "regular" ? AdministrationOperationType.GiveItem : AdministrationOperationType.GmForge,
                    request.TargetTelegramUserId,
                    line.Specification),
                cancellationToken);

            results.Add(new
            {
                index,
                outcome.IsSuccess,
                outcome.IsDuplicate,
                outcome.Code,
                outcome.Message
            });
        }

        // Per-entry outcomes allow retries after a partial batch. Previously completed
        // entries are idempotent, while failures remain visible to the administrator.
        return Results.Ok(new { items = results });
    }

    private static bool IsOrdinaryGrant(string raw)
    {
        string[] tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is < 1 or > 3 || tokens[0].Length is < 1 or > 128)
            return false;
        if (tokens.Length >= 2
            && (!int.TryParse(tokens[1], out int quantity) || quantity is < 1 or > 1000))
            return false;
        return tokens.Length < 3 || tokens[2] is "NORMAL" or "ELITE" or "BOSS";
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
