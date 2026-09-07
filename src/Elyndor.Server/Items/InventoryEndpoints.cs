using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Items;
using Elyndor.Core.Items;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.World;
using Elyndor.Infrastructure.Characters;

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
        group.MapPost("/set-lock", SetItemLockAsync);
        group.MapGet("/pending-loot", GetPendingLootAsync);
        group.MapPost("/pending-loot/claim", ClaimPendingLootAsync);
        group.MapGet("/merchant/{merchantId}", GetMerchantAsync);
        group.MapPost("/merchant/buy", BuyMerchantItemAsync);
        group.MapPost("/merchant/sell-material", SellMerchantMaterialAsync);
        group.MapPost("/merchant/sell-item", SellMerchantItemAsync);
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
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();

        EquipmentSlot? targetSlot = null;
        if (request.TargetSlot is not null)
        {
            if (!Enum.TryParse(request.TargetSlot, ignoreCase: false, out EquipmentSlot parsedSlot))
                return Problem(InventoryErrorCodes.InvalidSlot, context);
            targetSlot = parsedSlot;
        }

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () => ToResult(
                await service.EquipAsync(
                    accountId,
                    request.CharacterItemId,
                    targetSlot,
                    request.MutationId,
                    cancellationToken),
                context),
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> UnequipAsync(
        UnequipItemRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        InventoryEquipmentService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();
        if (!Enum.TryParse(request.Slot, ignoreCase: false, out EquipmentSlot slot))
            return Problem(InventoryErrorCodes.InvalidSlot, context);

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () => ToResult(
                await service.UnequipAsync(
                    accountId,
                    slot,
                    request.MutationId,
                    cancellationToken),
                context),
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> UseConsumableAsync(
        UseConsumableRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        InventoryEquipmentService service,
        BootstrapService bootstrapService,
        TimeProvider timeProvider,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                BootstrapSnapshot bootstrap = await bootstrapService.GetAsync(
                    accountId,
                    cancellationToken);
                if (bootstrap.Character is null)
                    return Problem(InventoryErrorCodes.CharacterNotFound, context);

                return ToResult(
                    await service.UseConsumableOutOfCombatAsync(
                        accountId,
                        request.CharacterItemId,
                        request.MutationId,
                        bootstrap.Character.Vitals.MaxHp,
                        bootstrap.Character.Vitals.ResourceType,
                        bootstrap.Character.Vitals.MaxResource,
                        timeProvider.GetUtcNow(),
                        cancellationToken),
                    context);
            },
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> GetPendingLootAsync(
        ClaimsPrincipal user,
        InventoryEquipmentService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        IReadOnlyList<PendingLootItemSnapshot> items =
            await service.GetPendingLootAsync(accountId, cancellationToken);
        return Results.Ok(new PendingLootResponse(
            items.Select(ToPendingLootResponse).ToArray()));
    }

    private static async Task<IResult> ClaimPendingLootAsync(
        ClaimPendingLootRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        InventoryEquipmentService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () => ToResult(
                await service.ClaimPendingLootAsync(
                    accountId,
                    request.MutationId,
                    cancellationToken),
                context),
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> SetItemLockAsync(
        SetItemLockRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        InventoryEquipmentService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return ToResult(
            await service.SetItemLockAsync(
                accountId,
                request.CharacterItemId,
                request.IsLocked,
                request.MutationId,
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
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                MerchantOperationResult result = await service.BuyAsync(
            accountId,
            request.MerchantId,
            request.ItemDefinitionId,
            request.Quantity,
                    request.MutationId,
                    cancellationToken);
                return ToMerchantResult(result, context);
            },
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> SellMerchantItemAsync(
        SellMerchantItemRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        MerchantService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                MerchantOperationResult result = await service.SellItemAsync(
                    accountId,
                    request.MerchantId,
                    request.CharacterItemId,
                    request.Quantity,
                    request.MutationId,
                    cancellationToken);
                return ToMerchantResult(result, context);
            },
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> SellMerchantMaterialAsync(
        SellMerchantItemRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        MerchantService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                MerchantOperationResult result = await service.SellMaterialAsync(
            accountId,
            request.MerchantId,
            request.CharacterItemId,
            request.Quantity,
                    request.MutationId,
                    cancellationToken);
                return ToMerchantResult(result, context);
            },
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static IResult InCombatProblem(HttpContext context) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = CharacterOperationErrorCodes.InCombat,
                ["correlationId"] = context.TraceIdentifier
            });

    internal static InventoryResponse ToResponse(InventorySnapshot snapshot) =>
        new(
            snapshot.Items.Select(ToResponse).ToArray(),
            new EquipmentSlotsResponse(
                GetEquipped(snapshot, EquipmentSlot.Weapon),
                GetEquipped(snapshot, EquipmentSlot.Head),
                GetEquipped(snapshot, EquipmentSlot.Chest),
                GetEquipped(snapshot, EquipmentSlot.Legs),
                GetEquipped(snapshot, EquipmentSlot.Boots),
                GetEquipped(snapshot, EquipmentSlot.Accessory),
                GetEquipped(snapshot, EquipmentSlot.MainHand),
                GetEquipped(snapshot, EquipmentSlot.OffHand),
                GetEquipped(snapshot, EquipmentSlot.Hands),
                GetEquipped(snapshot, EquipmentSlot.Feet),
                GetEquipped(snapshot, EquipmentSlot.Cloak),
                GetEquipped(snapshot, EquipmentSlot.Amulet),
                GetEquipped(snapshot, EquipmentSlot.Ring1),
                GetEquipped(snapshot, EquipmentSlot.Ring2)));

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
                ToConsumableActions(item.Definition),
                item.Definition.ConsumableCooldownCategoryId,
                item.Definition.ConsumableCooldownSeconds,
                item.Definition.IconId)).ToArray());

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
                : errorCode is MerchantErrorCodes.Conflict
                    or MerchantErrorCodes.MutationConflict
                    or MerchantErrorCodes.ItemLocked
                    or MerchantErrorCodes.ItemEquipped
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

    private static PendingLootItemResponse ToPendingLootResponse(
        PendingLootItemSnapshot item)
    {
        PrimaryStats stats = item.RolledPrimaryStats ?? item.Definition.Stats;
        return new PendingLootItemResponse(
            item.Id,
            item.Definition.Id,
            item.Definition.Name,
            item.Definition.Type.ToString(),
            item.Definition.Rarity.ToString(),
            item.Quantity,
            item.CreatedAtUtc,
            new ItemStatsResponse(
                stats.Strength,
                stats.Agility,
                stats.Intellect,
                stats.Stamina,
                item.Definition.MaxHpFlat,
                item.Definition.AttackPowerFlat,
                item.Definition.SpellPowerFlat,
                item.Definition.CriticalChancePercent,
                item.Definition.CriticalDamagePercent,
                item.Definition.AccuracyPercent,
                item.Definition.ArmorFlat,
                item.Definition.MagicResistanceFlat,
                item.Definition.DodgePercent,
                item.Definition.ArmorPenetrationPercent,
                item.Definition.MagicPenetrationPercent,
                item.Definition.AttackSpeedPercent,
                item.Definition.MaxResourceFlat));
    }

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
                item.EffectiveStats.Strength,
                item.EffectiveStats.Agility,
                item.EffectiveStats.Intellect,
                item.EffectiveStats.Stamina,
                item.Definition.MaxHpFlat,
                item.Definition.AttackPowerFlat,
                item.Definition.SpellPowerFlat,
                item.Definition.CriticalChancePercent,
                item.Definition.CriticalDamagePercent,
                item.Definition.AccuracyPercent,
                item.Definition.ArmorFlat,
                item.Definition.MagicResistanceFlat,
                item.Definition.DodgePercent,
                item.Definition.ArmorPenetrationPercent,
                item.Definition.MagicPenetrationPercent,
                item.Definition.AttackSpeedPercent,
                item.Definition.MaxResourceFlat),
            item.Definition.Description,
            item.Definition.SetId,
            item.Definition.WeaponCategory,
            item.Definition.ArmorCategory,
            item.Definition.AllowedClassIds ?? [],
            item.Definition.WeaponBaseAttackIntervalSeconds,
            item.Definition.AttackSpeedPercent,
            item.Definition.DodgePercent,
            ToConsumableActions(item.Definition),
            item.Definition.ConsumableCooldownCategoryId,
            item.Definition.ConsumableCooldownSeconds,
            item.Definition.BuyPriceGold,
            MerchantService.ResolveSellPrice(item.Definition),
            item.IsLocked,
            item.Definition.IconId,
            item.Definition.AppearanceProfileId,
            item.Definition.WeaponCategory is null
                ? null
                : EquipmentCategoryIds.UsesBothHands(item.Definition.WeaponCategory) ? 2 : 1,
            item.Definition.PrimaryStatRanges is not null);

    private static ConsumableActionResponse[] ToConsumableActions(
        ItemDefinition definition) =>
        (definition.ConsumableActions ?? [])
            .Select(action => new ConsumableActionResponse(
                action.Type.ToString(),
                action.Amount,
                action.ResourceType,
                action.EffectId,
                action.DispelCategory))
            .ToArray();

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}
