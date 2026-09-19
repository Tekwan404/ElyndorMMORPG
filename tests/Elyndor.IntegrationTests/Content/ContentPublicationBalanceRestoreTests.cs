using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Content;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ContentPublicationBalanceRestoreTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Start =
        new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RestoreLatestReleaseKeepsBundledPackageWhenOnlyBalanceVersionIsNewer()
    {
        GameContentPackage bundled = await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json"));
        Assert.Equal("0.24.1", bundled.ContentVersion);
        Assert.Equal("0.19.0", bundled.BalanceVersion);

        var currentPotion = bundled.Items!
            .Single(item => item.Id == "SMALL_HEALING_POTION");
        GameContentPackage stalePublished = bundled with
        {
            BalanceVersion = "0.17.0",
            PublishedAtUtc = Start,
            Items = bundled.Items!
                .Where(item => item.Id != currentPotion.Id)
                .Append(currentPotion with { Name = "УСТАРЕВШЕЕ НАЗВАНИЕ" })
                .ToArray()
        };

        MutableTimeProvider timeProvider = new(Start);
        await using (GameDbContext seedContext = postgres.CreateDbContext())
        {
            ContentRevisionStore seedStore = new(seedContext, timeProvider);
            string payload = GameContentPackageCodec.SerializeCanonical(stalePublished);
            ContentRevision revision = await seedStore.CreateRevisionAsync(
                stalePublished,
                payload,
                "integration-test",
                "same content version, stale balance",
                CancellationToken.None);
            _ = await seedStore.PublishAsync(
                revision.Id,
                "integration-test",
                "publish stale balance",
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

        ContentPublicationResult restored = (await service.RestoreLatestReleaseAsync(
            CancellationToken.None))!;

        Assert.Equal(bundled.ContentVersion, provider.GetCurrent().ContentVersion);
        Assert.Equal(bundled.BalanceVersion, provider.GetCurrent().BalanceVersion);
        Assert.Equal(
            currentPotion.Name,
            provider.GetCurrent().Package.Items!
                .Single(item => item.Id == currentPotion.Id)
                .Name);
        Assert.NotNull(restored.RuntimeState.RevisionId);
        Assert.NotNull(restored.RuntimeState.ReleaseId);
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
