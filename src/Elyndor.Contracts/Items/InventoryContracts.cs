using System.Globalization;

namespace Elyndor.Contracts.Items;

public sealed record ItemStatsResponse(
    decimal Strength,
    decimal Agility,
    decimal Intellect,
    decimal Stamina,
    decimal MaxHp,
    decimal AttackPower,
    decimal SpellPower,
    decimal CriticalChance,
    decimal CriticalDamage,
    decimal Accuracy,
    decimal Armor,
    decimal MagicResistance,
    decimal Dodge,
    decimal ArmorPenetration,
    decimal MagicPenetration,
    decimal AttackSpeed,
    decimal MaxResource);

public sealed record ConsumableActionResponse(
    string Type,
    decimal Amount,
    string? ResourceType,
    string? EffectId,
    string? DispelCategory);

public sealed record ItemAffixResponse(
    string SlotKey,
    string StatId,
    decimal Value,
    decimal Min,
    decimal Max,
    decimal Step,
    int AffixTier,
    bool IsGuaranteed,
    bool IsReforgeSlot);

public sealed record GeneratedItemSummaryResponse(
    int ItemLevel,
    decimal ItemPower,
    decimal MaxItemPower,
    decimal RollQuality,
    int Stars,
    bool IsPerfect,
    string? PerfectOrigin,
    string? GeneratedPrefixId,
    string? GeneratedSuffixId,
    string DisplayName,
    IReadOnlyList<ItemAffixResponse> Affixes);

public sealed record InventoryItemResponse(
    Guid Id,
    string DefinitionId,
    string Name,
    string Type,
    string Rarity,
    int RequiredLevel,
    int Quantity,
    string? Slot,
    string? EquippedSlot,
    ItemStatsResponse Stats,
    string Description,
    string? SetId,
    string? WeaponCategory,
    string? ArmorCategory,
    IReadOnlyList<string> AllowedClassIds,
    decimal? WeaponBaseAttackIntervalSeconds,
    decimal AttackSpeedPercent,
    decimal DodgePercent,
    IReadOnlyList<ConsumableActionResponse> ConsumableActions,
    string? ConsumableCooldownCategoryId,
    decimal ConsumableCooldownSeconds,
    int BuyPriceGold,
    int SellPriceGold,
    bool IsLocked,
    string? IconId = null,
    string? AppearanceProfileId = null,
    int? WeaponHandsRequired = null,
    bool HasRandomStats = false,
    GeneratedItemSummaryResponse? GeneratedItem = null,
    int ReforgeCount = 0,
    string? ReforgeSlotKey = null,
    bool TransactionLocked = false,
    string BindState = "UNBOUND",
    decimal BlockChancePercent = 0,
    decimal BlockValueMin = 0,
    decimal BlockValueMax = 0)
{
    public string Description { get; init; } = BuildDescription(
        Description,
        BlockChancePercent,
        BlockValueMin,
        BlockValueMax);

    private static string BuildDescription(
        string description,
        decimal blockChancePercent,
        decimal blockValueMin,
        decimal blockValueMax)
    {
        if (blockChancePercent <= 0 || blockValueMax <= 0)
            return description;
        if (description.Contains("Шанс блока:", StringComparison.Ordinal))
            return description;

        return $"{description}\n\nШанс блока: {FormatNumber(blockChancePercent)}%. Сила блока: {FormatNumber(blockValueMin)}–{FormatNumber(blockValueMax)}.";
    }

    private static string FormatNumber(decimal value) =>
        decimal.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}

public sealed record EquipmentSlotsResponse(
    InventoryItemResponse? Weapon,
    InventoryItemResponse? Head,
    InventoryItemResponse? Shoulders,
    InventoryItemResponse? Chest,
    InventoryItemResponse? Legs,
    InventoryItemResponse? Boots,
    InventoryItemResponse? Accessory,
    InventoryItemResponse? MainHand = null,
    InventoryItemResponse? OffHand = null,
    InventoryItemResponse? Hands = null,
    InventoryItemResponse? Feet = null,
    InventoryItemResponse? Cloak = null,
    InventoryItemResponse? Amulet = null,
    InventoryItemResponse? Ring1 = null,
    InventoryItemResponse? Ring2 = null);

public sealed record InventoryResponse(
    IReadOnlyList<InventoryItemResponse> Items,
    EquipmentSlotsResponse Equipped);

public sealed record EquipItemRequest(
    Guid CharacterItemId,
    Guid MutationId,
    string? TargetSlot = null);

public sealed record UnequipItemRequest(string Slot, Guid MutationId);

public sealed record UseConsumableRequest(Guid CharacterItemId, Guid MutationId);

public sealed record SetItemLockRequest(
    Guid CharacterItemId,
    bool IsLocked,
    Guid MutationId);

public sealed record MerchantItemResponse(
    string DefinitionId,
    string Name,
    string Type,
    string Rarity,
    string Description,
    int BuyPriceGold,
    int SellPriceGold,
    IReadOnlyList<ConsumableActionResponse> ConsumableActions,
    string? ConsumableCooldownCategoryId,
    decimal ConsumableCooldownSeconds,
    string? IconId);

public sealed record MerchantResponse(
    string Id,
    string Name,
    string Description,
    long Gold,
    IReadOnlyList<MerchantItemResponse> Items);

public sealed record BuyMerchantItemRequest(
    string MerchantId,
    string ItemDefinitionId,
    Guid MutationId,
    int Quantity = 1);

public sealed record SellMerchantItemRequest(
    string MerchantId,
    Guid CharacterItemId,
    Guid MutationId,
    int Quantity = 1);

public sealed record PendingLootItemResponse(
    Guid Id,
    string DefinitionId,
    string Name,
    string Type,
    string Rarity,
    int Quantity,
    DateTimeOffset CreatedAtUtc,
    ItemStatsResponse Stats,
    GeneratedItemSummaryResponse? GeneratedItem = null);

public sealed record PendingLootResponse(
    IReadOnlyList<PendingLootItemResponse> Items);

public sealed record ClaimPendingLootRequest(Guid MutationId);

public sealed record RollItemReforgeRequest(
    Guid CharacterItemId,
    string SlotKey,
    Guid OperationId);

public sealed record DecideItemReforgeRequest(
    Guid OperationId,
    bool AcceptProposed);

public sealed record UpgradeItemStarsRequest(Guid CharacterItemId, Guid MutationId);

public sealed record ItemStarUpgradeResponse(Guid ItemInstanceId, GeneratedItemSummaryResponse Item);

public sealed record ItemReforgeCostResponse(
    int Gold,
    string MaterialItemId,
    int MaterialQuantity,
    string CatalystItemId,
    int CatalystQuantity,
    decimal CountMultiplier);

public sealed record ItemReforgeResponse(
    Guid OperationId,
    string State,
    Guid ItemInstanceId,
    string SlotKey,
    GeneratedItemSummaryResponse Current,
    GeneratedItemSummaryResponse Proposed,
    ItemReforgeCostResponse Cost);

public sealed record ItemReforgePreviewResponse(
    Guid ItemInstanceId,
    string SlotKey,
    GeneratedItemSummaryResponse Current,
    ItemReforgeCostResponse Cost);

public sealed record ItemSalvageRewardResponse(
    string ReforgeStoneItemId,
    int ReforgeStoneQuantity,
    string MaterialItemId,
    int MaterialQuantity);

public sealed record ItemSalvagePreviewResponse(
    Guid CharacterItemId,
    ItemSalvageRewardResponse Reward,
    bool RequiresConfirmation);

public sealed record SalvageItemRequest(
    Guid CharacterItemId,
    Guid MutationId,
    bool ConfirmedHighValue = false);

public sealed record ItemSalvageResponse(ItemSalvageRewardResponse Reward);
