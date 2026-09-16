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

    public bool UseLongPolling { get; init; }

    public long ChatId { get; init; }

    public long[] AllowedUserIds { get; init; } = [];

    public bool MonitoringEnabled { get; init; }

    public int ReportIntervalMinutes { get; init; } = 15;

    public int CpuWarningPercent { get; init; } = 80;

    public int CpuCriticalPercent { get; init; } = 95;

    public int MemoryWarningPercent { get; init; } = 80;

    public int MemoryCriticalPercent { get; init; } = 95;

    public int DiskWarningPercent { get; init; } = 80;

    public int DiskCriticalPercent { get; init; } = 95;

    public bool IsConfigured =>
        AllowedUserIds.All(id => id > 0)
        && (!Enabled
            || (AllowedUserIds.Length > 0
                && (UseLongPolling
                    || (WebhookSecret.Length >= 32
                        && (!RegisterWebhookOnStartup || TryGetWebhookUri(out _))))));

    public bool IsMonitoringConfigured =>
        ChatId != 0
        && ReportIntervalMinutes is >= 1 and <= 1440
        && CpuWarningPercent is >= 1 and <= 100
        && CpuCriticalPercent is >= 1 and <= 100
        && CpuWarningPercent <= CpuCriticalPercent
        && MemoryWarningPercent is >= 1 and <= 100
        && MemoryCriticalPercent is >= 1 and <= 100
        && MemoryWarningPercent <= MemoryCriticalPercent
        && DiskWarningPercent is >= 1 and <= 100
        && DiskCriticalPercent is >= 1 and <= 100
        && DiskWarningPercent <= DiskCriticalPercent;

    public bool IsAllowedUser(long telegramUserId) =>
        telegramUserId > 0
        && AllowedUserIds.Contains(telegramUserId);

    public bool IsAllowedChat(long telegramChatId) =>
        ChatId != 0 && telegramChatId == ChatId;

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
