namespace Elyndor.Contracts.Combat;

public sealed record CombatEffectResponse(string Id, int Stacks, DateTimeOffset ExpiresAtUtc);
public sealed record CombatAbilityResponse(string Id, decimal ResourceCost, double CooldownSeconds);

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
    IReadOnlyList<CombatEffectResponse> Effects);

public sealed record CombatSnapshotResponse(
    Guid SessionId,
    long Sequence,
    string Status,
    DateTimeOffset ServerTimeUtc,
    CombatActorResponse Player,
    CombatActorResponse Enemy);

public sealed record CombatEventResponse(
    long Sequence,
    string Type,
    Guid ActorId,
    Guid? SourceActorId,
    Guid? TargetActorId,
    string? DefinitionId,
    decimal Amount,
    DateTimeOffset ServerTimeUtc);

public sealed record CombatUpdateResponse(
    bool Succeeded,
    string? ErrorCode,
    CombatSnapshotResponse? Snapshot,
    IReadOnlyList<CombatEventResponse> Events);
