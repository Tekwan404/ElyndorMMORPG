namespace Elyndor.Core.Quests;

public enum QuestType
{
    Story,
    Side,
    Contract
}

public enum QuestObjectiveType
{
    KillMonster,
    CollectItem
}

public sealed record QuestObjectiveDefinition(
    string Id,
    QuestObjectiveType Type,
    string TargetId,
    int RequiredCount,
    bool ConsumeOnClaim = false);

public sealed record QuestItemRewardDefinition(
    string ItemId,
    int Quantity);

public sealed record QuestDefinition(
    string Id,
    string DisplayName,
    string Description,
    QuestType Type,
    int RequiredLevel,
    string OfferLocationId,
    IReadOnlyList<QuestObjectiveDefinition> Objectives,
    int RewardXp = 0,
    int RewardGold = 0,
    IReadOnlyList<QuestItemRewardDefinition>? RewardItems = null,
    IReadOnlyList<string>? PrerequisiteQuestIds = null,
    string? UnlockLocationId = null);
