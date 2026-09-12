namespace Elyndor.Contracts.Afk;

public sealed record StartAfkFarmRequest(string LocationId, int DurationMinutes, string? TargetMonsterId = null);

public sealed record PreviewAfkFarmRequest(string LocationId, int DurationMinutes, string? TargetMonsterId = null);

public sealed record AfkFarmTargetResponse(string MonsterId, string DisplayName);

public sealed record AfkFarmPreviewResponse(
    string LocationId,
    string? TargetMonsterId,
    int DurationMinutes,
    int EncounteredEnemies,
    int Kills,
    int FailedKills,
    int EstimatedXp,
    int EstimatedGold,
    int PotentialLootRolls,
    int EfficiencyPercent);

public sealed record AfkFarmStateResponse(
    Guid SessionId,
    string LocationId,
    string? TargetMonsterId,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset ProcessedUntilUtc,
    DateTimeOffset? CompletedAtUtc,
    string? StopReason,
    int Kills,
    int XpEarned,
    int GoldEarned,
    int ItemsCount,
    int EfficiencyPercent);
