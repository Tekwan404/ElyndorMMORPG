using System.Diagnostics;
using System.Globalization;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Server.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Administration;

public sealed class TelegramAdminUpdateProcessor(
    IOptions<TelegramAdminOptions> configuredOptions,
    TelegramAdministrationService administrationService,
    ITelegramMessageSender messageSender,
    IServerMetricsCollector metricsCollector,
    ServerErrorMetrics errors,
    GameDbContext dbContext)
{
    internal const string HelpText = """
        Elyndor admin commands:
        /help
        /status
        /health
        /resources
        /errors
        /char <telegramId>
        /level <telegramId> <1-60>
        /restore <telegramId>
        /location <telegramId> <locationId>
        /rename <telegramId> <new name>
        /class <telegramId> WARRIOR|ARCHER|MAGE
        /race <telegramId> <raceId>
        /giveitem <telegramId> <itemId> [quantity] [NORMAL|ELITE|BOSS]
        /promocode create <CODE> crystals=<amount> [item=<ITEM_ID>:<qty>] [global=<N>] [per=<N>] [hours=<N>]
        /delete <telegramId> <exact name> CONFIRM
        /msg <telegramId> <text>

        Команды также принимаются без слэша.
        """;

    public async Task ProcessAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        TelegramAdminOptions options = configuredOptions.Value;
        if (!options.Enabled || !options.IsConfigured)
            return;

        TelegramMessage? message = update.Message;
        if (message?.From is null || !options.IsAllowedUser(message.From.Id) || !IsAuthorizedChat(message, options))
            return;

        AdminCommandParseResult parsed = TelegramAdminCommandParser.Parse(message.Text);
        if (!parsed.IsSuccess)
        {
            await messageSender.SendAsync(message.Chat.Id, $"Ошибка: {parsed.ErrorCode}\n\n{HelpText}", cancellationToken);
            return;
        }

        AdminCommand command = parsed.Command!;
        switch (command.Type)
        {
            case AdminCommandType.Help:
                await messageSender.SendAsync(message.Chat.Id, HelpText, cancellationToken);
                return;
            case AdminCommandType.Status:
                await SendStatusAsync(message.Chat.Id, cancellationToken);
                return;
            case AdminCommandType.Health:
                await SendHealthAsync(message.Chat.Id, cancellationToken);
                return;
            case AdminCommandType.Resources:
                await SendResourcesAsync(message.Chat.Id, cancellationToken);
                return;
            case AdminCommandType.Errors:
                await SendErrorsAsync(message.Chat.Id, cancellationToken);
                return;
        }

        AdministrationOperation operation = new(Map(command.Type), command.TargetTelegramUserId, command.Value, command.NumericValue);
        AdministrationResult result = await administrationService.ExecuteAsync(update.UpdateId, message.From.Id, operation, cancellationToken);
        string prefix = result.IsSuccess ? "✅" : "⚠️";
        await messageSender.SendAsync(message.Chat.Id, $"{prefix} {result.Message}\nКод: {result.Code}", cancellationToken);
    }

    private async Task SendStatusAsync(long chatId, CancellationToken cancellationToken)
    {
        ServerMetricsSnapshot metrics = await metricsCollector.CollectAsync(cancellationToken);
        int errors15m = errors.GetCount(TimeSpan.FromMinutes(15));
        string state = GetState(metrics);
        string text = $"📊 Elyndor Status — {state}\n"
            + $"Uptime: {FormatDuration(metrics.ProcessUptime)}\n"
            + $"👥 Players online: {metrics.OnlinePlayers}\n"
            + $"CPU: {FormatPercent(metrics.CpuPercent)}\n"
            + $"RAM: {metrics.MemoryUsedGiB:F1}/{metrics.MemoryTotalGiB:F1} GiB ({metrics.MemoryPercent:F0}%)\n"
            + $"Disk: {metrics.DiskUsedGiB:F1}/{metrics.DiskTotalGiB:F1} GiB ({metrics.DiskPercent:F0}%)\n"
            + $"Errors: {errors15m} / 15m\n"
            + $"Cores: {metrics.ProcessorCount}";
        await messageSender.SendAsync(chatId, text, cancellationToken);
    }

    private async Task SendHealthAsync(long chatId, CancellationToken cancellationToken)
    {
        ServerMetricsSnapshot metrics = await metricsCollector.CollectAsync(cancellationToken);
        Stopwatch stopwatch = Stopwatch.StartNew();
        bool databaseHealthy;
        string databaseDetail;
        try
        {
            databaseHealthy = await dbContext.Database.CanConnectAsync(cancellationToken);
            databaseDetail = databaseHealthy ? "connection ok" : "connection failed";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            databaseHealthy = false;
            databaseDetail = exception.GetType().Name;
        }
        stopwatch.Stop();

        string processState = "🟢 running";
        string databaseState = databaseHealthy ? "🟢 OK" : "🔴 DOWN";
        string resourceState = GetState(metrics);
        string overall = !databaseHealthy || resourceState == "🔴 CRITICAL"
            ? "🔴 CRITICAL"
            : resourceState == "🟡 WARNING"
                ? "🟡 WARNING"
                : "🟢 OK";

        string text = $"🩺 Elyndor Health — {overall}\n"
            + $"Process: {processState}\n"
            + $"PostgreSQL: {databaseState} ({stopwatch.ElapsedMilliseconds} ms)\n"
            + $"Detail: {databaseDetail}\n"
            + $"CPU: {FormatPercent(metrics.CpuPercent)}\n"
            + $"RAM: {metrics.MemoryPercent:F0}%\n"
            + $"Disk: {metrics.DiskPercent:F0}%\n"
            + $"Errors: {errors.GetCount(TimeSpan.FromMinutes(15))} / 15m";
        await messageSender.SendAsync(chatId, text, cancellationToken);
    }

    private async Task SendResourcesAsync(long chatId, CancellationToken cancellationToken)
    {
        ServerMetricsSnapshot metrics = await metricsCollector.CollectAsync(cancellationToken);
        string text = $"🖥 Elyndor Resources\n"
            + $"CPU: {FormatPercent(metrics.CpuPercent)} (process {FormatPercent(metrics.ProcessCpuPercent)})\n"
            + $"RAM: {metrics.MemoryUsedGiB:F1}/{metrics.MemoryTotalGiB:F1} GiB\n"
            + $"Disk: {metrics.DiskUsedGiB:F1}/{metrics.DiskTotalGiB:F1} GiB free {metrics.DiskFreeBytes / 1024d / 1024d / 1024d:F1} GiB\n"
            + $"Network: ↓ {FormatBytes(metrics.NetworkReceivedBytesPerSecond)}/s ↑ {FormatBytes(metrics.NetworkSentBytesPerSecond)}/s\n"
            + $"Cores: {metrics.ProcessorCount}";
        await messageSender.SendAsync(chatId, text, cancellationToken);
    }

    private async Task SendErrorsAsync(long chatId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ServerErrorMetrics.ErrorEvent> recent = errors.GetRecent(8);
        string text = $"🚨 Elyndor Errors\n15m: {errors.GetCount(TimeSpan.FromMinutes(15))}\n1h: {errors.GetCount(TimeSpan.FromHours(1))}\n24h: {errors.GetCount(TimeSpan.FromHours(24))}";
        if (recent.Count > 0)
        {
            text += "\n\nПоследние:";
            foreach (ServerErrorMetrics.ErrorEvent error in recent)
                text += $"\n• {error.Timestamp:HH:mm:ss} {error.Type} {error.Path}";
        }
        await messageSender.SendAsync(chatId, text, cancellationToken);
    }

    private string GetState(ServerMetricsSnapshot metrics)
    {
        TelegramAdminOptions options = configuredOptions.Value;
        if (metrics.CpuPercent >= options.CpuCriticalPercent || metrics.MemoryPercent >= options.MemoryCriticalPercent || metrics.DiskPercent >= options.DiskCriticalPercent)
            return "🔴 CRITICAL";
        if (metrics.CpuPercent >= options.CpuWarningPercent || metrics.MemoryPercent >= options.MemoryWarningPercent || metrics.DiskPercent >= options.DiskWarningPercent)
            return "🟡 WARNING";
        return "🟢 OK";
    }

    private static string FormatPercent(double? value) => value.HasValue ? $"{value.Value:F0}%" : "n/a";

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:F1} KiB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024d:F1} MiB";
        return $"{bytes / 1024d / 1024d / 1024d:F1} GiB";
    }

    private static string FormatDuration(TimeSpan value) => value.TotalDays >= 1
        ? $"{(int)value.TotalDays}d {value.Hours}h {value.Minutes}m"
        : $"{(int)value.TotalHours}h {value.Minutes}m {value.Seconds}s";

    private static bool IsAuthorizedChat(TelegramMessage message, TelegramAdminOptions options) =>
        options.IsAllowedChat(message.Chat.Id)
        || (string.Equals(message.Chat.Type, "private", StringComparison.OrdinalIgnoreCase)
            && message.Chat.Id == message.From!.Id);

    private static AdministrationOperationType Map(AdminCommandType type) => type switch
    {
        AdminCommandType.ShowCharacter => AdministrationOperationType.ShowCharacter,
        AdminCommandType.SetLevel => AdministrationOperationType.SetLevel,
        AdminCommandType.Restore => AdministrationOperationType.Restore,
        AdminCommandType.SetLocation => AdministrationOperationType.SetLocation,
        AdminCommandType.Rename => AdministrationOperationType.Rename,
        AdminCommandType.SetClass => AdministrationOperationType.SetClass,
        AdminCommandType.SetRace => AdministrationOperationType.SetRace,
        AdminCommandType.Delete => AdministrationOperationType.Delete,
        AdminCommandType.Message => AdministrationOperationType.Message,
        AdminCommandType.GiveItem => AdministrationOperationType.GiveItem,
        AdminCommandType.CreatePromoCode => AdministrationOperationType.CreatePromoCode,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
