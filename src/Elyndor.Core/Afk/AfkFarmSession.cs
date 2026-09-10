namespace Elyndor.Core.Afk;

public enum AfkFarmMode
{
    Safe
}

public enum AfkFarmStatus
{
    Active,
    Completed,
    Cancelled,
    Dead,
    InventoryFull,
    Invalidated
}

public sealed class AfkFarmSession
{
    private AfkFarmSession()
    {
        LocationId = null!;
        ContentVersion = null!;
        BalanceVersion = null!;
        CharacterSnapshotJson = null!;
    }

    public AfkFarmSession(
        Guid id,
        Guid characterId,
        string locationId,
        AfkFarmMode mode,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endsAtUtc,
        string contentVersion,
        string balanceVersion,
        string characterSnapshotJson,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || characterId == Guid.Empty)
            throw new ArgumentException("AFK session identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(balanceVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(characterSnapshotJson);
        EnsureUtc(startedAtUtc, nameof(startedAtUtc));
        EnsureUtc(endsAtUtc, nameof(endsAtUtc));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        if (endsAtUtc <= startedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(endsAtUtc), "AFK duration must be positive.");
        if (createdAtUtc > startedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(createdAtUtc));

        Id = id;
        CharacterId = characterId;
        LocationId = locationId;
        Mode = mode;
        StartedAtUtc = startedAtUtc;
        EndsAtUtc = endsAtUtc;
        LastProcessedAtUtc = startedAtUtc;
        Status = AfkFarmStatus.Active;
        ContentVersion = contentVersion;
        BalanceVersion = balanceVersion;
        CharacterSnapshotJson = characterSnapshotJson;
        CreatedAtUtc = createdAtUtc;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public Guid CharacterId { get; private set; }
    public string LocationId { get; private set; }
    public AfkFarmMode Mode { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public DateTimeOffset LastProcessedAtUtc { get; private set; }
    public AfkFarmStatus Status { get; private set; }
    public string ContentVersion { get; private set; }
    public string BalanceVersion { get; private set; }
    public string CharacterSnapshotJson { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? StopReason { get; private set; }
    public long Version { get; private set; }

    public bool IsTerminal => Status != AfkFarmStatus.Active;

    public bool Cancel(DateTimeOffset completedAtUtc, string? reason = null)
    {
        EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        if (Status != AfkFarmStatus.Active)
            return false;
        if (completedAtUtc < StartedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(completedAtUtc));

        Status = AfkFarmStatus.Cancelled;
        CompletedAtUtc = completedAtUtc;
        StopReason = string.IsNullOrWhiteSpace(reason) ? "manual_cancel" : reason;
        Version++;
        return true;
    }

    private static void EnsureUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
            throw new ArgumentException("AFK timestamps must be UTC.", parameterName);
    }
}
