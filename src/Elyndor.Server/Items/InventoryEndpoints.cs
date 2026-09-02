using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Items;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Items;

namespace Elyndor.Server.Items;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/inventory")
            .RequireAuthorization()
            .WithTags("Inventory");
        group.MapGet("/", GetAsync);
        group.MapPost("/equip", EquipAsync);
        group.MapPost("/unequip", UnequipAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal user,
        HttpContext context,
        InventoryEquipmentService service,
        CancellationToken cancellationToken) =>
        TryGetAccountId(user, out Guid accountId)
            ? ToResult(await service.GetAsync(accountId, cancellationToken), context)
            : Results.Unauthorized();

    private static async Task<IResult> EquipAsync(
        EquipItemRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        InventoryEquipmentService service,
        CancellationToken cancellationToken) =>
        TryGetAccountId(user, out Guid accountId)
            ? ToResult(
                await service.EquipAsync(accountId, request.CharacterItemId, cancellationToken),
                context)
            : Results.Unauthorized();

    private static async Task<IResult> UnequipAsync(
        UnequipItemRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        InventoryEquipmentService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();
        if (!Enum.TryParse(request.Slot, ignoreCase: false, out EquipmentSlot slot))
        {
            return Problem(InventoryErrorCodes.InvalidSlot, context);
        }

        return ToResult(await service.UnequipAsync(accountId, slot, cancellationToken), context);
    }

    internal static InventoryResponse ToResponse(InventorySnapshot snapshot) =>
        new(
            snapshot.Items.Select(ToResponse).ToArray(),
            new EquipmentSlotsResponse(
                GetEquipped(snapshot, EquipmentSlot.Weapon),
                GetEquipped(snapshot, EquipmentSlot.Head),
                GetEquipped(snapshot, EquipmentSlot.Chest),
                GetEquipped(snapshot, EquipmentSlot.Legs),
                GetEquipped(snapshot, EquipmentSlot.Boots),
                GetEquipped(snapshot, EquipmentSlot.Accessory)));

    private static IResult ToResult(InventoryOperationResult result, HttpContext context) =>
        result.IsSuccess
            ? Results.Ok(ToResponse(result.Snapshot!))
            : Problem(result.ErrorCode!, context);

    private static IResult Problem(string errorCode, HttpContext context) =>
        Results.Problem(
            statusCode: errorCode == InventoryErrorCodes.CharacterNotFound
                || errorCode == InventoryErrorCodes.ItemNotFound
                    ? StatusCodes.Status404NotFound
                    : errorCode == InventoryErrorCodes.Conflict
                        ? StatusCodes.Status409Conflict
                        : StatusCodes.Status422UnprocessableEntity,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = errorCode,
                ["correlationId"] = context.TraceIdentifier
            });

    private static InventoryItemResponse? GetEquipped(
        InventorySnapshot snapshot,
        EquipmentSlot slot) =>
        snapshot.Equipped.TryGetValue(slot, out InventoryItemSnapshot? item)
            ? ToResponse(item)
            : null;

    internal static InventoryItemResponse ToResponse(InventoryItemSnapshot item) =>
        new(
            item.Id,
            item.Definition.Id,
            item.Definition.Name,
            item.Definition.Type.ToString(),
            item.Definition.Rarity.ToString(),
            item.Definition.RequiredLevel,
            item.Quantity,
            item.Definition.Slot?.ToString(),
            item.EquippedSlot?.ToString(),
            new ItemStatsResponse(
                item.Definition.Stats.Strength,
                item.Definition.Stats.Agility,
                item.Definition.Stats.Intellect,
                item.Definition.Stats.Stamina),
            item.Definition.Description,
            item.Definition.SetId,
            item.Definition.WeaponBaseAttackIntervalSeconds,
            item.Definition.AttackSpeedPercent,
            item.Definition.DodgePercent,
            item.Definition.HealAmount,
            item.Definition.ConsumableCooldownSeconds,
            item.Definition.BuyPriceGold);

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
