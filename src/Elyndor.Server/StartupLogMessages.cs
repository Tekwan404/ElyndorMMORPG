namespace Elyndor.Server;

internal static class StartupLogMessages
{
    private static readonly Action<ILogger, string, string, Exception?>
        PublishedContentFallback = LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1001, nameof(PublishedContentFallback)),
            "Latest published content revision is incompatible with the current runtime. "
            + "Starting with validated file content {ContentVersion}/{BalanceVersion}. "
            + "Publish a new valid content revision to repair persisted LIVE content.");

    private static readonly Action<ILogger, string, string, string, Exception?>
        UnhandledRequestException =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Error,
                new EventId(1002, nameof(UnhandledRequestException)),
                "Unhandled request failure {Method} {Path}; correlationId={CorrelationId}.");

    public static void LogPublishedContentFallback(
        ILogger logger,
        string contentVersion,
        string balanceVersion,
        Exception exception) =>
        PublishedContentFallback(
            logger,
            contentVersion,
            balanceVersion,
            exception);
    public static void LogUnhandledRequestException(
        ILogger logger,
        string method,
        string path,
        string correlationId,
        Exception exception) =>
        UnhandledRequestException(
            logger,
            method,
            path,
            correlationId,
            exception);

}
