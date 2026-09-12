namespace Elyndor.Contracts.Afk;

public sealed record StartAfkFarmRequest(string LocationId, string Mode, int DurationMinutes);

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
