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

            ContentRevision? revision =
                await revisionStore.GetRevisionAsync(
                    release.RevisionId,
                    cancellationToken);
            if (revision is null)
            {
                throw new InvalidDataException(
                    $"Published content revision '{release.RevisionId}' is missing.");
            }

            GameContentPackage bundledPackage =
                snapshotProvider.GetCurrent().Package;
            GameContentPackage? publishedPackage =
                await revisionImporter.LoadRevisionPackageAsync(
                    release.RevisionId,
                    cancellationToken);
            if (publishedPackage is null)
            {
                throw new InvalidDataException(
                    $"Published content revision '{release.RevisionId}' is missing.");
            }

            // Category fragments can add runtime definitions without increasing the already-highest
            // package content version. When the bundled and published versions are equal, keep the
            // bundled definitions as the authoritative application baseline and layer published-only
            // extensions on top. Otherwise a stale DB snapshot with the same version can erase a
            // newly bundled dungeon/location/item after every production restart.
            GameContentPackage package = IsSameOrNewerContentVersion(
                    bundledPackage.ContentVersion,
                    revision.ContentVersion)
                ? MergePublishedExtensions(
                    publishedPackage,
                    bundledPackage)
                : publishedPackage;

            IReadOnlyList<ContentValidationError> errors =
                ContentValidationPipeline.Default.Validate(package);
            if (errors.Count > 0)
                throw new ContentPackageValidationException(errors);

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

    private static bool IsSameOrNewerContentVersion(
        string bundledContentVersion,
        string publishedContentVersion)
    {
        if (string.Equals(
                bundledContentVersion,
                publishedContentVersion,
                StringComparison.Ordinal))
        {
            return true;
        }

        return Version.TryParse(bundledContentVersion, out Version? bundled)
            && Version.TryParse(publishedContentVersion, out Version? published)
            && bundled > published;
    }

    private static GameContentPackage MergePublishedExtensions(
        GameContentPackage published,
        GameContentPackage bundled) =>
        bundled with
        {
            Definitions = ContentCompositionRules.MergeByKey(
                published.Definitions,
                bundled.Definitions,
                item => (item.Type, item.Id)),
            Locations = ContentCompositionRules.MergeByKey(
                published.Locations,
                bundled.Locations,
                item => item.Id),
            ClassProfiles = ContentCompositionRules.MergeOptionalByKey(
                published.ClassProfiles,
                bundled.ClassProfiles,
                item => item.Id),
            ResourceProfiles = ContentCompositionRules.MergeOptionalByKey(
                published.ResourceProfiles,
                bundled.ResourceProfiles,
                item => item.Id),
            Effects = ContentCompositionRules.MergeOptionalByKey(
                published.Effects,
                bundled.Effects,
                item => item.Id),
            Abilities = ContentCompositionRules.MergeOptionalByKey(
                published.Abilities,
                bundled.Abilities,
                item => item.Id),
            TalentTrees = ContentCompositionRules.MergeOptionalByKey(
                published.TalentTrees,
                bundled.TalentTrees,
                item => item.Id),
            Monsters = ContentCompositionRules.MergeOptionalByKey(
                published.Monsters,
                bundled.Monsters,
                item => item.Id),
            MonsterAiProfiles = ContentCompositionRules.MergeOptionalByKey(
                published.MonsterAiProfiles,
                bundled.MonsterAiProfiles,
                item => item.Id),
            Items = ContentCompositionRules.MergeOptionalByKey(
                published.Items,
                bundled.Items,
                item => item.Id),
            LootTables = ContentCompositionRules.MergeOptionalByKey(
                published.LootTables,
                bundled.LootTables,
                item => item.Id),
            EquipmentSets = ContentCompositionRules.MergeOptionalByKey(
                published.EquipmentSets,
                bundled.EquipmentSets,
                item => item.Id),
            Merchants = ContentCompositionRules.MergeOptionalByKey(
                published.Merchants,
                bundled.Merchants,
                item => item.Id),
            WorldContracts = ContentCompositionRules.MergeOptionalByKey(
                published.WorldContracts,
                bundled.WorldContracts,
                item => item.Id)
        };
}
