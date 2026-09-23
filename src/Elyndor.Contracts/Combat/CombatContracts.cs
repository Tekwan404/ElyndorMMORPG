namespace Elyndor.Contracts.Combat;

public sealed record CombatEffectResponse(
    string Id,
    int Stacks,
    DateTimeOffset ExpiresAtUtc,
    string? DisplayName = null,
    string? Description = null,
    string? IconId = null);
public sealed record CombatCastResponse(
    string AbilityId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ResolvesAtUtc);
public sealed record CombatAbilityResponse(
    string Id,
    string DisplayName,
    string Description,
    string? IconId,
    decimal ResourceCost,
    double CooldownSeconds,
    bool IsUnblockable = false,
    string TargetType = "SingleEnemy");

public sealed record CombatActorResponse(
    Guid ActorId,
    string Kind,
    string DefinitionId,
    string Name,
    decimal Hp,
    decimal MaxHp,
    string ResourceType,
    decimal Resource,
    decimal MaxResource,
    bool AutoAttackEnabled,
    IReadOnlyDictionary<string, DateTimeOffset> Cooldowns,
    IReadOnlyList<string> KnownAbilityIds,
    IReadOnlyList<CombatAbilityResponse> Abilities,
    IReadOnlyList<CombatEffectResponse> Effects,
    int Level = 1,
    string? ArtId = null,
    CombatCastResponse? ActiveCast = null,
    IReadOnlyDictionary<string, DateTimeOffset>? ConsumableCooldowns = null,
    double? AutoAttackIntervalSeconds = null,
    DateTimeOffset? NextAutoAttackAtUtc = null,
    Guid? CurrentAggroTargetActorId = null,
    string? GenderId = null,
    string? MonsterRank = null);

public sealed record CombatContributionResponse(
    Guid CharacterId,
    int QualifyingActions,
    decimal DamageDealt,
    decimal EffectiveHealing,
    decimal SupportContribution,
    decimal TankingContribution,
    DateTimeOffset JoinedAtUtc,
    DateTimeOffset? FledAtUtc,
    DateTimeOffset? DiedAtUtc);

public sealed record CombatParticipantResponse(
    Guid AccountId,
    Guid CharacterId,
    Guid ActorId,
    string Status,
    DateTimeOffset RosteredAtUtc,
    DateTimeOffset? JoinedAtUtc,
    DateTimeOffset? FledAtUtc,
    DateTimeOffset? DiedAtUtc);

public sealed record CombatParticipantContributionResponse(
    CombatContributionResponse Contribution,
    bool IsEligible,
    string Reason,
    decimal ContributionScore);

public sealed record CombatThreatEntryResponse(
    Guid ActorId,
    string Name,
    decimal Threat,
    bool IsCurrentTarget,
    Guid? SelectedTargetActorId);

public sealed record CombatThreatResponse(
    Guid EnemyActorId,
    string EnemyName,
    Guid? CurrentTargetActorId,
    Guid? ForcedTargetActorId,
    IReadOnlyList<CombatThreatEntryResponse> Entries);

public sealed record CombatSnapshotResponse(
    Guid SessionId,
    long Sequence,
    string Status,
    DateTimeOffset ServerTimeUtc,
    CombatActorResponse Player,
    CombatActorResponse Enemy,
    string ContentVersion = "UNVERSIONED",
    string BalanceVersion = "UNVERSIONED",
    IReadOnlyList<CombatActorResponse>? Enemies = null,
    Guid? SelectedTargetActorId = null,
    CombatActorResponse? Companion = null,
    CombatContributionResponse? PlayerContribution = null,
    IReadOnlyList<CombatActorResponse>? Players = null,
    IReadOnlyList<CombatParticipantResponse>? ParticipantRoster = null,
    bool? PlayerContributionEligible = null,
    IReadOnlyList<CombatParticipantContributionResponse>? ParticipantContributions = null);

public sealed record CombatEventResponse(
    long Sequence,
    string Type,
    Guid ActorId,
    Guid? SourceActorId,
    Guid? TargetActorId,
    string? DefinitionId,
    decimal Amount,
    DateTimeOffset ServerTimeUtc,
    decimal AmountBeforeShields = 0,
    string? WeaponHand = null,
    string? WeaponDefinitionId = null,
    decimal RawDamage = 0,
    decimal DamageAfterMitigation = 0,
    decimal DamageBeforeBlock = 0,
    bool IsUnblockable = false);

public sealed record CombatRewardItemResponse(
    string ItemId,
    string Name,
    string Type,
    string Rarity,
    int Quantity,
    string? IconId = null);

public sealed record CombatLootRollResponse(
    Guid LootRollId,
    string ItemId,
    string Name,
    string Rarity,
    int Quantity,
    DateTimeOffset EndsAtUtc,
    IReadOnlyList<Guid> EligibleCharacterIds,
    bool CanNeed,
    string? IconId = null,
    string? Type = null);

public sealed record CombatLootRollChoiceResponse(
    bool Succeeded,
    string? ErrorCode,
    CombatLootRollResponse? Roll,
    Guid? WinnerCharacterId);

public sealed record CombatRewardResponse(
    int XpEarned,
    int GoldEarned,
    bool LeveledUp,
    int PreviousLevel,
    int CurrentLevel,
    IReadOnlyList<CombatRewardItemResponse> Items,
    IReadOnlyList<string>? CompletedContractIds = null,
    IReadOnlyList<CombatLootRollResponse>? LootRolls = null);

public sealed record CombatUpdateResponse(
    bool Succeeded,
    string? ErrorCode,
    CombatSnapshotResponse? Snapshot,
    IReadOnlyList<CombatEventResponse> Events,
    CombatRewardResponse? Reward);
