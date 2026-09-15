using Elyndor.Core.Releases;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Server.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Releases;

public sealed class ReleaseAdminNotificationService(
    GameDbContext dbContext,
    IReleaseNotesCatalog releaseNotesCatalog,
    ITelegramMessageSender messageSender,
    IOptions<TelegramAdminOptions> configuredOptions,
    TimeProvider timeProvider,
    ILogger<ReleaseAdminNotificationService> logger)
{
    private static readonly Action<ILogger, long, string, Exception?> NotificationSendFailed =
        LoggerMessage.Define<long, string>(
            LogLevel.Warning,
            new EventId(2401, nameof(NotificationSendFailed)),
            "Failed to send release {ReleaseId} notification to administrator {TelegramUserId}.");

    public async Task NotifyCurrentReleaseAsync(CancellationToken cancellationToken)
    {
        ReleaseNoteDefinition? release = releaseNotesCatalog.Current;
        TelegramAdminOptions options = configuredOptions.Value;
        if (release is null || !options.Enabled || options.AllowedUserIds.Length == 0)
            return;

        string message = ReleaseAdminNotificationMessageFormatter.Format(release);
        foreach (long telegramUserId in options.AllowedUserIds.Distinct().Where(id => id > 0))
        {
            bool alreadySent = await dbContext.ReleaseAdminNotifications
                .AsNoTracking()
                .AnyAsync(
                    notification => notification.ReleaseId == release.Id
                        && notification.TelegramUserId == telegramUserId,
                    cancellationToken);
            if (alreadySent)
                continue;

            try
            {
                await messageSender.SendAsync(telegramUserId, message, cancellationToken);
                dbContext.ReleaseAdminNotifications.Add(new ReleaseAdminNotification(
                    release.Id,
                    telegramUserId,
                    timeProvider.GetUtcNow()));
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                dbContext.ChangeTracker.Clear();
                NotificationSendFailed(logger, telegramUserId, release.Id, exception);
            }
        }
    }
}

public static class ReleaseAdminNotificationMessageFormatter
{
    public static string Format(ReleaseNoteDefinition release)
    {
        ArgumentNullException.ThrowIfNull(release);

        IEnumerable<IGrouping<ReleaseNoteEntryKind, ReleaseNoteEntry>> groups = release.Entries
            .GroupBy(entry => entry.Kind)
            .OrderBy(group => group.Key);
        List<string> lines =
        [
            "ELYNDOR · игра обновлена",
            $"Версия {release.Id} — {release.Title}"
        ];

        foreach (IGrouping<ReleaseNoteEntryKind, ReleaseNoteEntry> group in groups)
        {
            lines.Add(string.Empty);
            lines.Add($"{LabelFor(group.Key)}:");
            lines.AddRange(group.Select(entry => $"• {entry.Text}"));
        }

        return string.Join('\n', lines);
    }

    private static string LabelFor(ReleaseNoteEntryKind kind) => kind switch
    {
        ReleaseNoteEntryKind.Added => "Добавлено",
        ReleaseNoteEntryKind.Changed => "Изменено",
        ReleaseNoteEntryKind.Fixed => "Исправлено",
        _ => "Изменения"
    };
}
