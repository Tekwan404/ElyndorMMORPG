using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Content;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ContentPublicationServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 4, 19, 25, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PublishSwitchesRuntimeAndRollbackReusesCachedSnapshot()
    {
        MutableTimeProvider timeProvider = new(Start);
        await using GameDbContext context = postgres.CreateDbContext();

        GameContentPackage initial = CreatePackage("content-0", "balance-0", Start);
        MutableContentSnapshotProvider provider = new(initial);
        ContentRevisionStore store = new(context, timeProvider);
        ContentRevisionImporter importer = new(store);
        ContentPublicationService service = new(
            store,
            importer,
            provider,
            new ContentPublicationCoordinator());

        ContentRevision first = await CreateRevisionAsync(
            store,
            CreatePackage("content-1", "balance-1", Start.AddMinutes(1)),
            "first");
        ContentPublicationResult firstPublish = (await service.PublishAsync(
            first.Id,
            "integration-test",
            "publish first",
            CancellationToken.None))!;

        GameContentSnapshot firstSnapshot = provider.GetCurrent();
        Assert.Equal("content-1", firstSnapshot.ContentVersion);
        Assert.Equal(first.Id, provider.GetRuntimeState().RevisionId);
        Assert.Equal(firstPublish.Release.Id, provider.GetRuntimeState().ReleaseId);
        Assert.Equal(1, provider.CachedRevisionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        ContentRevision second = await CreateRevisionAsync(
            store,
            CreatePackage("content-2", "balance-2", Start.AddMinutes(2)),
            "second");
        await service.PublishAsync(
            second.Id,
            "integration-test",
            "publish second",
            CancellationToken.None);

        Assert.Equal("content-2", provider.GetCurrent().ContentVersion);
        Assert.Equal(2, provider.CachedRevisionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        ContentPublicationResult rollback = (await service.PublishAsync(
            first.Id,
            "integration-test",
            "rollback first",
            CancellationToken.None))!;

        Assert.Same(firstSnapshot, provider.GetCurrent());
        Assert.Equal(first.Id, rollback.RuntimeState.RevisionId);
        Assert.Equal(2, provider.CachedRevisionCount);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(3, await verify.ContentReleases.CountAsync());
    }

    [Fact]
    public async Task InvalidRevisionNeverCreatesReleaseOrChangesRuntime()
    {
        await using GameDbContext context = postgres.CreateDbContext();
        MutableContentSnapshotProvider provider =
            new(CreatePackage("content-0", "balance-0", Start));
        ContentRevisionStore store =
            new(context, new MutableTimeProvider(Start));

        ContentRevision revision = await CreateRevisionAsync(
            store,
            CreatePackage("content-1", "balance-1", Start.AddMinutes(1)),
            "valid-before-tamper");

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE game.content_revisions SET \"PayloadJson\" = '{{\"tampered\":true}}' WHERE \"Id\" = {revision.Id}");
        context.ChangeTracker.Clear();

        ContentPublicationService service = new(
            store,
            new ContentRevisionImporter(store),
            provider,
            new ContentPublicationCoordinator());

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.PublishAsync(
                revision.Id,
                "integration-test",
                null,
                CancellationToken.None));

        Assert.Equal("content-0", provider.GetCurrent().ContentVersion);
        Assert.Null(provider.GetRuntimeState().RevisionId);
        Assert.Equal(0, provider.CachedRevisionCount);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.ContentReleases.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task RestoreLatestReleaseActivatesPublishedRevisionAfterRestart()
    {
        MutableTimeProvider timeProvider = new(Start);
        Guid revisionId;
        Guid releaseId;

        await using (GameDbContext seedContext = postgres.CreateDbContext())
        {
            ContentRevisionStore seedStore = new(seedContext, timeProvider);
            ContentRevision revision = await CreateRevisionAsync(
                seedStore,
                CreatePackage("content-9", "balance-4", Start.AddMinutes(1)),
                "published");
            ContentRelease release = (await seedStore.PublishAsync(
                revision.Id,
                "integration-test",
                "publish before restart",
                CancellationToken.None))!;
            revisionId = revision.Id;
            releaseId = release.Id;
        }

        await using GameDbContext runtimeContext = postgres.CreateDbContext();
        ContentRevisionStore runtimeStore = new(runtimeContext, timeProvider);
        MutableContentSnapshotProvider provider =
            new(CreatePackage("content-file", "balance-file", Start));
        ContentPublicationService service = new(
            runtimeStore,
            new ContentRevisionImporter(runtimeStore),
            provider,
            new ContentPublicationCoordinator());

        ContentPublicationResult restored = (await service.RestoreLatestReleaseAsync(
            CancellationToken.None))!;

        Assert.Equal("content-9", provider.GetCurrent().ContentVersion);
        Assert.Equal("balance-4", provider.GetCurrent().BalanceVersion);
        Assert.Equal(revisionId, restored.RuntimeState.RevisionId);
        Assert.Equal(releaseId, restored.RuntimeState.ReleaseId);
        Assert.Equal(1, provider.CachedRevisionCount);
    }

    private static async Task<ContentRevision> CreateRevisionAsync(
        ContentRevisionStore store,
        GameContentPackage package,
        string note)
    {
        string payload = GameContentPackageCodec.SerializeCanonical(package);
        return await store.CreateRevisionAsync(
            package,
            payload,
            "integration-test",
            note,
            CancellationToken.None);
    }

    private static GameContentPackage CreatePackage(
        string contentVersion,
        string balanceVersion,
        DateTimeOffset publishedAtUtc) =>
        new(
            contentVersion,
            balanceVersion,
            publishedAtUtc,
            [],
            []);

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan delta) => current += delta;
    }
}
