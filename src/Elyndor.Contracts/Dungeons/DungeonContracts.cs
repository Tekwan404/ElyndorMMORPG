namespace Elyndor.Contracts.Dungeons;

public sealed record DungeonPreviewResponse(
    string Id,
    string DisplayName,
    string Description,
    int MinimumLevel,
    int MaximumLevel,
    string EntryLocationId,
    int MinimumPartySize,
    int MaximumPartySize,
    IReadOnlyList<DungeonEncounterPreviewResponse> Encounters);

public sealed record DungeonEncounterPreviewResponse(
    string Id,
    string MonsterId,
    string CheckpointId,
    bool IsBoss);

public sealed record CreateDungeonRunRequest(Guid RequestId, string DungeonId);

public sealed record TeleportToDungeonRequest(Guid RequestId, string DungeonId);

public sealed record DungeonTeleportResponse(
    string DungeonId,
    string LocationId,
    long LocationVersion);

public sealed record DungeonRunResponse(
    Guid RunId,
    string DungeonId,
    string DisplayName,
    string Description,
    string State,
    int CurrentEncounterIndex,
    string CurrentCheckpointId,
    int EncounterCount,
    Guid PartyId,
    IReadOnlyList<DungeonRunMemberResponse> Members,
    IReadOnlyList<DungeonEncounterResponse> Encounters);

public sealed record DungeonRunMemberResponse(
    Guid CharacterId,
    string State,
    DateTimeOffset JoinedAtUtc);

public sealed record DungeonEncounterResponse(
    Guid EncounterId,
    int EncounterIndex,
    string MonsterId,
    string State,
    int WipeCount,
    IReadOnlyList<Guid> CharacterIds);
