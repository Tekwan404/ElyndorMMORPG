namespace Elyndor.Server.Administration;

public sealed class TelegramAdminOptions
{
    public const string SectionName = "Administration:Telegram";

    public bool Enabled { get; init; }

    public string WebhookSecret { get; init; } = string.Empty;

    public long[] AllowedUserIds { get; init; } = [];

    public bool IsConfigured =>
        AllowedUserIds.All(id => id > 0)
        && (!Enabled
            || (WebhookSecret.Length >= 32
                && AllowedUserIds.Length > 0));

    public bool IsAllowedUser(long telegramUserId) =>
        telegramUserId > 0
        && AllowedUserIds.Contains(telegramUserId);
}
