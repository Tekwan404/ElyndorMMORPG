namespace Elyndor.Core.Economy;

public enum CrystalLedgerEntryType
{
    Payment,
    StorePurchase,
    PromoCode,
    AdminGrant,
    Refund
}

public sealed class CrystalWallet
{
    private CrystalWallet() { }

    public CrystalWallet(Guid accountId)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Account identifier cannot be empty.", nameof(accountId));
        AccountId = accountId;
    }

    public Guid AccountId { get; private set; }
    public long Balance { get; private set; }

    public void Credit(long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        Balance = checked(Balance + amount);
    }

    public bool TryDebit(long amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (Balance < amount) return false;
        Balance -= amount;
        return true;
    }
}

public sealed class CrystalLedgerEntry
{
    private CrystalLedgerEntry()
    {
        Reference = null!;
        RequestFingerprint = null!;
    }

    public CrystalLedgerEntry(
        Guid id,
        Guid accountId,
        Guid operationId,
        CrystalLedgerEntryType entryType,
        long delta,
        long balanceAfter,
        string reference,
        string requestFingerprint,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || accountId == Guid.Empty || operationId == Guid.Empty)
            throw new ArgumentException("Crystal ledger identifiers cannot be empty.");
        ArgumentOutOfRangeException.ThrowIfZero(delta);
        ArgumentOutOfRangeException.ThrowIfNegative(balanceAfter);
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (reference.Length > 128) throw new ArgumentException("Reference is too long.", nameof(reference));
        if (requestFingerprint.Length != 64) throw new ArgumentException("Invalid request fingerprint.", nameof(requestFingerprint));
        if (createdAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Ledger timestamps must be UTC.", nameof(createdAtUtc));

        Id = id;
        AccountId = accountId;
        OperationId = operationId;
        EntryType = entryType;
        Delta = delta;
        BalanceAfter = balanceAfter;
        Reference = reference;
        RequestFingerprint = requestFingerprint;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid OperationId { get; private set; }
    public CrystalLedgerEntryType EntryType { get; private set; }
    public long Delta { get; private set; }
    public long BalanceAfter { get; private set; }
    public string Reference { get; private set; }
    public string RequestFingerprint { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
