using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Content;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ContentRevisionImporterTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 19, 5, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RealContentImportsAndRoundTripsWithStrictParity()
    {
        string packagePath = Path.GetFullPath("content/package.json");

        await using GameDbContext context = postgres.CreateDbContext();
        ContentRevisionStore store = new(context, new FixedTimeProvider(Now));
        ContentRevisionImporter importer = new(store);

        GameContentPackage source =
            await GameContentPackageLoader.LoadAsync(packagePath);

        ContentRevisionImportResult imported = await importer.ImportAsync(
            packagePath,
            "integration-test",
            "import current file content",
            CancellationToken.None);

        GameContentPackage? restored = await importer.LoadRevisionPackageAsync(
            imported.Revision.Id,
            CancellationToken.None);

        Assert.NotNull(restored);
        Assert.True(imported.Parity.IsMatch);
        Assert.Equal(imported.Parity.SourceSha256, imported.Parity.RoundTripSha256);
        Assert.Equal(imported.Parity.SourceSha256, imported.Revision.PayloadSha256);
        Assert.Equal(
            GameContentPackageCodec.SerializeCanonical(source),
            GameContentPackageCodec.SerializeCanonical(restored));

        GameContentIndexes sourceIndexes = GameContentIndexes.For(source);
        GameContentIndexes restoredIndexes = GameContentIndexes.For(restored);

        Assert.Equal(
            sourceIndexes.DefinitionsByKey.Keys.OrderBy(key => key.Type).ThenBy(key => key.Id),
            restoredIndexes.DefinitionsByKey.Keys.OrderBy(key => key.Type).ThenBy(key => key.Id));
        Assert.Equal(
            sourceIndexes.LocationsById.Keys.Order(StringComparer.Ordinal),
            restoredIndexes.LocationsById.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            sourceIndexes.AbilitiesById.Keys.Order(StringComparer.Ordinal),
            restoredIndexes.AbilitiesById.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            sourceIndexes.ItemsById.Keys.Order(StringComparer.Ordinal),
            restoredIndexes.ItemsById.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            sourceIndexes.MonstersById.Keys.Order(StringComparer.Ordinal),
            restoredIndexes.MonstersById.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(
            sourceIndexes.TalentTreesById.Keys.Order(StringComparer.Ordinal),
            restoredIndexes.TalentTreesById.Keys.Order(StringComparer.Ordinal));

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Single(await verify.ContentRevisions.AsNoTracking().ToArrayAsync());
        Assert.Single(await verify.ContentAuditEntries.AsNoTracking().ToArrayAsync());
        Assert.Empty(await verify.ContentReleases.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task TamperedRevisionPayloadIsRejectedBeforeDeserialization()
    {
        string packagePath = Path.GetFullPath("content/package.json");

        Guid revisionId;
        await using (GameDbContext context = postgres.CreateDbContext())
        {
            ContentRevisionImporter importer = new(
                new ContentRevisionStore(context, new FixedTimeProvider(Now)));
            ContentRevisionImportResult imported = await importer.ImportAsync(
                packagePath,
                "integration-test",
                null,
                CancellationToken.None);
            revisionId = imported.Revision.Id;
        }

        await using (GameDbContext tamper = postgres.CreateDbContext())
        {
            await tamper.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE game.content_revisions SET "PayloadJson" = '{{"tampered":true}}' WHERE "Id" = {revisionId}""");
        }

        await using GameDbContext verifyContext = postgres.CreateDbContext();
        ContentRevisionImporter verifier = new(
            new ContentRevisionStore(verifyContext, new FixedTimeProvider(Now)));

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => verifier.LoadRevisionPackageAsync(revisionId, CancellationToken.None));

        Assert.Contains("SHA-256 integrity", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
