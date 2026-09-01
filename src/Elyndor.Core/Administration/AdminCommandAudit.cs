namespace Elyndor.Core.Administration;

public sealed class AdminCommandAudit
{
    private AdminCommandAudit()
    {
        CommandName = null!;
        ResultCode = null!;
        ResultSummary = null!;
    }

    public AdminCommandAudit(
        long updateId,
        long administratorTelegramUserId,
        string commandName,
        long? targetTelegramUserId,
        DateTimeOffset receivedAtUtc)
    {
        if (updateId < 0 || administratorTelegramUserId <= 0 || receivedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(updateId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        UpdateId = updateId;
        AdministratorTelegramUserId = administratorTelegramUserId;
        CommandName = commandName;
        TargetTelegramUserId = targetTelegramUserId;
        ReceivedAtUtc = receivedAtUtc;
        ResultCode = "admin_pending";
        ResultSummary = "Команда принята.";
    }

    public long UpdateId { get; private set; }
    public long AdministratorTelegramUserId { get; private set; }
    public string CommandName { get; private set; }
    public long? TargetTelegramUserId { get; private set; }
    public string ResultCode { get; private set; }
    public string ResultSummary { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void Complete(string code, string summary, DateTimeOffset atUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (atUtc.Offset != TimeSpan.Zero || atUtc < ReceivedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(atUtc));
        }

        ResultCode = code;
        ResultSummary = summary;
        CompletedAtUtc = atUtc;
    }
}
