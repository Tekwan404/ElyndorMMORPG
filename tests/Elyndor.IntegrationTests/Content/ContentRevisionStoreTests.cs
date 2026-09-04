using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Content;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ContentRevisionStoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 4, 18, 45, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreatingRevisionPersistsExactPayloadAndAudit()
    {
        MutableTimeProvider timeProvider = new(Start);
        await using GameDbContext context = postgres.CreateDbContext();
        ContentRevisionStore store = new(context, timeProvider);
        GameContentPackage package = CreatePackage("content-1", "balance-1", Start.AddMinutes(-5));
        const string payload = """{"contentVersion":"content-1","balanceVersion":"balance-1","locations":[]}""";

        ContentRevision revision = await store.CreateRevisionAsync(
            package,
            payload,
            "integration-test",
            "initial import",
            CancellationToken.None);

        await using GameDbContext verify = postgres.CreateDbContext();
        ContentRevision persisted = await verify.ContentRevisions.AsNoTracking().SingleAsync();
        ContentAuditEntry audit = await verify.ContentAuditEntries.AsNoTracking().SingleAsync();

        Assert.Equal(revision.Id, persisted.Id);
        Assert.Equal(payload, persisted.PayloadJson);
        Assert.Equal(64, persisted.PayloadSha256.Length);
        Assert.Equal(package.PublishedAtUtc, persisted.SourcePublishedAtUtc);
        Assert.Equal("integration-test", persisted.CreatedBy);
        Assert.Equal(ContentAuditActions.RevisionCreated, audit.Action);
        Assert.Equal(revision.Id, audit.RevisionId);
        Assert.Null(audit.ReleaseId);
    }

    [Fact]
    public async Task PublishingIsAppendOnlyAndSupportsRollbackByRepublishingOlderRevision()
    {
        MutableTimeProvider timeProvider = new(Start);
        await using GameDbContext context = postgres.CreateDbContext();
        ContentRevisionStore store = new(context, timeProvider);

        ContentRevision first = await store.CreateRevisionAsync(
            CreatePackage("content-1", "balance-1", Start),
            """{"version":"one"}""",
            "integration-test",
            null,
            CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        ContentRelease firstRelease = (await store.PublishAsync(
            first.Id,
            "integration-test",
            "publish one",
            CancellationToken.None))!;

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        ContentRevision second = await store.CreateRevisionAsync(
            CreatePackage("content-2", "balance-2", Start.AddMinutes(2)),
            """{"version":"two"}""",
            "integration-test",
            null,
            CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        ContentRelease secondRelease = (await store.PublishAsync(
            second.Id,
            "integration-test",
            "publish two",
            CancellationToken.None))!;

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        ContentRelease rollbackRelease = (await store.PublishAsync(
            first.Id,
            "integration-test",
            "rollback to one",
            CancellationToken.None))!;

        ContentRelease? latest = await store.GetLatestReleaseAsync(CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal(rollbackRelease.Id, latest.Id);
        Assert.Equal(first.Id, latest.RevisionId);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(2, await verify.ContentRevisions.CountAsync());
        Assert.Equal(3, await verify.ContentReleases.CountAsync());
        Assert.Equal(5, await verify.ContentAuditEntries.CountAsync());
        Assert.Equal(first.Id, firstRelease.RevisionId);
        Assert.Equal(second.Id, secondRelease.RevisionId);
    }

    [Fact]
    public async Task PublishingMissingRevisionDoesNotCreateHistory()
    {
        await using GameDbContext context = postgres.CreateDbContext();
        ContentRevisionStore store = new(context, new MutableTimeProvider(Start));

        ContentRelease? release = await store.PublishAsync(
            Guid.CreateVersion7(),
            "integration-test",
            null,
            CancellationToken.None);

        Assert.Null(release);

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Empty(await verify.ContentReleases.AsNoTracking().ToArrayAsync());
        Assert.Empty(await verify.ContentAuditEntries.AsNoTracking().ToArrayAsync());
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
