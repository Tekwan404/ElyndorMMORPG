using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Contracts.Items;
using Elyndor.Core.Items;
using Elyndor.Core.Content;
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
        group.MapGet("/salvage/preview/{characterItemId:guid}", GetSalvagePreviewAsync);
        group.MapPost("/salvage", SalvageAsync);
        group.MapGet("/reforge/pending", GetPendingReforgeAsync);
        group.MapGet("/reforge/preview/{characterItemId:guid}", GetReforgePreviewAsync);
        group.MapPost("/reforge/roll", RollReforgeAsync);
        group.MapPost("/reforge/decide", DecideReforgeAsync);
        group.MapPost("/star-upgrade", UpgradeItemStarsAsync);
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

    private static async Task<IResult> GetPendingReforgeAsync(
        Guid? characterItemId,
        ClaimsPrincipal user,
        ItemReforgeService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        ItemReforgeOperationResult? result = await service.GetPendingAsync(
            accountId,
            characterItemId,
            cancellationToken);
        return result is null
            ? Results.NoContent()
            : Results.Ok(ToReforgeResponse(result));
    }

    private static async Task<IResult> GetReforgePreviewAsync(
        Guid characterItemId,
        string slotKey,
        ClaimsPrincipal user,
        HttpContext context,
        ItemReforgeService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        ItemReforgePreviewResult result = await service.GetPreviewAsync(accountId, characterItemId, slotKey, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new ItemReforgePreviewResponse(
                characterItemId,
                slotKey,
                ToGeneratedItemResponse(result.Current!)!,
                ToReforgeCostResponse(result.Cost!)))
            : ReforgeProblem(result.ErrorCode!, context);
    }

    private static async Task<IResult> RollReforgeAsync(
        RollItemReforgeRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        ItemReforgeService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                ItemReforgeOperationResult result = await service.RollAsync(
                    accountId,
                    request.CharacterItemId,
                    request.SlotKey,
                    request.OperationId,
                    cancellationToken);
                return result.Succeeded
                    ? Results.Ok(ToReforgeResponse(result))
                    : ReforgeProblem(result.ErrorCode!, context);
            },
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> DecideReforgeAsync(
        DecideItemReforgeRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        ItemReforgeService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                ItemReforgeOperationResult result = await service.DecideAsync(
                    accountId,
                    request.OperationId,
                    request.AcceptProposed,
                    cancellationToken);
                return result.Succeeded
                    ? Results.Ok(ToReforgeResponse(result))
                    : ReforgeProblem(result.ErrorCode!, context);
            },
            () => InCombatProblem(context),
            cancellationToken);
    }

    private static async Task<IResult> UpgradeItemStarsAsync(
        UpgradeItemStarsRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        ItemStarUpgradeService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(accountId, async () =>
        {
            ItemStarUpgradeResult result = await service.UpgradeAsync(accountId, request.CharacterItemId, request.MutationId, cancellationToken);
            return result.Succeeded
                ? Results.Ok(new ItemStarUpgradeResponse(request.CharacterItemId, ToGeneratedItemResponse(result.Item!)!))
                : StarUpgradeProblem(result.ErrorCode!, context);
        }, () => InCombatProblem(context), cancellationToken);
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

    private static async Task<IResult> GetSalvagePreviewAsync(
        Guid characterItemId,
        ClaimsPrincipal user,
        HttpContext context,
        ItemSalvageService service,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        ItemSalvagePreviewResult result = await service.GetPreviewAsync(accountId, characterItemId, cancellationToken);
        return result.Succeeded
            ? Results.Ok(new ItemSalvagePreviewResponse(
                characterItemId,
                ToSalvageRewardResponse(result.Reward!),
                result.RequiresConfirmation))
            : SalvageProblem(result.ErrorCode!, context);
    }

    private static async Task<IResult> SalvageAsync(
        SalvageItemRequest request,
        ClaimsPrincipal user,
        HttpContext context,
        ItemSalvageService service,
        CharacterOperationGuard operationGuard,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId)) return Results.Unauthorized();
        return await operationGuard.ExecuteOutOfCombatAsync(
            accountId,
            async () =>
            {
                ItemSalvageOperationResult result = await service.SalvageAsync(
                    accountId,
                    request.CharacterItemId,
                    request.MutationId,
                    request.ConfirmedHighValue,
                    cancellationToken);
                return result.Succeeded
                    ? Results.Ok(new ItemSalvageResponse(ToSalvageRewardResponse(result.Reward!)))
                    : SalvageProblem(result.ErrorCode!, context);
            },
            () => InCombatProblem(context),
            cancellationToken);
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
                GetEquipped(snapshot, EquipmentSlot.Shoulders),
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

    private static IResult ReforgeProblem(string errorCode, HttpContext context) =>
        Results.Problem(
            statusCode: errorCode is ItemReforgeErrorCodes.ItemNotFound
                    or ItemReforgeErrorCodes.ProposalNotFound
                    or ItemReforgeErrorCodes.CharacterNotFound
                ? StatusCodes.Status404NotFound
                : errorCode is ItemReforgeErrorCodes.ItemTransactionLocked
                    or ItemReforgeErrorCodes.ItemLocked
                    or ItemReforgeErrorCodes.OperationConflict
                    or ItemReforgeErrorCodes.ProposalNotPending
                    or ItemReforgeErrorCodes.ItemEquipped
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status422UnprocessableEntity,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = errorCode,
                ["correlationId"] = context.TraceIdentifier
            });

    private static IResult StarUpgradeProblem(string errorCode, HttpContext context) =>
        Results.Problem(statusCode: errorCode is ItemStarUpgradeErrorCodes.ItemNotFound or ItemStarUpgradeErrorCodes.CharacterNotFound
                ? StatusCodes.Status404NotFound
                : errorCode is ItemStarUpgradeErrorCodes.ItemLocked or ItemStarUpgradeErrorCodes.ItemEquipped
                    or ItemStarUpgradeErrorCodes.ItemTransactionLocked or ItemStarUpgradeErrorCodes.MaxStars
                    or ItemStarUpgradeErrorCodes.MutationConflict
                    ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity,
            extensions: new Dictionary<string, object?> { ["code"] = errorCode, ["correlationId"] = context.TraceIdentifier });

    private static IResult SalvageProblem(string errorCode, HttpContext context) =>
        Results.Problem(
            statusCode: errorCode is ItemSalvageErrorCodes.CharacterNotFound or ItemSalvageErrorCodes.ItemNotFound
                ? StatusCodes.Status404NotFound
                : errorCode is ItemSalvageErrorCodes.Conflict
                    or ItemSalvageErrorCodes.MutationConflict
                    or ItemSalvageErrorCodes.ItemLocked
                    or ItemSalvageErrorCodes.ItemEquipped
                    or ItemSalvageErrorCodes.ItemTransactionLocked
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
                    or MerchantErrorCodes.TransactionLocked
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
                item.Definition.MaxResourceFlat),
            ToGeneratedItemResponse(item.GeneratedItem));
    }

    internal static InventoryItemResponse ToResponse(InventoryItemSnapshot item)
    {
        GeneratedItemInstance? generated = item.GeneratedItem;
        decimal blockChanceBonus = generated?.Affixes
            .Where(affix => string.Equals(affix.StatId, ItemStatIds.BlockChance, StringComparison.Ordinal))
            .Sum(affix => affix.Value) ?? 0m;
        decimal blockValueBonus = generated?.Affixes
            .Where(affix => string.Equals(affix.StatId, ItemStatIds.BlockValue, StringComparison.Ordinal))
            .Sum(affix => affix.Value) ?? 0m;

        return new InventoryItemResponse(
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
            item.Definition.PrimaryStatRanges is not null || generated is not null,
            ToGeneratedItemResponse(generated),
            item.ReforgeCount,
            item.ReforgeSlotKey,
            item.TransactionLocked,
            item.BindState,
            item.Definition.BlockChancePercent + blockChanceBonus,
            item.Definition.BlockValueMin + blockValueBonus,
            item.Definition.BlockValueMax + blockValueBonus);
    }

    private static GeneratedItemSummaryResponse? ToGeneratedItemResponse(
        GeneratedItemInstance? generated) =>
        generated is null
            ? null
            : new GeneratedItemSummaryResponse(
                generated.ItemLevel,
                generated.ActualItemPower,
                generated.MaxTemplateItemPower,
                generated.RollQuality,
                generated.Stars,
                generated.IsPerfect,
                generated.PerfectOrigin,
                generated.GeneratedPrefixId,
                generated.GeneratedSuffixId,
                generated.DisplayName,
                generated.Affixes
                    .OrderBy(affix => affix.GenerationOrdinal)
                    .Select(affix => new ItemAffixResponse(
                        affix.SlotKey,
                        affix.StatId,
                        affix.Value,
                        affix.MinAtGeneration,
                        affix.MaxAtGeneration,
                        affix.StepAtGeneration,
                        affix.AffixTier,
                        affix.IsGuaranteed,
                        affix.IsReforgeSlot))
                    .ToArray());

    private static ItemReforgeResponse ToReforgeResponse(
        ItemReforgeOperationResult result)
    {
        ItemReforgeOperation operation = result.Operation
            ?? throw new InvalidOperationException("Successful Reforge result requires an operation.");
        GeneratedItemInstance current = result.Current
            ?? throw new InvalidOperationException("Successful Reforge result requires current item state.");
        GeneratedItemInstance proposed = result.Proposed
            ?? throw new InvalidOperationException("Successful Reforge result requires proposed item state.");
        ItemReforgeCost cost = result.Cost
            ?? throw new InvalidOperationException("Successful Reforge result requires cost.");

        return new ItemReforgeResponse(
            operation.OperationId,
            operation.State.ToString(),
            operation.ItemInstanceId,
            operation.SlotKey,
            ToGeneratedItemResponse(current)!,
            ToGeneratedItemResponse(proposed)!,
            new ItemReforgeCostResponse(
                cost.Gold,
                cost.MaterialItemId,
                cost.MaterialQuantity,
                cost.CatalystItemId,
                cost.CatalystQuantity,
                cost.CountMultiplier));
    }

    private static ItemReforgeCostResponse ToReforgeCostResponse(ItemReforgeCost cost) =>
        new(cost.Gold, cost.MaterialItemId, cost.MaterialQuantity, cost.CatalystItemId, cost.CatalystQuantity, cost.CountMultiplier);

    private static ItemSalvageRewardResponse ToSalvageRewardResponse(ItemSalvageYield reward) => new(
        reward.ReforgeStoneItemId,
        reward.ReforgeStoneQuantity,
        reward.MaterialItemId,
        reward.MaterialQuantity);

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