using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Content;

public sealed record ContentPublicationResult(
    ContentRelease Release,
    ActiveContentRuntimeState RuntimeState);

public sealed class ContentPublicationConflictException(
    string expectedPayloadSha256,
    string actualPayloadSha256)
    : Exception("Live content changed before publication.")
{
    public string ExpectedPayloadSha256 { get; } = expectedPayloadSha256;
    public string ActualPayloadSha256 { get; } = actualPayloadSha256;
}

public sealed class ContentPublicationService(
    ContentRevisionStore revisionStore,
    ContentRevisionImporter revisionImporter,
    MutableContentSnapshotProvider snapshotProvider,
    ContentPublicationCoordinator coordinator)
{
    public Task<ContentPublicationResult?> PublishAsync(
        Guid revisionId,
        string actor,
        string? note,
        CancellationToken cancellationToken = default) =>
        PublishCoreAsync(
            revisionId,
            actor,
            note,
            expectedCurrentPayloadSha256: null,
            cancellationToken);

    public Task<ContentPublicationResult?> PublishAsync(
        Guid revisionId,
        string actor,
        string? note,
        string expectedCurrentPayloadSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentPayloadSha256);
        return PublishCoreAsync(
            revisionId,
            actor,
            note,
            expectedCurrentPayloadSha256,
            cancellationToken);
    }

    private async Task<ContentPublicationResult?> PublishCoreAsync(
        Guid revisionId,
        string actor,
        string? note,
        string? expectedCurrentPayloadSha256,
        CancellationToken cancellationToken)
    {
        if (revisionId == Guid.Empty)
            throw new ArgumentException("Revision id cannot be empty.", nameof(revisionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        await coordinator.Gate.WaitAsync(cancellationToken);
        try
        {
            if (expectedCurrentPayloadSha256 is not null)
            {
                string currentPayload = GameContentPackageCodec.SerializeCanonical(
                    snapshotProvider.GetCurrent().Package);
                string actualPayloadSha256 =
                    GameContentPackageCodec.ComputeSha256(currentPayload);
                if (!string.Equals(
                        expectedCurrentPayloadSha256,
                        actualPayloadSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ContentPublicationConflictException(
                        expectedCurrentPayloadSha256,
                        actualPayloadSha256);
                }
            }

            GameContentPackage? package =
                await revisionImporter.LoadRevisionPackageAsync(
                    revisionId,
                    cancellationToken);
            if (package is null)
                return null;

            GameContentSnapshot candidate =
                snapshotProvider.GetOrCreateRevisionSnapshot(
                    revisionId,
                    package);

            ContentRelease? release = await revisionStore.PublishAsync(
                revisionId,
                actor,
                note,
                cancellationToken);
            if (release is null)
            {
                throw new InvalidOperationException(
                    $"Content revision '{revisionId}' disappeared during publication.");
            }

            ActiveContentRuntimeState runtimeState =
                snapshotProvider.Activate(
                    revisionId,
                    release.Id,
                    candidate);

            return new ContentPublicationResult(release, runtimeState);
        }
        finally
        {
            coordinator.Gate.Release();
        }
    }

    public async Task<ContentPublicationResult?> RestoreLatestReleaseAsync(
        CancellationToken cancellationToken = default)
    {
        await coordinator.Gate.WaitAsync(cancellationToken);
        try
        {
            ContentRelease? release =
                await revisionStore.GetLatestReleaseAsync(cancellationToken);
            if (release is null)
                return null;

            GameContentPackage? package =
                await revisionImporter.LoadRevisionPackageAsync(
                    release.RevisionId,
                    cancellationToken);
            if (package is null)
            {
                throw new InvalidDataException(
                    $"Published content revision '{release.RevisionId}' is missing.");
            }

            GameContentSnapshot snapshot =
                snapshotProvider.GetOrCreateRevisionSnapshot(
                    release.RevisionId,
                    package);
            ActiveContentRuntimeState runtimeState =
                snapshotProvider.Activate(
                    release.RevisionId,
                    release.Id,
                    snapshot);

            return new ContentPublicationResult(release, runtimeState);
        }
        finally
        {
            coordinator.Gate.Release();
        }
    }
}
