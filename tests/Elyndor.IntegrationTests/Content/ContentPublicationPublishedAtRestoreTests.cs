using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Content;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ContentPublicationPublishedAtRestoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string EnhancementOreId = "ENHANCEMENT_ORE";
    private const string MarcusSuppliesId = "MARCUS_SUPPLIES";

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RestoreLatestReleaseUsesNewerBundledPublishedAtWhenSemanticVersionsMatch()
    {
        GameContentPackage bundled = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));

        Assert.Contains(bundled.Items ?? [], item => item.Id == EnhancementOreId);
        Assert.Contains(
            (bundled.Merchants ?? [])
                .Single(merchant => merchant.Id == MarcusSuppliesId)
                .ItemIds,
            itemId => itemId == EnhancementOreId);

        GameContentPackage stalePublished = bundled with
        {
            PublishedAtUtc = bundled.PublishedAtUtc.AddMinutes(-1),
            Items = (bundled.Items ?? [])
                .Where(item => item.Id != EnhancementOreId)
                .ToArray(),
            Merchants = (bundled.Merchants ?? [])
                .Select(merchant => merchant.Id == MarcusSuppliesId
                    ? merchant with
                    {
                        ItemIds = merchant.ItemIds
                            .Where(itemId => itemId != EnhancementOreId)
                            .ToArray()
                    }
                    : merchant)
                .ToArray(),
            PremiumStoreOffers = (bundled.PremiumStoreOffers ?? [])
                .Where(offer => offer.ItemDefinitionId != EnhancementOreId)
                .ToArray()
        };

        Assert.Equal(bundled.ContentVersion, stalePublished.ContentVersion);
        Assert.Equal(bundled.BalanceVersion, stalePublished.BalanceVersion);
        Assert.True(stalePublished.PublishedAtUtc < bundled.PublishedAtUtc);
        Assert.DoesNotContain(
            stalePublished.Items ?? [],
            item => item.Id == EnhancementOreId);
        Assert.DoesNotContain(
            (stalePublished.Merchants ?? [])
                .Single(merchant => merchant.Id == MarcusSuppliesId)
                .ItemIds,
            itemId => itemId == EnhancementOreId);
        Assert.DoesNotContain(
            stalePublished.PremiumStoreOffers ?? [],
            offer => offer.ItemDefinitionId == EnhancementOreId);

        MutableTimeProvider timeProvider = new(bundled.PublishedAtUtc.AddHours(1));
        Guid revisionId;
        Guid releaseId;
        string revisionPayloadSha256;

        await using (GameDbContext seedContext = postgres.CreateDbContext())
        {
            ContentRevisionStore seedStore = new(seedContext, timeProvider);
            string payload = GameContentPackageCodec.SerializeCanonical(stalePublished);
            ContentRevision revision = await seedStore.CreateRevisionAsync(
                stalePublished,
                payload,
                "integration-test",
                "production regression: stale package with equal aggregate versions",
                CancellationToken.None);
            revisionId = revision.Id;
            revisionPayloadSha256 = revision.PayloadSha256;

            MutableContentSnapshotProvider seedProvider = new(bundled);
            ContentPublicationService seedService = new(
                seedStore,
                new ContentRevisionImporter(seedStore),
                seedProvider,
                new ContentPublicationCoordinator());

            ContentPublicationResult published = (await seedService.PublishAsync(
                revision.Id,
                "integration-test",
                "publish stale revision before restart",
                CancellationToken.None))!;
            releaseId = published.Release.Id;

            Assert.DoesNotContain(
                seedProvider.GetCurrent().Package.Items ?? [],
                item => item.Id == EnhancementOreId);
        }

        await using GameDbContext runtimeContext = postgres.CreateDbContext();
        ContentRevisionStore runtimeStore = new(runtimeContext, timeProvider);
        MutableContentSnapshotProvider provider = new(bundled);
        ContentPublicationService service = new(
            runtimeStore,
            new ContentRevisionImporter(runtimeStore),
            provider,
            new ContentPublicationCoordinator());

        ContentPublicationResult restored = (await service.RestoreLatestReleaseAsync(
            CancellationToken.None))!;

        GameContentPackage restoredPackage = provider.GetCurrent().Package;
        Assert.Contains(restoredPackage.Items ?? [], item => item.Id == EnhancementOreId);
        Assert.Contains(
            (restoredPackage.Merchants ?? [])
                .Single(merchant => merchant.Id == MarcusSuppliesId)
                .ItemIds,
            itemId => itemId == EnhancementOreId);
        Assert.Equal(bundled.ContentVersion, restoredPackage.ContentVersion);
        Assert.Equal(bundled.BalanceVersion, restoredPackage.BalanceVersion);
        Assert.Equal(revisionId, restored.RuntimeState.RevisionId);
        Assert.Equal(releaseId, restored.RuntimeState.ReleaseId);
        Assert.Equal(revisionId, provider.GetRuntimeState().RevisionId);
        Assert.Equal(releaseId, provider.GetRuntimeState().ReleaseId);

        ContentRevision persistedRevision = (await runtimeStore.GetRevisionAsync(
            revisionId,
            CancellationToken.None))!;
        Assert.Equal(revisionPayloadSha256, persistedRevision.PayloadSha256);
        Assert.Equal(stalePublished.PublishedAtUtc, persistedRevision.SourcePublishedAtUtc);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
