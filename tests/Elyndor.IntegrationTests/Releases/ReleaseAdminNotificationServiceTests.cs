using Elyndor.Core.Releases;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Administration;
using Elyndor.Server.Releases;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Elyndor.IntegrationTests.Releases;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class ReleaseAdminNotificationServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SendsCurrentReleaseOnlyOnceToConfiguredAdministrators()
    {
        await using GameDbContext context = postgres.CreateDbContext();
        RecordingSender sender = new();
        ReleaseAdminNotificationService service = CreateService(context, sender, [42, 77, 42]);

        await service.NotifyCurrentReleaseAsync(CancellationToken.None);
        await service.NotifyCurrentReleaseAsync(CancellationToken.None);

        Assert.Equal([42L, 77L], sender.Messages.Select(message => message.ChatId).Order());
        Assert.All(sender.Messages, message => Assert.Contains("0.23.1", message.Text));
        Assert.Equal(2, await context.ReleaseAdminNotifications.CountAsync());
    }

    private static ReleaseAdminNotificationService CreateService(
        GameDbContext context,
        ITelegramMessageSender sender,
        long[] allowedUserIds) =>
        new(
            context,
            new ReleaseNotesCatalog(new ReleaseNotesDocument(
            [
                new ReleaseNoteDefinition(
                    "0.23.1",
                    "Игра обновлена",
                    Now,
                    true,
                    [new ReleaseNoteEntry(ReleaseNoteEntryKind.Fixed, "Исправлена стабильность боя.")])
            ])),
            sender,
            Options.Create(new TelegramAdminOptions
            {
                Enabled = true,
                AllowedUserIds = allowedUserIds
            }),
            new ReleaseNotificationTimeProvider(Now),
            NullLogger<ReleaseAdminNotificationService>.Instance);

    private sealed class RecordingSender : ITelegramMessageSender
    {
        public List<(long ChatId, string Text)> Messages { get; } = [];

        public Task SendAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            Messages.Add((chatId, text));
            return Task.CompletedTask;
        }
    }
}

file sealed class ReleaseNotificationTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
