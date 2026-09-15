using Elyndor.Core.Identity;
using Elyndor.Core.Releases;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Releases;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Releases;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ReleaseAcknowledgementServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AcknowledgeIsIdempotentAndMakesTheReleaseSeen()
    {
        Guid accountId = Guid.CreateVersion7();
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.Accounts.Add(new Account(accountId, 4_242, Now));
            await setup.SaveChangesAsync();
        }

        ReleaseNotesCatalog catalog = new(new ReleaseNotesDocument(
        [
            new ReleaseNoteDefinition(
                "0.23.1",
                "Игра обновлена",
                Now,
                true,
                [new ReleaseNoteEntry(ReleaseNoteEntryKind.Added, "Добавлены новости обновления.")])
        ]));

        await using GameDbContext context = postgres.CreateDbContext();
        ReleaseAcknowledgementService service = new(context, catalog, new FixedTimeProvider(Now));

        await service.AcknowledgeAsync(accountId, "0.23.1", CancellationToken.None);
        await service.AcknowledgeAsync(accountId, "0.23.1", CancellationToken.None);

        Assert.True(await service.HasAcknowledgedAsync(accountId, "0.23.1", CancellationToken.None));
        Assert.Equal(1, await context.AccountReleaseAcknowledgements.CountAsync());
    }
}

file sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
