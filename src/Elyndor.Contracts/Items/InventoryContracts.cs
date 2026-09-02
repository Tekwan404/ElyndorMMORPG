namespace Elyndor.Contracts.Items;

public sealed record ItemStatsResponse(
    decimal Strength,
    decimal Agility,
    decimal Intellect,
    decimal Stamina);

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
    decimal? WeaponBaseAttackIntervalSeconds,
    decimal AttackSpeedPercent,
    decimal DodgePercent,
    decimal HealAmount,
    decimal ConsumableCooldownSeconds,
    int BuyPriceGold);

public sealed record EquipmentSlotsResponse(
    InventoryItemResponse? Weapon,
    InventoryItemResponse? Head,
    InventoryItemResponse? Chest,
    InventoryItemResponse? Legs,
    InventoryItemResponse? Boots,
    InventoryItemResponse? Accessory);

public sealed record InventoryResponse(
    IReadOnlyList<InventoryItemResponse> Items,
    EquipmentSlotsResponse Equipped);

public sealed record EquipItemRequest(Guid CharacterItemId);

public sealed record UnequipItemRequest(string Slot);

public sealed record UseConsumableRequest(Guid CharacterItemId);
