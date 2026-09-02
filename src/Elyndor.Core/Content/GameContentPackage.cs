using Elyndor.Core.World;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;
using Elyndor.Core.Monsters;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Items;
using Elyndor.Core.Progression;

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
    IReadOnlyList<LootTableDefinition>? LootTables = null);

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
    AutoAttackProfile? CombatAutoAttack = null);

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
