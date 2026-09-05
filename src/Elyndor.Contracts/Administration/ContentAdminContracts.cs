namespace Elyndor.Contracts.Administration;

public sealed record ContentAdminCurrentResponse(
    string ContentVersion,
    string BalanceVersion,
    DateTimeOffset SourcePublishedAtUtc,
    Guid? RevisionId,
    Guid? ReleaseId,
    string PayloadSha256,
    string PayloadJson);

public sealed record ContentAdminValidateRequest(string PayloadJson);

public sealed record ContentAdminValidationErrorResponse(
    string Code,
    string Path,
    string Message);

public sealed record ContentAdminValidationResponse(
    bool IsValid,
    string? CanonicalPayloadJson,
    string? PayloadSha256,
    IReadOnlyList<ContentAdminValidationErrorResponse> Errors);

public sealed record ContentAdminCreateRevisionRequest(
    string PayloadJson,
    string BasePayloadSha256,
    string? Note);

public sealed record ContentAdminPublishRequest(
    string ExpectedLivePayloadSha256,
    string? Note);

public sealed record ContentAdminRollbackRequest(
    string ExpectedLivePayloadSha256,
    string? Note);

public sealed record ContentAdminRevisionResponse(
    Guid Id,
    string ContentVersion,
    string BalanceVersion,
    DateTimeOffset SourcePublishedAtUtc,
    string PayloadSha256,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    string? Note);

public sealed record ContentAdminRevisionDetailResponse(
    Guid Id,
    string ContentVersion,
    string BalanceVersion,
    DateTimeOffset SourcePublishedAtUtc,
    string PayloadSha256,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    string? Note,
    string PayloadJson);

public sealed record ContentAdminReleaseResponse(
    Guid Id,
    Guid RevisionId,
    DateTimeOffset PublishedAtUtc,
    string PublishedBy,
    string? Note);

public sealed record ContentAdminHistoryResponse(
    IReadOnlyList<ContentAdminRevisionResponse> Revisions,
    IReadOnlyList<ContentAdminReleaseResponse> Releases);


public sealed record ContentAdminSimulationRequest(
    string PayloadJson,
    string ClassId,
    int PlayerLevel,
    string MonsterId,
    int Iterations,
    int Seed,
    int MaxDurationSeconds,
    IReadOnlyList<string>? AbilityPriority = null);

public sealed record ContentAdminSimulationDamageSourceResponse(
    string DefinitionId,
    decimal AverageDamage,
    decimal DamageSharePercent);

public sealed record ContentAdminSimulationResponse(
    string ContentVersion,
    string BalanceVersion,
    string ClassId,
    int PlayerLevel,
    string MonsterId,
    int Iterations,
    int Victories,
    int Defeats,
    int Timeouts,
    decimal WinRatePercent,
    decimal AverageDurationSeconds,
    decimal P50DurationSeconds,
    decimal P95DurationSeconds,
    decimal AveragePlayerDps,
    decimal AverageEnemyDps,
    decimal AveragePlayerRemainingHp,
    IReadOnlyList<ContentAdminSimulationDamageSourceResponse> DamageSources);
