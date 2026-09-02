using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

public enum ItemType
{
    Equipment,
    Material,
    Consumable
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare
}

public enum EquipmentSlot
{
    Weapon,
    Head,
    Chest,
    Legs,
    Boots,
    Accessory
}

public sealed record ItemDefinition(
    string Id,
    string Name,
    ItemType Type,
    ItemRarity Rarity,
    int RequiredLevel,
    bool Stackable,
    int MaxStack,
    EquipmentSlot? Slot,
    PrimaryStats Stats,
    string Description,
    int Version = 1,
    string? SetId = null,
    decimal? WeaponBaseAttackIntervalSeconds = null,
    decimal AttackSpeedPercent = 0,
    decimal DodgePercent = 0,
    decimal HealAmount = 0,
    decimal ConsumableCooldownSeconds = 0,
    int BuyPriceGold = 0,
    int SellPriceGold = 0);

public sealed record EquipmentSetBonusDefinition(
    int RequiredPieces,
    decimal AttackSpeedPercent = 0,
    decimal DodgePercent = 0);

public sealed record EquipmentSetDefinition(
    string Id,
    string Name,
    IReadOnlyList<EquipmentSetBonusDefinition> Bonuses);

public sealed record MerchantDefinition(
    string Id,
    string Name,
    string LocationId,
    string Description,
    IReadOnlyList<string> ItemIds);

public sealed record LootTableEntry(
    string ItemId,
    decimal DropChance,
    int MinQuantity,
    int MaxQuantity);

public sealed record LootTableDefinition(
    string Id,
    IReadOnlyList<LootTableEntry> Entries,
    int Version = 1);
