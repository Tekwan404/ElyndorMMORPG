using Elyndor.Contracts.Items;

namespace Elyndor.Contracts.World;

public sealed record WorldLocationResponse(
    string Id,
    string DisplayName,
    string DangerLevel,
    int RecommendedLevel);

public sealed record BootstrapAbilityResponse(
    string Id,
    decimal ResourceCost,
    decimal CooldownSeconds,
    string Type,
    string TargetType,
    string? SourceTalentId,
    string? SourceTalentName);

public sealed record CharacterStatContributionResponse(
    string Source,
    decimal Value);

public sealed record CharacterStatBreakdownResponse(
    decimal FinalValue,
    IReadOnlyList<CharacterStatContributionResponse> Contributions);

public sealed record BootstrapCharacterResponse(
    Guid Id,
    string Name,
    string RaceId,
    string GenderId,
    string ClassId,
    int Level,
    long Experience,
    int XpToNextLevel,
    string PrimaryAttribute,
    string ClassProfileVersion,
    IReadOnlyList<string> KnownAbilityIds,
    IReadOnlyList<BootstrapAbilityResponse> KnownAbilities,
    CharacterStatsResponse Stats,
    IReadOnlyDictionary<string, CharacterStatBreakdownResponse> StatBreakdown,
    CharacterVitalsResponse Vitals,
    InventoryResponse Inventory);

public sealed record CharacterStatsResponse(
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
    decimal ArmorPenetration,
    decimal MagicPenetration,
    decimal AttackSpeed,
    decimal Armor,
    decimal MagicResistance,
    decimal Dodge);

public sealed record CharacterVitalsResponse(
    decimal CurrentHp,
    decimal MaxHp,
    string ResourceType,
    decimal CurrentResource,
    decimal MaxResource,
    DateTimeOffset CheckpointedAtUtc);

public sealed record BootstrapWorldResponse(
    WorldLocationResponse CurrentLocation,
    long Version,
    IReadOnlyList<WorldLocationResponse> OutgoingTransitions);

public sealed record BootstrapResponse(
    Guid AccountId,
    BootstrapCharacterResponse? Character,
    BootstrapWorldResponse? World,
    string ContentVersion,
    string BalanceVersion,
    DateTimeOffset ServerTimeUtc);

public sealed record TravelRequest(Guid RequestId, string TargetLocationId);

public sealed record TravelResponse(string LocationId, long Version);
