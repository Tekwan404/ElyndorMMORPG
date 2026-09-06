namespace Elyndor.Core.Progression;

public sealed class CombatRewardGrant
{
    private CombatRewardGrant()
    {
        MonsterId = null!;
        RewardSourcesJson = "[]";
    }

    public CombatRewardGrant(
        Guid combatSessionId,
        Guid characterId,
        string monsterId,
        int xpEarned,
        int goldEarned,
        DateTimeOffset grantedAtUtc,
        string rewardSourcesJson = "[]")
    {
        if (combatSessionId == Guid.Empty || characterId == Guid.Empty)
            throw new ArgumentException("Reward identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(monsterId);
        ArgumentOutOfRangeException.ThrowIfNegative(xpEarned);
        ArgumentOutOfRangeException.ThrowIfNegative(goldEarned);
        ArgumentException.ThrowIfNullOrWhiteSpace(rewardSourcesJson);
        if (grantedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Reward timestamps must be UTC.", nameof(grantedAtUtc));

        CombatSessionId = combatSessionId;
        CharacterId = characterId;
        MonsterId = monsterId;
        XpEarned = xpEarned;
        GoldEarned = goldEarned;
        GrantedAtUtc = grantedAtUtc;
        RewardSourcesJson = rewardSourcesJson;
    }

    public Guid CombatSessionId { get; private set; }
    public Guid CharacterId { get; private set; }
    public string MonsterId { get; private set; }
    public int XpEarned { get; private set; }
    public int GoldEarned { get; private set; }
    public DateTimeOffset GrantedAtUtc { get; private set; }
    public string RewardSourcesJson { get; private set; }
}

public sealed record CombatRewardSourceAudit(
    Guid EnemyActorId,
    string MonsterId,
    int XpEarned,
    int GoldEarned,
    int EncounterOrder);
