using Elyndor.Core.Items;

namespace Elyndor.Infrastructure.Items;

/// <summary>
/// Legacy API error aliases kept while old clients migrate from star-upgrade to enhancement.
/// Stars are no longer mutated by this service.
/// </summary>
public static class ItemStarUpgradeErrorCodes
{
    public const string CharacterNotFound = "star_upgrade_character_not_found";
    public const string ItemNotFound = "star_upgrade_item_not_found";
    public const string ItemNotGenerated = "star_upgrade_item_not_generated";
    public const string ItemLocked = "star_upgrade_item_locked";
    public const string ItemEquipped = "star_upgrade_item_equipped";
    public const string ItemTransactionLocked = "star_upgrade_item_transaction_locked";
    public const string MaxStars = "star_upgrade_max_stars";
    public const string ProfileMissing = "star_upgrade_profile_missing";
    public const string NotEnoughGold = "star_upgrade_not_enough_gold";
    public const string NotEnoughStones = "star_upgrade_not_enough_stones";
    public const string MissingCatalyst = "star_upgrade_missing_catalyst";
    public const string MutationConflict = "star_upgrade_mutation_conflict";
    public const string Conflict = "star_upgrade_conflict";
}

public sealed record ItemStarUpgradeResult(bool Succeeded, string? ErrorCode, GeneratedItemInstance? Item)
{
    public static ItemStarUpgradeResult Failure(string code) => new(false, code, null);
}

public sealed record ItemStarUpgradePreviewResult(
    bool Succeeded,
    string? ErrorCode,
    Guid ItemInstanceId,
    int TargetStars,
    int Gold,
    string ReforgeStoneItemId,
    int ReforgeStoneQuantity,
    string? CatalystItemId,
    int CatalystQuantity)
{
    public static ItemStarUpgradePreviewResult Failure(string code) =>
        new(false, code, Guid.Empty, 0, 0, string.Empty, 0, null, 0);
}

/// <summary>
/// Backward-compatible facade for clients that still call /star-upgrade.
/// TargetStars carries the target enhancement level only on this legacy contract.
/// The generated item's Stars/RollQuality/Perfect fields remain immutable.
///
/// This facade intentionally is not marked Obsolete at type level because ASP.NET endpoint
/// method signatures still bind it through DI while old routes remain supported. Deprecation
/// is an API-contract concern; making the DI type obsolete turns warnings-as-errors builds red.
/// </summary>
public sealed class ItemStarUpgradeService(ItemEnhancementService enhancementService)
{
    public async Task<ItemStarUpgradeResult> UpgradeAsync(
        Guid accountId,
        Guid itemId,
        Guid mutationId,
        CancellationToken cancellationToken)
    {
        ItemEnhancementResult result = await enhancementService.EnhanceAsync(
            accountId,
            itemId,
            mutationId,
            cancellationToken);
        return result.Succeeded
            ? new ItemStarUpgradeResult(true, null, result.Item)
            : ItemStarUpgradeResult.Failure(ToLegacyError(result.ErrorCode));
    }

    public async Task<ItemStarUpgradePreviewResult> GetPreviewAsync(
        Guid accountId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        ItemEnhancementPreviewResult result = await enhancementService.GetPreviewAsync(
            accountId,
            itemId,
            cancellationToken);
        if (!result.Succeeded)
            return ItemStarUpgradePreviewResult.Failure(ToLegacyError(result.ErrorCode));

        return new ItemStarUpgradePreviewResult(
            true,
            null,
            result.ItemInstanceId,
            result.TargetEnhancementLevel,
            result.Gold,
            result.EnhancementMaterialItemId,
            result.EnhancementMaterialQuantity,
            result.CatalystItemId,
            result.CatalystQuantity);
    }

    private static string ToLegacyError(string? code) => code switch
    {
        ItemEnhancementErrorCodes.CharacterNotFound => ItemStarUpgradeErrorCodes.CharacterNotFound,
        ItemEnhancementErrorCodes.ItemNotFound => ItemStarUpgradeErrorCodes.ItemNotFound,
        ItemEnhancementErrorCodes.ItemNotGenerated => ItemStarUpgradeErrorCodes.ItemNotGenerated,
        ItemEnhancementErrorCodes.ItemLocked => ItemStarUpgradeErrorCodes.ItemLocked,
        ItemEnhancementErrorCodes.ItemTransactionLocked => ItemStarUpgradeErrorCodes.ItemTransactionLocked,
        ItemEnhancementErrorCodes.MaxEnhancement => ItemStarUpgradeErrorCodes.MaxStars,
        ItemEnhancementErrorCodes.ProfileMissing => ItemStarUpgradeErrorCodes.ProfileMissing,
        ItemEnhancementErrorCodes.NotEnoughGold => ItemStarUpgradeErrorCodes.NotEnoughGold,
        ItemEnhancementErrorCodes.NotEnoughMaterial => ItemStarUpgradeErrorCodes.NotEnoughStones,
        ItemEnhancementErrorCodes.MissingCatalyst => ItemStarUpgradeErrorCodes.MissingCatalyst,
        ItemEnhancementErrorCodes.MutationConflict => ItemStarUpgradeErrorCodes.MutationConflict,
        _ => ItemStarUpgradeErrorCodes.Conflict,
    };
}
