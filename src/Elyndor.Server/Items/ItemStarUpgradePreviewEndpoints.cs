using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Items;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Items;

namespace Elyndor.Server.Items;

public static class ItemStarUpgradePreviewEndpoints
{
    public static IEndpointRouteBuilder MapItemStarUpgradePreviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/inventory/enhancement/preview/{characterItemId:guid}", GetEnhancementPreviewAsync)
            .RequireAuthorization()
            .WithTags("Inventory");
        endpoints.MapPost("/api/v1/inventory/enhancement", EnhanceAsync)
            .RequireAuthorization()
            .WithTags("Inventory");

        // Deprecated read alias. The response shape is retained for old clients, but targetStars
        // contains the target enhancement level and no operation can mutate intrinsic Stars.
        endpoints.MapGet("/api/v1/inventory/star-upgrade/preview/{characterItemId:guid}", GetLegacyPreviewAsync)
            .RequireAuthorization()
            .WithTags("Inventory");
        return endpoints;
    }

    private static async Task<IResult> GetEnhancementPreviewAsync(
        Guid characterItemId,
        ClaimsPrincipal user,
        HttpContext context,
        ItemEnhancementService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        ItemEnhancementPreviewResult result = await service.GetPreviewAsync(accountId, characterItemId, cancellationToken);
        if (!result.Succeeded) return EnhancementProblem(result.ErrorCode!, context);

        return Results.Ok(new
        {
            itemInstanceId = result.ItemInstanceId,
            targetEnhancementLevel = result.TargetEnhancementLevel,
            enhancementBonusPercent = result.EnhancementBonusPercent,
            intrinsicItemPower = result.IntrinsicItemPower,
            finalItemPower = result.FinalItemPower,
            cost = new
            {
                gold = result.Gold,
                enhancementMaterialItemId = result.EnhancementMaterialItemId,
                enhancementMaterialQuantity = result.EnhancementMaterialQuantity,
                catalystItemId = result.CatalystItemId,
                catalystQuantity = result.CatalystQuantity
            }
        });
    }

    private static async Task<IResult> EnhanceAsync(
        UpgradeItemStarsRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        ItemEnhancementService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                ItemEnhancementResult result = await service.EnhanceAsync(
                    accountId,
                    request.CharacterItemId,
                    request.MutationId,
                    cancellationToken);
                return result.Succeeded
                    ? Results.Ok(new
                    {
                        itemInstanceId = request.CharacterItemId,
                        enhancementLevel = result.EnhancementLevel,
                        enhancementBonusPercent = result.EnhancementBonusPercent,
                        finalItemPower = result.FinalItemPower,
                        item = result.Item
                    })
                    : EnhancementProblem(result.ErrorCode!, context);
            },
            () => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "character_operation_in_combat",
                    ["correlationId"] = context.TraceIdentifier
                }),
            cancellationToken);
    }

    private static async Task<IResult> GetLegacyPreviewAsync(
        Guid characterItemId,
        ClaimsPrincipal user,
        HttpContext context,
        ItemStarUpgradeService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
#pragma warning disable CS0618
        ItemStarUpgradePreviewResult result = await service.GetPreviewAsync(accountId, characterItemId, cancellationToken);
#pragma warning restore CS0618
        if (!result.Succeeded) return LegacyProblem(result.ErrorCode!, context);

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

    private static IResult EnhancementProblem(string errorCode, HttpContext context) => Results.Problem(
        statusCode: errorCode is ItemEnhancementErrorCodes.ItemNotFound or ItemEnhancementErrorCodes.CharacterNotFound
            ? StatusCodes.Status404NotFound
            : errorCode is ItemEnhancementErrorCodes.ItemLocked
                or ItemEnhancementErrorCodes.ItemTransactionLocked
                or ItemEnhancementErrorCodes.MaxEnhancement
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status422UnprocessableEntity,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = errorCode,
            ["correlationId"] = context.TraceIdentifier
        });

    private static IResult LegacyProblem(string errorCode, HttpContext context) => Results.Problem(
        statusCode: errorCode is ItemStarUpgradeErrorCodes.ItemNotFound or ItemStarUpgradeErrorCodes.CharacterNotFound
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
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId) && accountId != Guid.Empty;
}
