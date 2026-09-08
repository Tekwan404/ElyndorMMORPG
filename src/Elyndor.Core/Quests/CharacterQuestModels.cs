namespace Elyndor.Core.Quests;

public static class QuestStateStatuses
{
    public const string Active = "ACTIVE";
    public const string ReadyToClaim = "READY_TO_CLAIM";
    public const string Completed = "COMPLETED";
}

public sealed class CharacterQuestState
{
    private CharacterQuestState()
    {
        QuestId = null!;
        Status = null!;
        ProgressJson = null!;
    }

    public CharacterQuestState(
        Guid characterId,
        string questId,
        DateTimeOffset acceptedAtUtc)
    {
        if (characterId == Guid.Empty)
            throw new ArgumentException("Character id cannot be empty.", nameof(characterId));
        ArgumentException.ThrowIfNullOrWhiteSpace(questId);
        if (acceptedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Quest timestamps must be UTC.", nameof(acceptedAtUtc));

        CharacterId = characterId;
        QuestId = questId;
        Status = QuestStateStatuses.Active;
        AcceptedAtUtc = acceptedAtUtc;
        ProgressJson = "{}";
        StateVersion = 1;
    }

    public Guid CharacterId { get; private set; }
    public string QuestId { get; private set; }
    public string Status { get; private set; }
    public DateTimeOffset AcceptedAtUtc { get; private set; }
    public DateTimeOffset? ReadyAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string ProgressJson { get; private set; }
    public long StateVersion { get; private set; }

    public void UpdateProgress(
        string progressJson,
        bool readyToClaim,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(progressJson);
        if (now.Offset != TimeSpan.Zero)
            throw new ArgumentException("Quest timestamps must be UTC.", nameof(now));
        if (Status == QuestStateStatuses.Completed)
            return;

        ProgressJson = progressJson;
        if (readyToClaim && Status != QuestStateStatuses.ReadyToClaim)
        {
            Status = QuestStateStatuses.ReadyToClaim;
            ReadyAtUtc = now;
        }
        else if (!readyToClaim && Status == QuestStateStatuses.ReadyToClaim)
        {
            Status = QuestStateStatuses.Active;
            ReadyAtUtc = null;
        }

        StateVersion = checked(StateVersion + 1);
    }

    public void MarkCompleted(DateTimeOffset completedAtUtc)
    {
        if (completedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Quest timestamps must be UTC.", nameof(completedAtUtc));

        Status = QuestStateStatuses.Completed;
        CompletedAtUtc = completedAtUtc;
        ReadyAtUtc ??= completedAtUtc;
        StateVersion = checked(StateVersion + 1);
    }
}

public sealed class QuestRewardGrant
{
    private QuestRewardGrant()
    {
        QuestId = null!;
        RewardItemsJson = null!;
    }

    public QuestRewardGrant(
        Guid characterId,
        string questId,
        Guid claimMutationId,
        int xpEarned,
        int goldEarned,
        string rewardItemsJson,
        DateTimeOffset grantedAtUtc)
    {
        if (characterId == Guid.Empty || claimMutationId == Guid.Empty)
            throw new ArgumentException("Quest reward identifiers cannot be empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(questId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rewardItemsJson);
        ArgumentOutOfRangeException.ThrowIfNegative(xpEarned);
        ArgumentOutOfRangeException.ThrowIfNegative(goldEarned);
        if (grantedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Quest timestamps must be UTC.", nameof(grantedAtUtc));

        CharacterId = characterId;
        QuestId = questId;
        ClaimMutationId = claimMutationId;
        XpEarned = xpEarned;
        GoldEarned = goldEarned;
        RewardItemsJson = rewardItemsJson;
        GrantedAtUtc = grantedAtUtc;
    }

    public Guid CharacterId { get; private set; }
    public string QuestId { get; private set; }
    public Guid ClaimMutationId { get; private set; }
    public int XpEarned { get; private set; }
    public int GoldEarned { get; private set; }
    public string RewardItemsJson { get; private set; }
    public DateTimeOffset GrantedAtUtc { get; private set; }
}
