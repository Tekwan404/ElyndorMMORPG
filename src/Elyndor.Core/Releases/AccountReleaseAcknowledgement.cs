namespace Elyndor.Core.Releases;

public sealed class AccountReleaseAcknowledgement
{
    private AccountReleaseAcknowledgement()
    {
        ReleaseId = null!;
    }

    public AccountReleaseAcknowledgement(
        Guid accountId,
        string releaseId,
        DateTimeOffset acknowledgedAtUtc)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account ID cannot be empty.", nameof(accountId));
        if (string.IsNullOrWhiteSpace(releaseId))
            throw new ArgumentException("Release ID is required.", nameof(releaseId));
        if (acknowledgedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Acknowledgement timestamps must be UTC.", nameof(acknowledgedAtUtc));

        AccountId = accountId;
        ReleaseId = releaseId.Trim();
        AcknowledgedAtUtc = acknowledgedAtUtc;
    }

    public Guid AccountId { get; private set; }

    public string ReleaseId { get; private set; }

    public DateTimeOffset AcknowledgedAtUtc { get; private set; }
}
