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
    string ContentVersion,
    string BalanceVersion);
