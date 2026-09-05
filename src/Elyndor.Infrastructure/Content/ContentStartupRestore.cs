using Elyndor.Core.Content;

namespace Elyndor.Infrastructure.Content;

public sealed record ContentStartupRestoreResult(
    ContentPublicationResult? Publication,
    Exception? FileFallbackReason)
{
    public bool RestoredPublishedContent => Publication is not null;

    public bool UsedFileFallback => FileFallbackReason is not null;
}

public static class ContentStartupRestore
{
    public static async Task<ContentStartupRestoreResult> RestoreAsync(
        ContentPublicationService publicationService,
        bool allowFileFallback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publicationService);

        try
        {
            ContentPublicationResult? publication =
                await publicationService.RestoreLatestReleaseAsync(cancellationToken);
            return new ContentStartupRestoreResult(publication, null);
        }
        catch (Exception exception)
            when (allowFileFallback && IsRecoverableContentFailure(exception))
        {
            return new ContentStartupRestoreResult(null, exception);
        }
    }

    private static bool IsRecoverableContentFailure(Exception exception) =>
        exception is ContentPackageValidationException
            or InvalidDataException;
}
