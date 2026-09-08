namespace Elyndor.Contracts.Quests;

public sealed record QuestObjectiveResponse(
    string Id,
    string Type,
    string TargetId,
    int CurrentCount,
    int RequiredCount,
    bool Completed,
    bool ConsumeOnClaim);

public sealed record QuestRewardItemResponse(
    string ItemId,
    int Quantity);

public sealed record QuestResponse(
    string Id,
    string DisplayName,
    string Description,
    string Type,
    int RequiredLevel,
    string OfferLocationId,
    string Status,
    IReadOnlyList<QuestObjectiveResponse> Objectives,
    int RewardXp,
    int RewardGold,
    IReadOnlyList<QuestRewardItemResponse> RewardItems,
    IReadOnlyList<string> PrerequisiteQuestIds,
    string? UnlockLocationId);

public sealed record QuestJournalResponse(
    IReadOnlyList<QuestResponse> Quests);

public sealed record QuestMutationRequest(string QuestId);

public sealed record QuestMutationResponse(
    string QuestId,
    string Status);

public sealed record QuestClaimRequest(
    string QuestId,
    Guid MutationId);

public sealed record QuestClaimResponse(
    string QuestId,
    bool Granted,
    int XpEarned,
    int GoldEarned,
    bool LeveledUp,
    int PreviousLevel,
    int CurrentLevel,
    IReadOnlyList<QuestRewardItemResponse> Items);
