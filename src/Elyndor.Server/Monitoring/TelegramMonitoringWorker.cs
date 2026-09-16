using System.Globalization;
using Elyndor.Server.Administration;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Monitoring;

public sealed class TelegramMonitoringWorker(
    IOptions<TelegramMonitoringOptions> configuredOptions,
    IServerMetricsCollector metricsCollector,
    ITelegramMessageSender messageSender,
    IHostEnvironment environment,
    ILogger<TelegramMonitoringWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TelegramMonitoringOptions options = configuredOptions.Value;
        if (!options.Enabled || !options.IsConfigured)
            return;

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        using PeriodicTimer timer = new(TimeSpan.FromMinutes(options.ReportIntervalMinutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                ServerMetricsSnapshot metrics =
                    await metricsCollector.CollectAsync(stoppingToken);
                string report = BuildReport(metrics, options, environment.EnvironmentName);
                await messageSender.SendAsync(options.ChatId, report, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to send Telegram server monitoring report.");
            }
        }
    }

    internal static string BuildReport(
        ServerMetricsSnapshot metrics,
        TelegramMonitoringOptions options,
        string environment)
    {
        string cpu = metrics.CpuPercent.HasValue
            ? $"{metrics.CpuPercent.Value:F1}%"
            : "n/a";
        string processCpu = metrics.ProcessCpuPercent.HasValue
            ? $"{metrics.ProcessCpuPercent.Value:F1}%"
            : "n/a";
        string memory = metrics.MemoryTotalBytes > 0
            ? $"{metrics.MemoryUsedGiB:F2}/{metrics.MemoryTotalGiB:F2} GiB ({metrics.MemoryPercent:F1}%)"
            : "n/a";
        string disk = metrics.DiskTotalBytes > 0
            ? $"{metrics.DiskUsedGiB:F1}/{metrics.DiskTotalGiB:F1} GiB ({metrics.DiskPercent:F1}%)"
            : "n/a";

        string cpuState = State(metrics.CpuPercent, options.CpuWarningPercent, options.CpuCriticalPercent);
        string memoryState = State(metrics.MemoryPercent, options.MemoryWarningPercent, options.MemoryCriticalPercent);
        string diskState = State(metrics.DiskPercent, options.DiskWarningPercent, options.DiskCriticalPercent);
        string overall = cpuState == "CRITICAL" || memoryState == "CRITICAL" || diskState == "CRITICAL"
            ? "🔴 CRITICAL"
            : cpuState == "WARNING" || memoryState == "WARNING" || diskState == "WARNING"
                ? "🟡 WARNING"
                : "🟢 OK";

        return $"""
            🖥 Elyndor Logs
            {overall} · {environment}
            {metrics.Timestamp.ToLocalTime():dd.MM.yyyy HH:mm:ss}

            CPU: {cpu} · process {processCpu}
            RAM: {memory}
            Disk: {disk}
            Network: ↓ {FormatRate(metrics.NetworkReceivedBytesPerSecond)} / ↑ {FormatRate(metrics.NetworkSentBytesPerSecond)}
            Uptime: {FormatDuration(metrics.ProcessUptime)}
            Cores: {metrics.ProcessorCount.ToString(CultureInfo.InvariantCulture)}
            """.Trim();
    }

    private static string State(double? value, int warning, int critical) =>
        !value.HasValue ? "UNKNOWN" : value.Value >= critical ? "CRITICAL" : value.Value >= warning ? "WARNING" : "OK";

    private static string FormatRate(long bytesPerSecond)
    {
        string[] units = ["B/s", "KB/s", "MB/s", "GB/s"];
        double value = Math.Max(0, bytesPerSecond);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:F1} {units[unit]}";
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.TotalDays >= 1
            ? $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m"
            : duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
                : $"{duration.Minutes}m {duration.Seconds}s";
}
