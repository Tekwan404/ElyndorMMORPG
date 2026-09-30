using Elyndor.Contracts.Combat;

namespace Elyndor.Contracts.Arena;

public sealed record ArenaQueueRequest(string? Mode);

public sealed record ArenaStatusResponse(
    bool Enabled,
    long Honor,
    int Rating,
    int Wins,
    int Losses,
    int Draws,
    bool IsQueued,
    string? QueueMode,
    DateTimeOffset? QueuedAtUtc,
    Guid? ActiveMatchId);

public sealed record ArenaQueueResponse(bool Succeeded, string? ErrorCode, ArenaStatusResponse? Status);

public sealed record ArenaLeaderboardEntryResponse(
    int Rank,
    Guid CharacterId,
    string Name,
    int Rating,
    int Wins,
    int Losses,
    int Draws);

/// <summary>Viewer-relative arena match. <c>Battle</c> reuses the regular BattleScreen contract.</summary>
public sealed record ArenaMatchResponse(
    string Status,
    Guid? MatchId,
    Guid CharacterId,
    Guid? OpponentCharacterId,
    string? OpponentName,
    string Outcome,
    string? Result,
    long Sequence,
    CombatSnapshotResponse? Battle,
    IReadOnlyList<CombatEventResponse> Events);

public sealed record ArenaCommandResponse(bool Succeeded, string? ErrorCode, ArenaMatchResponse? Match);

public sealed record ArenaMatchNotification(Guid MatchId, long Sequence);
