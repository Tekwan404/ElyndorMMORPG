using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Content;

public sealed record ContentAdminRuntimeState(
    GameContentPackage Package,
    Guid? RevisionId,
    Guid? ReleaseId,
    string PayloadJson,
    string PayloadSha256);

public sealed record ContentDraftValidationResult(
    bool IsValid,
    GameContentPackage? Package,
    string? CanonicalPayloadJson,
    string? PayloadSha256,
    IReadOnlyList<ContentValidationError> Errors);

public sealed record ContentAdminHistory(
    IReadOnlyList<ContentRevision> Revisions,
    IReadOnlyList<ContentRelease> Releases);

public sealed class ContentDraftConflictException(
    string expectedPayloadSha256,
    string actualPayloadSha256)
    : Exception("Draft was created from stale live content.")
{
    public string ExpectedPayloadSha256 { get; } = expectedPayloadSha256;
    public string ActualPayloadSha256 { get; } = actualPayloadSha256;
}

public sealed class ContentPayloadTooLargeException(int maxCharacters)
    : Exception($"Content payload exceeds {maxCharacters} characters.")
{
    public int MaxCharacters { get; } = maxCharacters;
}

public sealed class ContentDraftValidationException(
    IReadOnlyList<ContentValidationError> errors)
    : Exception("Content draft is invalid.")
{
    public IReadOnlyList<ContentValidationError> Errors { get; } = errors;
}

public sealed class ContentAdministrationService(
    ContentRevisionStore revisionStore,
    ContentPublicationService publicationService,
    MutableContentSnapshotProvider snapshotProvider)
{
    public const int MaxPayloadCharacters = 2_000_000;

    public ContentAdminRuntimeState GetCurrent()
    {
        ActiveContentRuntimeState runtime = snapshotProvider.GetRuntimeState();
        string payload =
            GameContentPackageCodec.SerializeCanonical(runtime.Snapshot.Package);
        return new ContentAdminRuntimeState(
            runtime.Snapshot.Package,
            runtime.RevisionId,
            runtime.ReleaseId,
            payload,
            GameContentPackageCodec.ComputeSha256(payload));
    }

    public ContentDraftValidationResult ValidateDraft(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return Invalid(new ContentValidationError(
                "CONTENT_PAYLOAD_EMPTY",
                "$",
                "Content payload cannot be empty."));
        }

        if (payloadJson.Length > MaxPayloadCharacters)
        {
            return Invalid(new ContentValidationError(
                "CONTENT_PAYLOAD_TOO_LARGE",
                "$",
                $"Content payload cannot exceed {MaxPayloadCharacters} characters."));
        }

        try
        {
            GameContentPackage package =
                GameContentPackageCodec.DeserializeValidated(payloadJson);
            string canonical =
                GameContentPackageCodec.SerializeCanonical(package);
            return new ContentDraftValidationResult(
                true,
                package,
                canonical,
                GameContentPackageCodec.ComputeSha256(canonical),
                []);
        }
        catch (ContentPackageValidationException exception)
        {
            return new ContentDraftValidationResult(
                false,
                null,
                null,
                null,
                exception.Errors);
        }
        catch (InvalidDataException exception)
        {
            return Invalid(new ContentValidationError(
                "CONTENT_JSON_INVALID",
                "$",
                exception.Message));
        }
    }

    public async Task<ContentRevision> CreateDraftAsync(
        string payloadJson,
        string basePayloadSha256,
        string actor,
        string? note,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePayloadSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        if (payloadJson.Length > MaxPayloadCharacters)
            throw new ContentPayloadTooLargeException(MaxPayloadCharacters);

        ContentAdminRuntimeState current = GetCurrent();
        if (!string.Equals(
                basePayloadSha256,
                current.PayloadSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ContentDraftConflictException(
                basePayloadSha256,
                current.PayloadSha256);
        }

        ContentDraftValidationResult validation = ValidateDraft(payloadJson);
        if (!validation.IsValid)
            throw new ContentDraftValidationException(validation.Errors);

        return await revisionStore.CreateRevisionAsync(
            validation.Package!,
            validation.CanonicalPayloadJson!,
            actor,
            note,
            cancellationToken);
    }

    public Task<ContentPublicationResult?> PublishAsync(
        Guid revisionId,
        string expectedLivePayloadSha256,
        string actor,
        string? note,
        CancellationToken cancellationToken = default) =>
        publicationService.PublishAsync(
            revisionId,
            actor,
            note,
            expectedLivePayloadSha256,
            cancellationToken);

    public async Task<ContentPublicationResult?> RollbackAsync(
        Guid releaseId,
        string expectedLivePayloadSha256,
        string actor,
        string? note,
        CancellationToken cancellationToken = default)
    {
        ContentRelease? target =
            await revisionStore.GetReleaseAsync(releaseId, cancellationToken);
        if (target is null)
            return null;

        string rollbackNote = string.IsNullOrWhiteSpace(note)
            ? $"rollback release {releaseId}"
            : $"rollback release {releaseId}: {note.Trim()}";

        return await publicationService.PublishAsync(
            target.RevisionId,
            actor,
            rollbackNote,
            expectedLivePayloadSha256,
            cancellationToken);
    }

    public async Task<ContentAdminHistory> GetHistoryAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        int take = Math.Clamp(limit, 1, 100);
        IReadOnlyList<ContentRevision> revisions =
            await revisionStore.GetRecentRevisionsAsync(take, cancellationToken);
        IReadOnlyList<ContentRelease> releases =
            await revisionStore.GetRecentReleasesAsync(take, cancellationToken);
        return new ContentAdminHistory(revisions, releases);
    }

    private static ContentDraftValidationResult Invalid(
        ContentValidationError error) =>
        new(false, null, null, null, [error]);
}
