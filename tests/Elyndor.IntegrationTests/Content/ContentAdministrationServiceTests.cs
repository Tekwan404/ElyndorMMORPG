using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Content;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ContentAdministrationServiceTests(PostgresFixture postgres)
    : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 20, 10, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DraftPublishHistoryAndRollbackAreAppendOnlyAndGuarded()
    {
        GameContentPackage initial =
            await GameContentPackageLoader.LoadAsync(
                Path.GetFullPath("content/package.json"));
        MutableContentSnapshotProvider provider = new(initial);

        await using GameDbContext context = postgres.CreateDbContext();
        ContentRevisionStore store = new(context, new FixedTimeProvider(Now));
        ContentPublicationService publication = new(
            store,
            new ContentRevisionImporter(store),
            provider,
            new ContentPublicationCoordinator());
        ContentAdministrationService service =
            new(store, publication, provider);

        ContentAdminRuntimeState original = service.GetCurrent();

        ContentRevision originalRevision = await service.CreateDraftAsync(
            original.PayloadJson,
            original.PayloadSha256,
            "integration-test",
            "capture original",
            CancellationToken.None);
        ContentPublicationResult originalRelease = (await service.PublishAsync(
            originalRevision.Id,
            original.PayloadSha256,
            "integration-test",
            "publish original",
            CancellationToken.None))!;

        ContentAdminRuntimeState liveOriginal = service.GetCurrent();
        GameContentPackage changed = liveOriginal.Package with
        {
            BalanceVersion = "0.9.99"
        };
        string changedPayload =
            GameContentPackageCodec.SerializeCanonical(changed);

        ContentRevision changedRevision = await service.CreateDraftAsync(
            changedPayload,
            liveOriginal.PayloadSha256,
            "integration-test",
            "raise test balance version",
            CancellationToken.None);
        ContentPublicationResult changedRelease = (await service.PublishAsync(
            changedRevision.Id,
            liveOriginal.PayloadSha256,
            "integration-test",
            "publish draft",
            CancellationToken.None))!;

        Assert.Equal("0.9.99", provider.GetCurrent().BalanceVersion);

        await Assert.ThrowsAsync<ContentPublicationConflictException>(
            () => service.PublishAsync(
                changedRevision.Id,
                liveOriginal.PayloadSha256,
                "integration-test",
                "stale publish",
                CancellationToken.None));

        ContentAdminRuntimeState changedLive = service.GetCurrent();
        ContentPublicationResult rollback = (await service.RollbackAsync(
            originalRelease.Release.Id,
            changedLive.PayloadSha256,
            "integration-test",
            "restore original",
            CancellationToken.None))!;

        Assert.Equal(originalRevision.Id, rollback.Release.RevisionId);
        Assert.Equal(
            initial.BalanceVersion,
            provider.GetCurrent().BalanceVersion);

        ContentAdminHistory history =
            await service.GetHistoryAsync(20, CancellationToken.None);
        Assert.Equal(2, history.Revisions.Count);
        Assert.Equal(3, history.Releases.Count);
        Assert.Contains(
            history.Releases,
            item => item.Id == changedRelease.Release.Id);
    }

    [Fact]
    public async Task StaleDraftBaseIsRejectedBeforePersistence()
    {
        GameContentPackage initial =
            await GameContentPackageLoader.LoadAsync(
                Path.GetFullPath("content/package.json"));
        MutableContentSnapshotProvider provider = new(initial);

        await using GameDbContext context = postgres.CreateDbContext();
        ContentRevisionStore store = new(context, new FixedTimeProvider(Now));
        ContentAdministrationService service = new(
            store,
            new ContentPublicationService(
                store,
                new ContentRevisionImporter(store),
                provider,
                new ContentPublicationCoordinator()),
            provider);

        ContentAdminRuntimeState current = service.GetCurrent();
        string payload = GameContentPackageCodec.SerializeCanonical(
            current.Package with { BalanceVersion = "0.9.98" });

        await Assert.ThrowsAsync<ContentDraftConflictException>(
            () => service.CreateDraftAsync(
                payload,
                new string('A', 64),
                "integration-test",
                null,
                CancellationToken.None));
    }

    [Fact]
    public void ValidationReturnsStructuredErrorInsteadOfThrowing()
    {
        MutableContentSnapshotProvider provider =
            new(new GameContentPackage("1.0.0", "1.0.0", Now, [], []));
        using GameDbContext context = postgres.CreateDbContext();
        ContentRevisionStore store =
            new(context, new FixedTimeProvider(Now));
        ContentAdministrationService service = new(
            store,
            new ContentPublicationService(
                store,
                new ContentRevisionImporter(store),
                provider,
                new ContentPublicationCoordinator()),
            provider);

        ContentDraftValidationResult result =
            ContentAdministrationService.ValidateDraft("{not-json");

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Code == "CONTENT_JSON_INVALID");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
