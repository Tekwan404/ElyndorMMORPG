using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Items;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.World;

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
        group.MapPost("/use-consumable", UseConsumableAsync);
        group.MapGet("/merchant/{merchantId}", GetMerchantAsync);
        group.MapPost("/merchant/buy", BuyMerchantItemAsync);
        group.MapPost("/merchant/sell-material", SellMerchantMaterialAsync);
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
                await service.EquipAsync(accountId, request.CharacterItemId, request.MutationId, cancellationToken),
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
            return Problem(InventoryErrorCodes.InvalidSlot, context);

        return ToResult(await service.UnequipAsync(accountId, slot, request.MutationId, cancellationToken), context);
    }

    private static async Task<IResult> UseConsumableAsync(
        UseConsumableRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        InventoryEquipmentService service,
        BootstrapService bootstrapService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        BootstrapSnapshot bootstrap = await bootstrapService.GetAsync(accountId, cancellationToken);
        if (bootstrap.Character is null) return Problem(InventoryErrorCodes.CharacterNotFound, context);

        return ToResult(
            await service.UseConsumableOutOfCombatAsync(
                accountId,
                request.CharacterItemId,
                request.MutationId,
                bootstrap.Character.Vitals.MaxHp,
                timeProvider.GetUtcNow(),
                cancellationToken),
            context);
    }

    private static async Task<IResult> GetMerchantAsync(
        string merchantId,
        ClaimsPrincipal user,
        HttpContext context,
        MerchantService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        MerchantOperationResult result = await service.GetAsync(accountId, merchantId, cancellationToken);
        return ToMerchantResult(result, context);
    }

    private static async Task<IResult> BuyMerchantItemAsync(
        BuyMerchantItemRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        MerchantService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        MerchantOperationResult result = await service.BuyAsync(
            accountId,
            request.MerchantId,
            request.ItemDefinitionId,
            request.Quantity,
            request.MutationId,
            cancellationToken);
        return ToMerchantResult(result, context);
    }

    private static async Task<IResult> SellMerchantMaterialAsync(
        SellMerchantItemRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        MerchantService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        MerchantOperationResult result = await service.SellMaterialAsync(
            accountId,
            request.MerchantId,
            request.CharacterItemId,
            request.Quantity,
            request.MutationId,
            cancellationToken);
        return ToMerchantResult(result, context);
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

    private static IResult ToMerchantResult(MerchantOperationResult result, HttpContext context) =>
        result.IsSuccess
            ? Results.Ok(ToMerchantResponse(result.Snapshot!))
            : MerchantProblem(result.ErrorCode!, context);

    private static MerchantResponse ToMerchantResponse(MerchantSnapshot snapshot) =>
        new(
            snapshot.Merchant.Id,
            snapshot.Merchant.Name,
            snapshot.Merchant.Description,
            snapshot.Gold,
            snapshot.Items.Select(item => new MerchantItemResponse(
                item.Definition.Id,
                item.Definition.Name,
                item.Definition.Type.ToString(),
                item.Definition.Rarity.ToString(),
                item.Definition.Description,
                item.Definition.BuyPriceGold,
                item.SellPriceGold,
                item.Definition.HealAmount)).ToArray());

    private static IResult Problem(string errorCode, HttpContext context) =>
        Results.Problem(
            statusCode: errorCode == InventoryErrorCodes.CharacterNotFound
                || errorCode == InventoryErrorCodes.ItemNotFound
                    ? StatusCodes.Status404NotFound
                    : errorCode is InventoryErrorCodes.Conflict or InventoryErrorCodes.MutationConflict
                        ? StatusCodes.Status409Conflict
                        : StatusCodes.Status422UnprocessableEntity,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = errorCode,
                ["correlationId"] = context.TraceIdentifier
            });

    private static IResult MerchantProblem(string errorCode, HttpContext context) =>
        Results.Problem(
            statusCode: errorCode is MerchantErrorCodes.CharacterNotFound or MerchantErrorCodes.MerchantNotFound
                ? StatusCodes.Status404NotFound
                : errorCode is MerchantErrorCodes.Conflict or MerchantErrorCodes.MutationConflict
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
            item.Definition.WeaponCategory,
            item.Definition.ArmorCategory,
            item.Definition.AllowedClassIds ?? [],
            item.Definition.WeaponBaseAttackIntervalSeconds,
            item.Definition.AttackSpeedPercent,
            item.Definition.DodgePercent,
            item.Definition.HealAmount,
            item.Definition.ConsumableCooldownSeconds,
            item.Definition.BuyPriceGold,
            MerchantService.ResolveSellPrice(item.Definition));

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
