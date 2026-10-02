namespace Elyndor.Contracts.WorldBosses;

public sealed record WorldBossActiveResponse(
    Guid SpawnId,
    string BossDefinitionId,
    string Name,
    int Level,
    decimal CurrentHealth,
    decimal MaxHealth,
    int CurrentPhase,
    string PhaseName,
    DateTimeOffset SpawnedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int Participants,
    decimal PersonalDamage,
    decimal PartyDamage,
    bool RewardEligible,
    string? RewardTier,
    string? NextRewardTier,
    decimal? NextRewardTierAtDamage,
    decimal DamageToNextRewardTier,
    string ContentVersion,
    string BalanceVersion);

public sealed record WorldBossPersonalLeaderboardEntryResponse(
    int Rank,
    Guid CharacterId,
    string Name,
    decimal Damage);

public sealed record WorldBossPartyLeaderboardEntryResponse(
    int Rank,
    Guid PartyId,
    string LeaderName,
    decimal Damage);

public sealed record WorldBossLeaderboardResponse(
    Guid SpawnId,
    IReadOnlyList<WorldBossPersonalLeaderboardEntryResponse> Players,
    IReadOnlyList<WorldBossPartyLeaderboardEntryResponse> Parties,
    int? PersonalRank,
    decimal PersonalDamage,
    Guid? PartyId,
    int? PartyRank,
    decimal PartyDamage);


public sealed record WorldBossRewardItemResponse(
    string ItemId,
    string Name,
    string Rarity,
    int Quantity,
    string? IconId);

public sealed record WorldBossRewardResponse(
    Guid SpawnId,
    decimal Contribution,
    string Tier,
    int Experience,
    int BossGold,
    int ChestGold,
    int TotalGold,
    IReadOnlyList<WorldBossRewardItemResponse> Items,
    DateTimeOffset SettledAtUtc);


public sealed record WorldBossDefeatedResponse(
    Guid SpawnId,
    DateTimeOffset DefeatedAtUtc);

public sealed record WorldBossRewardsSettledResponse(
    Guid SpawnId,
    decimal Contribution,
    bool RewardEligible,
    WorldBossRewardResponse? Reward,
    DateTimeOffset SettledAtUtc);
