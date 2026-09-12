using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Infrastructure.Items;

namespace Elyndor.Server.Items;

public static class ItemStarUpgradePreviewEndpoints
{
    public static IEndpointRouteBuilder MapItemStarUpgradePreviewEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/inventory/star-upgrade/preview/{characterItemId:guid}",
                GetPreviewAsync)
            .RequireAuthorization()
            .WithTags("Inventory");

        return endpoints;
    }

    private static async Task<IResult> GetPreviewAsync(
        Guid characterItemId,
        ClaimsPrincipal user,
        HttpContext context,
        ItemStarUpgradeService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        ItemStarUpgradePreviewResult result = await service.GetPreviewAsync(
            accountId,
            characterItemId,
            cancellationToken);

        if (!result.Succeeded)
            return PreviewProblem(result.ErrorCode!, context);

        return Results.Ok(new
        {
            itemInstanceId = result.ItemInstanceId,
            targetStars = result.TargetStars,
            cost = new
            {
                gold = result.Gold,
                reforgeStoneItemId = result.ReforgeStoneItemId,
                reforgeStoneQuantity = result.ReforgeStoneQuantity,
                catalystItemId = result.CatalystItemId,
                catalystQuantity = result.CatalystQuantity
            }
        });
    }

    private static IResult PreviewProblem(string errorCode, HttpContext context) =>
        Results.Problem(
            statusCode: errorCode is ItemStarUpgradeErrorCodes.ItemNotFound
                    or ItemStarUpgradeErrorCodes.CharacterNotFound
                ? StatusCodes.Status404NotFound
                : errorCode is ItemStarUpgradeErrorCodes.ItemLocked
                    or ItemStarUpgradeErrorCodes.ItemTransactionLocked
                    or ItemStarUpgradeErrorCodes.MaxStars
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status422UnprocessableEntity,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = errorCode,
                ["correlationId"] = context.TraceIdentifier
            });

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
