namespace Elyndor.Core.Combat;

public sealed class ActiveCombatSession
{
    private ActiveCombatSession()
    {
        ContentVersion = null!;
        BalanceVersion = null!;
    }

    public ActiveCombatSession(
        Guid sessionId,
        Guid characterId,
        DateTimeOffset startedAtUtc,
        string contentVersion,
        string balanceVersion)
    {
        if (sessionId == Guid.Empty || characterId == Guid.Empty)
            throw new ArgumentException("Combat persistence identifiers cannot be empty.");
        if (startedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Combat timestamps must be UTC.", nameof(startedAtUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(balanceVersion);

        SessionId = sessionId;
        CharacterId = characterId;
        StartedAtUtc = startedAtUtc;
        ContentVersion = contentVersion;
        BalanceVersion = balanceVersion;
    }

    public Guid SessionId { get; private set; }
    public Guid CharacterId { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public string ContentVersion { get; private set; }
    public string BalanceVersion { get; private set; }
}
