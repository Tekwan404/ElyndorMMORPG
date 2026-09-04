using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Content;

public sealed record ContentRevisionImportResult(
    ContentRevision Revision,
    ContentPackageParityResult Parity);

public sealed class ContentRevisionImporter(ContentRevisionStore revisionStore)
{
    public async Task<ContentRevisionImportResult> ImportAsync(
        string packagePath,
        string actor,
        string? note,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        GameContentPackage package =
            await GameContentPackageLoader.LoadAsync(packagePath, cancellationToken);
        string canonicalPayload =
            GameContentPackageCodec.SerializeCanonical(package);

        ContentPackageParityResult parity =
            ContentPackageParityVerifier.Verify(package, canonicalPayload);
        if (!parity.IsMatch)
        {
            throw new ContentPackageParityException(
                parity.SourceSha256,
                parity.RoundTripSha256);
        }

        ContentRevision revision = await revisionStore.CreateRevisionAsync(
            package,
            canonicalPayload,
            actor,
            note,
            cancellationToken);

        if (!string.Equals(
                revision.PayloadSha256,
                parity.SourceSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Persisted content revision SHA-256 does not match imported canonical payload.");
        }

        return new ContentRevisionImportResult(revision, parity);
    }

    public async Task<GameContentPackage?> LoadRevisionPackageAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default)
    {
        ContentRevision? revision =
            await revisionStore.GetRevisionAsync(revisionId, cancellationToken);
        if (revision is null)
            return null;

        string actualSha256 =
            GameContentPackageCodec.ComputeSha256(revision.PayloadJson);
        if (!string.Equals(
                actualSha256,
                revision.PayloadSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Content revision '{revision.Id}' failed SHA-256 integrity validation.");
        }

        GameContentPackage package =
            GameContentPackageCodec.DeserializeValidated(revision.PayloadJson);

        if (!string.Equals(
                package.ContentVersion,
                revision.ContentVersion,
                StringComparison.Ordinal)
            || !string.Equals(
                package.BalanceVersion,
                revision.BalanceVersion,
                StringComparison.Ordinal)
            || package.PublishedAtUtc != revision.SourcePublishedAtUtc)
        {
            throw new InvalidDataException(
                $"Content revision '{revision.Id}' metadata does not match its payload.");
        }

        return package;
    }
}
