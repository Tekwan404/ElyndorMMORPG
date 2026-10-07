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
    decimal BlockValueMax = 0,
    decimal? WeaponDamageMin = null,
    decimal? WeaponDamageMax = null,
    EquipmentSetSummaryResponse? SetSummary = null)
{
    // RequiredLevel is an equip gate. ItemLevel is a separate power/progression value.
    // Generated instances already own their authoritative resolved ItemLevel, so expose it
    // directly on the inventory item without asking clients to conflate it with RequiredLevel.
    public int? ItemLevel => GeneratedItem?.ItemLevel;

    public string Description { get; init; } = BuildDescription(
        Description,
        BlockChancePercent,
        BlockValueMin,
        BlockValueMax,
        WeaponDamageMin,
        WeaponDamageMax);

    private static string BuildDescription(
        string description,
        decimal blockChancePercent,
        decimal blockValueMin,
        decimal blockValueMax,
        decimal? weaponDamageMin,
        decimal? weaponDamageMax)
    {
        List<string> details = [];

        if (weaponDamageMin.HasValue
            && weaponDamageMax.HasValue
            && weaponDamageMin.Value >= 0
            && weaponDamageMax.Value >= weaponDamageMin.Value
            && weaponDamageMax.Value > 0
            && !description.Contains("Урон оружия:", StringComparison.Ordinal))
        {
            details.Add(
                $"Урон оружия: {FormatNumber(weaponDamageMin.Value)}–{FormatNumber(weaponDamageMax.Value)}.");
        }

        if (blockChancePercent > 0
            && blockValueMax > 0
            && !description.Contains("Шанс блока:", StringComparison.Ordinal))
        {
            details.Add(
                $"Шанс блока: {FormatNumber(blockChancePercent)}%. Сила блока: {FormatNumber(blockValueMin)}–{FormatNumber(blockValueMax)}.");
        }

        return details.Count == 0
            ? description
            : $"{description}\n\n{string.Join(" ", details)}";
    }

    private static string FormatNumber(decimal value) =>
        decimal.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}

public sealed record EquipmentSlotsResponse(
    InventoryItemResponse? MainHand,
    InventoryItemResponse? OffHand,
    InventoryItemResponse? Head,
    InventoryItemResponse? Shoulders,
    InventoryItemResponse? Chest,
    InventoryItemResponse? Hands,
    InventoryItemResponse? Legs,
    InventoryItemResponse? Feet,
    InventoryItemResponse? Cloak,
    InventoryItemResponse? Amulet,
    InventoryItemResponse? Ring1,
    InventoryItemResponse? Ring2);

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
    string? IconId,
    int RequiredLevel,
    string? Slot,
    ItemStatsResponse Stats,
    string? WeaponCategory,
    string? ArmorCategory);

public sealed record MerchantBuybackItemResponse(
    Guid CharacterItemId,
    string DefinitionId,
    string Name,
    string Type,
    string Rarity,
    int Quantity,
    int BuybackPriceGold,
    string? IconId,
    int EnhancementLevel);

public sealed record MerchantResponse(
    string Id,
    string Name,
    string Description,
    [property: global::System.Text.Json.Serialization.JsonConverter(typeof(Elyndor.Contracts.Economy.MoneyJsonConverter))] long Gold,
    IReadOnlyList<MerchantItemResponse> Items,
    IReadOnlyList<MerchantBuybackItemResponse>? BuybackItems = null);

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

public sealed record SellMerchantItemSelectionRequest(
    Guid CharacterItemId,
    int Quantity);

public sealed record SellMerchantItemsRequest(
    string MerchantId,
    Guid MutationId,
    IReadOnlyList<SellMerchantItemSelectionRequest> Items);

public sealed record BuybackMerchantItemRequest(
    string MerchantId,
    Guid CharacterItemId,
    Guid MutationId);

public sealed record PendingLootItemResponse(
    Guid Id,
    Guid RewardResolutionId,
    string DefinitionId,
    string Name,
    string Type,
    string Rarity,
    int Quantity,
    DateTimeOffset CreatedAtUtc,
    ItemStatsResponse Stats,
    GeneratedItemSummaryResponse? GeneratedItem = null,
    string? IconId = null,
    string? SourceType = null);

public sealed record PendingLootResponse(
    IReadOnlyList<PendingLootItemResponse> Items);

public sealed record ClaimPendingLootRequest(
    Guid MutationId,
    IReadOnlyList<Guid>? ItemIds = null);

public sealed record DiscardPendingLootRequest(
    Guid MutationId,
    IReadOnlyList<Guid> ItemIds);

public sealed record DiscardInventoryItemRequest(
    Guid CharacterItemId,
    int Quantity);

public sealed record DiscardInventoryItemsRequest(
    Guid MutationId,
    IReadOnlyList<DiscardInventoryItemRequest> Items);

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

public sealed record ItemReforgePossibleAffixResponse(
    string StatId,
    decimal Min,
    decimal Max,
    decimal Step);

public sealed record ItemReforgePreviewResponse(
    Guid ItemInstanceId,
    string SlotKey,
    GeneratedItemSummaryResponse Current,
    ItemReforgeCostResponse Cost,
    IReadOnlyList<ItemReforgePossibleAffixResponse> PossibleAffixes);

public sealed record ItemSalvageRewardResponse(
    string ReforgeStoneItemId,
    int ReforgeStoneQuantity,
    string MaterialItemId,
    int MaterialQuantity);

public sealed record ItemEnhancementSalvageRefundResponse(
    string? EnhancementMaterialItemId,
    int EnhancementMaterialQuantity,
    string? CatalystItemId,
    int CatalystQuantity);

public sealed record ItemSalvagePreviewResponse(
    Guid CharacterItemId,
    ItemSalvageRewardResponse Reward,
    bool RequiresConfirmation,
    ItemEnhancementSalvageRefundResponse EnhancementRefund);

public sealed record SalvageItemRequest(
    Guid CharacterItemId,
    Guid MutationId,
    bool ConfirmedHighValue = false);

public sealed record ItemSalvageResponse(
    ItemSalvageRewardResponse Reward,
    ItemEnhancementSalvageRefundResponse EnhancementRefund);

public sealed record EquipmentSetBonusResponse(int RequiredPieces, string Description);
public sealed record EquipmentSetSummaryResponse(string Name, int TotalPieces, IReadOnlyList<EquipmentSetBonusResponse> Bonuses);
