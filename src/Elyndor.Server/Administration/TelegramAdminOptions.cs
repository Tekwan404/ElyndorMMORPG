namespace Elyndor.Server.Administration;

public sealed class TelegramAdminOptions
{
    public const string SectionName = "Administration:Telegram";
    public const string DefaultWebhookUrl =
        "https://game.elyndor.su/api/v1/administration/telegram/webhook";

    public bool Enabled { get; init; }

    public string WebhookSecret { get; init; } = string.Empty;

    public string WebhookUrl { get; init; } = DefaultWebhookUrl;

    public bool RegisterWebhookOnStartup { get; init; } = true;

    public long[] AllowedUserIds { get; init; } = [];

    public bool IsConfigured =>
        AllowedUserIds.All(id => id > 0)
        && (!Enabled
            || (WebhookSecret.Length >= 32
                && AllowedUserIds.Length > 0
                && (!RegisterWebhookOnStartup || TryGetWebhookUri(out _))));

    public bool IsAllowedUser(long telegramUserId) =>
        telegramUserId > 0
        && AllowedUserIds.Contains(telegramUserId);

    public bool TryGetWebhookUri(out Uri? webhookUri)
    {
        bool parsed = Uri.TryCreate(WebhookUrl, UriKind.Absolute, out webhookUri);
        return parsed
            && webhookUri is not null
            && string.Equals(
                webhookUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase);
    }
}
