using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Items;
using Elyndor.Core.Progression;
using Elyndor.Core.Dungeons;
using Elyndor.Core.Quests;
using Elyndor.Core.Economy;

namespace Elyndor.Core.Content;

public sealed record GameContentPackage(
    string ContentVersion,
    string BalanceVersion,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<GameContentDefinition> Definitions,
    IReadOnlyList<LocationDefinition> Locations,
    IReadOnlyList<ClassProfile>? ClassProfiles = null,
    StatFormulaProfile? StatFormula = null,
    IReadOnlyList<ResourceProfile>? ResourceProfiles = null,
    IReadOnlyList<EffectDefinition>? Effects = null,
    IReadOnlyList<AbilityDefinition>? Abilities = null,
    IReadOnlyList<TalentTreeDefinition>? TalentTrees = null,
    IReadOnlyList<MonsterDefinition>? Monsters = null,
    IReadOnlyList<MonsterAiProfile>? MonsterAiProfiles = null,
    LevelProgressionDefinition? LevelProgression = null,
    IReadOnlyList<ItemDefinition>? Items = null,
    IReadOnlyList<LootTableDefinition>? LootTables = null,
    IReadOnlyList<EquipmentSetDefinition>? EquipmentSets = null,
    IReadOnlyList<MerchantDefinition>? Merchants = null,
    ResourceScalingProfile? ResourceScaling = null,
    IReadOnlyList<WorldContractDefinition>? WorldContracts = null,
    IReadOnlyList<DungeonDefinition>? Dungeons = null,
    InventoryProfileDefinition? InventoryProfile = null,
    IReadOnlyList<QuestDefinition>? Quests = null,
    ItemizationDefinition? Itemization = null,
    IReadOnlyList<PremiumStoreOfferDefinition>? PremiumStoreOffers = null,
    IReadOnlyList<PromoCodeDefinition>? PromoCodes = null);

public sealed record GameContentDefinition(
    string Type,
    string Id,
    IReadOnlyList<GameContentReference> References);

public sealed record GameContentReference(string Type, string Id);

public sealed record ContentValidationError(string Code, string Path, string Message);

public sealed record PrimaryStats(
    decimal Strength,
    decimal Agility,
    decimal Intellect,
    decimal Stamina);

public sealed record ClassProfile(
    string Id,
    string PrimaryAttribute,
    string ResourceProfileId,
    PrimaryStats BaseStats,
    PrimaryStats LevelGrowth,
    IReadOnlyList<string> AllowedWeaponCategories,
    IReadOnlyList<string> AllowedArmorCategories,
    string PrototypeIdentity,
    IReadOnlyList<string>? StartingAbilityIds = null,
    IReadOnlyList<AbilityUnlockDefinition>? AbilityUnlocks = null,
    AutoAttackProfile? CombatAutoAttack = null,
    IReadOnlyList<string>? AllowedOffHandCategories = null,
    bool AllowUnarmed = false,
    IReadOnlyList<CompanionProfileDefinition>? CompanionProfiles = null,
    string? StartingCompanionProfileId = null,
    IReadOnlyList<string>? StartingEquipmentItemIds = null);

public sealed record CompanionProfileDefinition(
    string Id,
    string Tag,
    string Archetype,
    string Name,
    decimal MaxHpBase,
    decimal MaxHpPerOwnerStamina,
    decimal AttackPowerOwnerCoefficient,
    decimal SpellPowerOwnerCoefficient,
    decimal BaseDamageMin,
    decimal BaseDamageMax,
    TimeSpan AutoAttackInterval,
    decimal Accuracy = 95,
    decimal Dodge = 5,
    decimal CriticalChance = 5,
    decimal CriticalDamage = 1.5m,
    decimal Armor = 0,
    decimal MagicResistance = 0,
    string? ArtId = null);

public sealed record AbilityUnlockDefinition(string AbilityId, int UnlockLevel);

public sealed record StatFormulaProfile(
    string Id,
    decimal MaxHpBase,
    decimal MaxHpPerStamina,
    decimal AttackPowerPerStrength,
    decimal AttackPowerPerAgility,
    decimal SpellPowerPerIntellect,
    decimal ArmorPerStamina,
    decimal ArmorPerStrength,
    decimal MagicResistancePerStamina,
    decimal MagicResistancePerIntellect,
    decimal CriticalChanceBase,
    decimal CriticalChancePerAgility,
    decimal CriticalDamageBase,
    decimal AccuracyBase,
    decimal DodgePerAgility,
    decimal AttackSpeedBase);

public sealed record ResourceProfile(
    string Id,
    decimal MaxValue,
    decimal StartValue,
    decimal RespawnValue,
    decimal CombatRegenPerSecond,
    decimal OutOfCombatRegenPerSecond,
    decimal OutOfCombatDecayPerSecond,
    decimal OutOfCombatDelaySeconds);

public sealed record ResourceScalingProfile(
    decimal ManaBase,
    decimal ManaPerIntellect);

public sealed record InventoryProfileDefinition(int DefaultCapacity);
