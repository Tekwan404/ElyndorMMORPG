namespace Elyndor.Core.Identity;

public sealed class Account
{
    private Account()
    {
        TelegramUsername = null;
        NormalizedTelegramUsername = null;
    }

    public Account(Guid id, long telegramUserId, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Account ID cannot be empty.", nameof(id));
        }

        if (telegramUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(telegramUserId),
                "Telegram user ID must be positive.");
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Account timestamps must be UTC.", nameof(createdAtUtc));
        }

        Id = id;
        TelegramUserId = telegramUserId;
        CreatedAtUtc = createdAtUtc;
        LastSeenAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public long TelegramUserId { get; private set; }

    public string? TelegramUsername { get; private set; }

    public string? NormalizedTelegramUsername { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset LastSeenAtUtc { get; private set; }

    public void RecordSeen(DateTimeOffset seenAtUtc)
    {
        if (seenAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Account timestamps must be UTC.", nameof(seenAtUtc));
        }

        if (seenAtUtc > LastSeenAtUtc)
        {
            LastSeenAtUtc = seenAtUtc;
        }
    }

    public void SetTelegramUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            TelegramUsername = null;
            NormalizedTelegramUsername = null;
            return;
        }

        string normalized = TelegramUsernamePolicy.Normalize(username);
        TelegramUsername = username.Trim();
        NormalizedTelegramUsername = normalized;
    }
}
