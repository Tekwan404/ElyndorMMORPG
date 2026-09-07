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
    bool HasRandomStats = false);

public sealed record EquipmentSlotsResponse(
    InventoryItemResponse? Weapon,
    InventoryItemResponse? Head,
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
    ItemStatsResponse Stats);

public sealed record PendingLootResponse(
    IReadOnlyList<PendingLootItemResponse> Items);

public sealed record ClaimPendingLootRequest(Guid MutationId);
