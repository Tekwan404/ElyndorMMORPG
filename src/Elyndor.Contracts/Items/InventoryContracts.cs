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
    decimal HealAmount,
    decimal ConsumableCooldownSeconds,
    int BuyPriceGold,
    int SellPriceGold,
    string? IconId = null,
    string? AppearanceProfileId = null);

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

public sealed record EquipItemRequest(Guid CharacterItemId, Guid MutationId);

public sealed record UnequipItemRequest(string Slot, Guid MutationId);

public sealed record UseConsumableRequest(Guid CharacterItemId, Guid MutationId);

public sealed record MerchantItemResponse(
    string DefinitionId,
    string Name,
    string Type,
    string Rarity,
    string Description,
    int BuyPriceGold,
    int SellPriceGold,
    decimal HealAmount);

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
