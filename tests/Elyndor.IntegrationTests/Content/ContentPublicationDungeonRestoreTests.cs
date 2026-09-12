using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Content;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ContentPublicationDungeonRestoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 11, 6, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RestoreWithOlderPublishedContentKeepsBundledEclipsedCitadel()
    {
        GameContentPackage bundled = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        Assert.Equal("0.19.1", bundled.ContentVersion);
        Assert.Contains(
            bundled.Dungeons ?? [],
            dungeon => dungeon.Id == "ECLIPSED_CITADEL");

        GameContentPackage stalePublished = bundled with
        {
            ContentVersion = "0.18.0",
            PublishedAtUtc = Now.AddDays(-1),
            Dungeons = (bundled.Dungeons ?? [])
                .Where(dungeon => dungeon.Id != "ECLIPSED_CITADEL")
                .ToArray()
        };
        Assert.DoesNotContain(
            stalePublished.Dungeons ?? [],
            dungeon => dungeon.Id == "ECLIPSED_CITADEL");

        MutableTimeProvider timeProvider = new(Now);
        await using (GameDbContext seedContext = postgres.CreateDbContext())
        {
            ContentRevisionStore seedStore = new(seedContext, timeProvider);
            string payload = GameContentPackageCodec.SerializeCanonical(stalePublished);
            ContentRevision revision = await seedStore.CreateRevisionAsync(
                stalePublished,
                payload,
                "integration-test",
                "release created before Eclipsed Citadel content version advanced",
                CancellationToken.None);
            _ = await seedStore.PublishAsync(
                revision.Id,
                "integration-test",
                "publish stale content",
                CancellationToken.None);
        }

        await using GameDbContext runtimeContext = postgres.CreateDbContext();
        ContentRevisionStore runtimeStore = new(runtimeContext, timeProvider);
        MutableContentSnapshotProvider provider = new(bundled);
        ContentPublicationService service = new(
            runtimeStore,
            new ContentRevisionImporter(runtimeStore),
            provider,
            new ContentPublicationCoordinator());

        ContentPublicationResult? restored = await service.RestoreLatestReleaseAsync(
            CancellationToken.None);

        Assert.NotNull(restored);
        Assert.Equal(bundled.ContentVersion, provider.GetCurrent().ContentVersion);
        Assert.Contains(
            provider.GetCurrent().Package.Dungeons ?? [],
            dungeon => dungeon.Id == "ECLIPSED_CITADEL"
                && dungeon.EntryLocationId == "ECLIPSED_CITADEL"
                && dungeon.MinimumPartySize == 1);
        Assert.True(
            provider.GetCurrent().Indexes.DungeonsById.ContainsKey("ECLIPSED_CITADEL"));
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
