using Elyndor.Core.Content;

namespace Elyndor.Core.Items;

public enum ItemType
{
    Equipment,
    Material,
    Consumable,
    SpatialArtifact
}

public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Unique
}

public enum ConsumableActionType
{
    RestoreHp,
    RestoreResource,
    ApplyEffect,
    RemoveEffect
}

public sealed record ConsumableActionDefinition(
    ConsumableActionType Type,
    decimal Amount = 0,
    string? ResourceType = null,
    string? EffectId = null,
    string? DispelCategory = null);

public enum EquipmentSlot
{
    // Modern canonical slots.
    MainHand,
    OffHand,
    Head,
    Shoulders,
    Chest,
    Hands,
    Legs,
    Feet,
    Cloak,
    Amulet,
    Ring1,
    Ring2,

    // Legacy slots kept during content migration.
    Weapon,
    Boots,
    Accessory,
    Waist,
    Wrist
}

public sealed record ItemStatRange(
    decimal Min,
    decimal Max,
    decimal Step = 1);

public sealed record PrimaryStatRanges(
    ItemStatRange? Strength = null,
    ItemStatRange? Agility = null,
    ItemStatRange? Intellect = null,
    ItemStatRange? Stamina = null);

public static class EquipmentCategoryIds
{
    public const string OneHandSword = "ONE_HAND_SWORD";
    public const string TwoHandSword = "TWO_HAND_SWORD";
    public const string TwoHandAxe = "TWO_HAND_AXE";
    public const string TwoHandMace = "TWO_HAND_MACE";
    public const string Polearm = "POLEARM";
    public const string Axe = "AXE";
    public const string Mace = "MACE";
    public const string Bow = "BOW";
    public const string Crossbow = "CROSSBOW";
    public const string Dagger = "DAGGER";
    public const string Staff = "STAFF";
    public const string Wand = "WAND";

    public const string Cloth = "CLOTH";
    public const string Leather = "LEATHER";
    public const string Heavy = "HEAVY";

    public const string Shield = "SHIELD";
    public const string Focus = "FOCUS";
    public const string Quiver = "QUIVER";

    // Legacy identifiers remain as constants only so old tooling can produce a clear
    // validation error instead of failing to compile. They are not valid categories.
    public const string Light = "LIGHT";
    public const string Medium = "MEDIUM";

    private static readonly HashSet<string> WeaponCategories = new(StringComparer.Ordinal)
    {
        OneHandSword,
        TwoHandSword,
        TwoHandAxe,
        TwoHandMace,
        Polearm,
        Axe,
        Mace,
        Bow,
        Crossbow,
        Dagger,
        Staff,
        Wand
    };

    private static readonly HashSet<string> ArmorCategories = new(StringComparer.Ordinal)
    {
        Cloth,
        Leather,
        Heavy
    };

    private static readonly HashSet<string> OffHandCategories = new(StringComparer.Ordinal)
    {
        Shield,
        Focus,
        Quiver
    };

    public static bool IsWeapon(string? category) =>
        category is not null && WeaponCategories.Contains(category);

    public static bool IsArmor(string? category) =>
        category is not null && ArmorCategories.Contains(category);

    public static bool IsOffHand(string? category) =>
        category is not null && OffHandCategories.Contains(category);

    public static bool UsesBothHands(string? weaponCategory) =>
        weaponCategory is TwoHandSword or TwoHandAxe or TwoHandMace or Polearm or Bow or Crossbow or Staff;

    public static bool IsOneHandedWeapon(string? weaponCategory) =>
        IsWeapon(weaponCategory) && !UsesBothHands(weaponCategory);
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
    decimal ConsumableCooldownSeconds = 0,
    IReadOnlyList<ConsumableActionDefinition>? ConsumableActions = null,
    string? ConsumableCooldownCategoryId = null,
    int BuyPriceGold = 0,
    int SellPriceGold = 0,
    string? WeaponCategory = null,
    string? ArmorCategory = null,
    decimal MaxHpFlat = 0,
    decimal AttackPowerFlat = 0,
    decimal SpellPowerFlat = 0,
    decimal CriticalChancePercent = 0,
    decimal CriticalDamagePercent = 0,
    decimal AccuracyPercent = 0,
    decimal ArmorFlat = 0,
    decimal MagicResistanceFlat = 0,
    decimal ArmorPenetrationPercent = 0,
    decimal MagicPenetrationPercent = 0,
    decimal MaxResourceFlat = 0,
    string? IconId = null,
    string? AppearanceProfileId = null,
    PrimaryStatRanges? PrimaryStatRanges = null,
    decimal? WeaponDamageMin = null,
    decimal? WeaponDamageMax = null,
    string? OffHandCategory = null,
    decimal BlockChancePercent = 0,
    decimal BlockValueMin = 0,
    decimal BlockValueMax = 0,
    int? ItemLevelMin = null,
    int? ItemLevelMax = null,
    IReadOnlyList<string>? GuaranteedAffixStatIds = null,
    string? RandomAffixPoolId = null,
    string? AffixCountProfileId = null,
    decimal ExtraAffixBudgetCap = 0,
    string? PrefixSuffixPolicyId = null,
    string? UniqueEquippedGroup = null,
    string? TradePolicyId = null,
    int GenerationVersion = 1,
    bool PremiumEligible = true,
    int InventoryCapacityBonus = 0);

public sealed record EquipmentSetBonusDefinition(
    int RequiredPieces,
    decimal AttackSpeedPercent = 0,
    decimal DodgePercent = 0,
    decimal MaxHpFlat = 0,
    decimal AttackPowerFlat = 0,
    decimal SpellPowerFlat = 0,
    decimal CriticalChancePercent = 0,
    decimal CriticalDamagePercent = 0,
    decimal AccuracyPercent = 0,
    decimal ArmorFlat = 0,
    decimal MagicResistanceFlat = 0,
    decimal ArmorPenetrationPercent = 0,
    decimal MagicPenetrationPercent = 0,
    decimal MaxResourceFlat = 0);

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

public sealed record PremiumStoreOfferDefinition(
    string Sku,
    string ItemDefinitionId,
    int Quantity,
    long CrystalPrice,
    bool Enabled = true,
    int? PerAccountLimit = null);

public sealed record LootTableEntry(
    string ItemId,
    decimal DropChance,
    int MinQuantity,
    int MaxQuantity);

public sealed record LootSelectionEntry(
    string ItemId,
    decimal Weight,
    int MinQuantity = 1,
    int MaxQuantity = 1);

public sealed record LootSelectionGroup(
    string Id,
    int Rolls,
    string SelectionMode,
    IReadOnlyList<LootSelectionEntry> Entries);

public sealed record LootTableDefinition(
    string Id,
    IReadOnlyList<LootTableEntry> Entries,
    int Version = 1,
    IReadOnlyList<LootSelectionGroup>? SelectionGroups = null);