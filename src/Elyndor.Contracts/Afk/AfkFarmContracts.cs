namespace Elyndor.Contracts.Afk;

public sealed record StartAfkFarmRequest(string LocationId, string Mode, int DurationMinutes);

public sealed record PreviewAfkFarmRequest(string LocationId, string Mode, int DurationMinutes);

public sealed record AfkFarmPreviewResponse(
    string LocationId,
    string Mode,
    int DurationMinutes,
    int EncounteredEnemies,
    int Kills,
    int FailedKills,
    int EstimatedXp,
    int EstimatedGold,
    int PotentialLootRolls,
    decimal EstimatedIncomingDamage);

public sealed record AfkFarmStateResponse(
    Guid SessionId,
    string LocationId,
    string Mode,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset ProcessedUntilUtc,
    DateTimeOffset? CompletedAtUtc,
    string? StopReason,
    int Kills,
    int XpEarned,
    int GoldEarned,
    int ItemsCount);
