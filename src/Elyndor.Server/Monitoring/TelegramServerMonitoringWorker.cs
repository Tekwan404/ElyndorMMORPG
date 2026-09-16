using System.Globalization;
using Elyndor.Server.Administration;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Monitoring;

public sealed class TelegramServerMonitoringWorker(
    IOptions<TelegramAdminOptions> options,
    IServerMetricsCollector metricsCollector,
    ServerErrorMetrics errors,
    ITelegramMessageSender messageSender,
    TimeProvider timeProvider,
    IHostEnvironment environment,
    ILogger<TelegramServerMonitoringWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TelegramAdminOptions configured = options.Value;
        if (!configured.Enabled || !configured.MonitoringEnabled || !configured.IsMonitoringConfigured)
            return;

        using PeriodicTimer timer = new(
            TimeSpan.FromMinutes(configured.ReportIntervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SendReportAsync(configured, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Telegram server monitoring report failed.");
            }
        }
    }

    private async Task SendReportAsync(
        TelegramAdminOptions options,
        CancellationToken cancellationToken)
    {
        ServerMetricsSnapshot metrics = await metricsCollector.CollectAsync(cancellationToken);
        int errors15m = errors.GetCount(TimeSpan.FromMinutes(15));
        int errors1h = errors.GetCount(TimeSpan.FromHours(1));
        string state = GetState(metrics, options);
        string environmentName = environment.EnvironmentName;
        string now = timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);

        string cpu = FormatPercent(metrics.CpuPercent);
        string processCpu = FormatPercent(metrics.ProcessCpuPercent);
        string memory = $"{metrics.MemoryUsedGiB:F1}/{metrics.MemoryTotalGiB:F1} GiB ({metrics.MemoryPercent:F0}%)";
        string disk = $"{metrics.DiskUsedGiB:F1}/{metrics.DiskTotalGiB:F1} GiB ({metrics.DiskPercent:F0}%)";
        string network = $"↓ {FormatBytes(metrics.NetworkReceivedBytesPerSecond)}/s  ↑ {FormatBytes(metrics.NetworkSentBytesPerSecond)}/s";

        string text =
            $"📊 Elyndor Logs — {state}\n"
            + $"🕒 {now}\n"
            + $"🌐 Environment: {environmentName}\n"
            + $"⏱ Uptime: {FormatDuration(metrics.ProcessUptime)}\n\n"
            + $"🖥 CPU: {cpu}  | process: {processCpu}\n"
            + $"🧠 RAM: {memory}\n"
            + $"💾 Disk: {disk}\n"
            + $"🌐 Network: {network}\n"
            + $"⚙ Cores: {metrics.ProcessorCount}\n\n"
            + $"🚨 Errors: {errors15m} / 15m  | {errors1h} / 1h\n"
            + $"🔗 API: /api/v1/status\n\n"
            + "Команды: /status /health /resources /errors /help";

        await messageSender.SendAsync(options.ChatId, text, cancellationToken);
    }

    private static string GetState(ServerMetricsSnapshot metrics, TelegramAdminOptions options)
    {
        if (metrics.CpuPercent >= options.CpuCriticalPercent
            || metrics.MemoryPercent >= options.MemoryCriticalPercent
            || metrics.DiskPercent >= options.DiskCriticalPercent)
            return "🔴 CRITICAL";

        if (metrics.CpuPercent >= options.CpuWarningPercent
            || metrics.MemoryPercent >= options.MemoryWarningPercent
            || metrics.DiskPercent >= options.DiskWarningPercent)
            return "🟡 WARNING";

        return "🟢 OK";
    }

    private static string FormatPercent(double? value) =>
        value.HasValue ? $"{value.Value:F0}%" : "n/a";

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024d:F1} KiB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / 1024d / 1024d:F1} MiB";
        return $"{bytes / 1024d / 1024d / 1024d:F1} GiB";
    }

    private static string FormatDuration(TimeSpan value) =>
        value.TotalDays >= 1
            ? $"{(int)value.TotalDays}d {value.Hours}h {value.Minutes}m"
            : $"{(int)value.TotalHours}h {value.Minutes}m {value.Seconds}s";
}
