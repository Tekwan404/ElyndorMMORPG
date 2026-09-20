using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Items;
using Elyndor.Infrastructure.Items;

namespace Elyndor.Server.Items;

public static class SpatialInventoryEndpoints
{
    public static IEndpointRouteBuilder MapSpatialInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/inventory/spatial-artifact")
            .RequireAuthorization()
            .WithTags("Inventory");

        group.MapGet("/", GetAsync);
        group.MapPost("/equip", EquipAsync);
        group.MapPost("/unequip", UnequipAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user,
        SpatialInventoryService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        SpatialInventoryOperationResult result = await service.GetAsync(accountId, cancellationToken);
        return result.IsSuccess
            ? Results.Ok(ToResponse(result.Snapshot!))
            : Problem(result.ErrorCode!);
    }

    private static async Task<IResult> EquipAsync(
        EquipSpatialArtifactRequest request,
        ClaimsPrincipal user,
        SpatialInventoryService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        SpatialInventoryOperationResult result = await service.EquipAsync(
            accountId,
            request.CharacterItemId,
            cancellationToken);
        return result.IsSuccess
            ? Results.Ok(ToResponse(result.Snapshot!))
            : Problem(result.ErrorCode!);
    }

    private static async Task<IResult> UnequipAsync(
        ClaimsPrincipal user,
        SpatialInventoryService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        SpatialInventoryOperationResult result = await service.UnequipAsync(accountId, cancellationToken);
        return result.IsSuccess
            ? Results.Ok(ToResponse(result.Snapshot!))
            : Problem(result.ErrorCode!);
    }

    private static SpatialInventoryResponse ToResponse(SpatialInventorySnapshot snapshot) =>
        new(
            snapshot.EquippedArtifact is null
                ? null
                : new SpatialArtifactResponse(
                    snapshot.EquippedArtifact.CharacterItemId,
                    snapshot.EquippedArtifact.Definition.Id,
                    snapshot.EquippedArtifact.Definition.Name,
                    snapshot.EquippedArtifact.Definition.Rarity.ToString(),
                    snapshot.EquippedArtifact.Definition.InventoryCapacityBonus,
                    snapshot.EquippedArtifact.Definition.IconId),
            new InventoryCapacityResponse(
                snapshot.Capacity.BaseCapacity,
                snapshot.Capacity.ArtifactCapacityBonus,
                snapshot.Capacity.Capacity,
                snapshot.Capacity.UsedSlots,
                snapshot.Capacity.FreeSlots,
                snapshot.Capacity.IsOverflow));

    private static IResult Problem(string errorCode) => errorCode switch
    {
        SpatialInventoryErrorCodes.CharacterNotFound or SpatialInventoryErrorCodes.ItemNotFound =>
            Results.NotFound(new { errorCode }),
        SpatialInventoryErrorCodes.ItemNotOwned or SpatialInventoryErrorCodes.NotSpatialArtifact =>
            Results.BadRequest(new { errorCode }),
        SpatialInventoryErrorCodes.InventoryFull or SpatialInventoryErrorCodes.TransactionLocked or SpatialInventoryErrorCodes.Conflict =>
            Results.Conflict(new { errorCode }),
        _ => Results.BadRequest(new { errorCode })
    };

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId)
    {
        string? subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out accountId);
    }
}
