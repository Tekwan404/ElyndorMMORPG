namespace Elyndor.Core.Afk;

public sealed class AfkFarmIntervalGrant
{
    private AfkFarmIntervalGrant()
    {
        LootJson = "[]";
    }

    public AfkFarmIntervalGrant(
        Guid sessionId,
        int intervalIndex,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc,
        int kills,
        int xpEarned,
        int goldEarned,
        string lootJson,
        DateTimeOffset grantedAtUtc)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("AFK session identifier cannot be empty.", nameof(sessionId));
        ArgumentOutOfRangeException.ThrowIfNegative(intervalIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(kills);
        ArgumentOutOfRangeException.ThrowIfNegative(xpEarned);
        ArgumentOutOfRangeException.ThrowIfNegative(goldEarned);
        ArgumentException.ThrowIfNullOrWhiteSpace(lootJson);
        EnsureUtc(startedAtUtc, nameof(startedAtUtc));
        EnsureUtc(endedAtUtc, nameof(endedAtUtc));
        EnsureUtc(grantedAtUtc, nameof(grantedAtUtc));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(endedAtUtc, startedAtUtc);
        ArgumentOutOfRangeException.ThrowIfLessThan(grantedAtUtc, endedAtUtc);

        SessionId = sessionId;
        IntervalIndex = intervalIndex;
        StartedAtUtc = startedAtUtc;
        EndedAtUtc = endedAtUtc;
        Kills = kills;
        XpEarned = xpEarned;
        GoldEarned = goldEarned;
        LootJson = lootJson;
        GrantedAtUtc = grantedAtUtc;
    }

    public Guid SessionId { get; private set; }
    public int IntervalIndex { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset EndedAtUtc { get; private set; }
    public int Kills { get; private set; }
    public int XpEarned { get; private set; }
    public int GoldEarned { get; private set; }
    public string LootJson { get; private set; }
    public DateTimeOffset GrantedAtUtc { get; private set; }

    private static void EnsureUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
            throw new ArgumentException("AFK timestamps must be UTC.", parameterName);
    }
}
