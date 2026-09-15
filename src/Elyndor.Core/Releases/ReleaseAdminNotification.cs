namespace Elyndor.Core.Releases;

public sealed class ReleaseAdminNotification
{
    private ReleaseAdminNotification()
    {
        ReleaseId = null!;
    }

    public ReleaseAdminNotification(string releaseId, long telegramUserId, DateTimeOffset sentAtUtc)
    {
        if (string.IsNullOrWhiteSpace(releaseId))
            throw new ArgumentException("Release ID is required.", nameof(releaseId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(telegramUserId);
        if (sentAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Notification timestamps must be UTC.", nameof(sentAtUtc));

        ReleaseId = releaseId.Trim();
        TelegramUserId = telegramUserId;
        SentAtUtc = sentAtUtc;
    }

    public string ReleaseId { get; private set; }

    public long TelegramUserId { get; private set; }

    public DateTimeOffset SentAtUtc { get; private set; }
}
