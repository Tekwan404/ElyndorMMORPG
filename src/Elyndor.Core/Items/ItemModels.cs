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

public static class EquipmentCategoryIds
{
    public const string OneHandSword = "ONE_HAND_SWORD";
    public const string TwoHandSword = "TWO_HAND_SWORD";
    public const string Axe = "AXE";
    public const string Mace = "MACE";
    public const string Shield = "SHIELD";
    public const string Bow = "BOW";
    public const string Dagger = "DAGGER";
    public const string Staff = "STAFF";
    public const string Wand = "WAND";

    public const string Light = "LIGHT";
    public const string Medium = "MEDIUM";
    public const string Heavy = "HEAVY";

    private static readonly HashSet<string> WeaponCategories = new(StringComparer.Ordinal)
    {
        OneHandSword,
        TwoHandSword,
        Axe,
        Mace,
        Shield,
        Bow,
        Dagger,
        Staff,
        Wand
    };

    private static readonly HashSet<string> ArmorCategories = new(StringComparer.Ordinal)
    {
        Light,
        Medium,
        Heavy
    };

    public static bool IsWeapon(string? category) =>
        category is not null && WeaponCategories.Contains(category);

    public static bool IsArmor(string? category) =>
        category is not null && ArmorCategories.Contains(category);
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
    int SellPriceGold = 0,
    string? WeaponCategory = null,
    string? ArmorCategory = null,
    IReadOnlyList<string>? AllowedClassIds = null);

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
