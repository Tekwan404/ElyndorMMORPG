namespace Elyndor.Server.Monitoring;

public sealed class TelegramMonitoringOptions
{
    public const string SectionName = "Monitoring:Telegram";

    public bool Enabled { get; init; }

    public long ChatId { get; init; }

    public long[] AdminUserIds { get; init; } = [];

    public int ReportIntervalMinutes { get; init; } = 15;

    public int CpuWarningPercent { get; init; } = 80;

    public int CpuCriticalPercent { get; init; } = 95;

    public int MemoryWarningPercent { get; init; } = 80;

    public int MemoryCriticalPercent { get; init; } = 95;

    public int DiskWarningPercent { get; init; } = 80;

    public int DiskCriticalPercent { get; init; } = 90;

    public bool IsConfigured =>
        !Enabled
        || (ChatId != 0
            && AdminUserIds.Length > 0
            && AdminUserIds.All(id => id > 0)
            && ReportIntervalMinutes > 0
            && CpuWarningPercent < CpuCriticalPercent
            && MemoryWarningPercent < MemoryCriticalPercent
            && DiskWarningPercent < DiskCriticalPercent);

    public bool IsAllowed(long chatId, long userId) =>
        Enabled
        && chatId == ChatId
        && AdminUserIds.Contains(userId);
}
