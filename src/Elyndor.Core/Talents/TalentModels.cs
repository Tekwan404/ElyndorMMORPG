namespace Elyndor.Core.Talents;

public static class TalentLoadoutIds
{
    public const string Loadout1 = "LOADOUT_1";
    public const string Loadout2 = "LOADOUT_2";

    public static bool IsValid(string value) => value is Loadout1 or Loadout2;
}

public enum TalentModifierType
{
    StatModifier,
    AbilityModifier,
    EffectModifier,
    ResourceModifier,
    EventTriggered,
    EquipmentConditional
}

public enum TalentModifierRuntimeStatus
{
    Supported,
    Deferred
}

public sealed record TalentModifierDefinition(
    TalentModifierType Type,
    string Key,
    IReadOnlyList<decimal> Values,
    string? TargetId = null,
    TalentModifierRuntimeStatus RuntimeStatus = TalentModifierRuntimeStatus.Supported,
    string? DeferredOwner = null,
    decimal InternalCooldownSeconds = 0,
    bool CanTriggerFromProc = false);

public sealed record TalentPrerequisite(string TalentId, int RequiredRank = 1);

public sealed record TalentDefinition(
    string Id,
    string BranchId,
    int Tier,
    int RequiredSpentPoints,
    string Name,
    string EnglishName,
    int MaxRank,
    IReadOnlyList<TalentPrerequisite> Prerequisites,
    string Description,
    int? RequiredLevel = null,
    IReadOnlyList<TalentModifierDefinition>? Modifiers = null,
    int Version = 1,
    string? IconId = null);

public sealed record TalentBranchDefinition(
    string Id,
    string Name,
    string Fantasy,
    int NodeCount);

public sealed record TalentTreeDefinition(
    string Id,
    string ClassId,
    int MaxSpendablePoints,
    int Version,
    IReadOnlyList<TalentBranchDefinition> Branches,
    IReadOnlyList<TalentDefinition> Nodes);

public static class TalentErrorCodes
{
    public const string Unavailable = "talent_unavailable";
    public const string UnknownTalent = "talent_unknown";
    public const string InsufficientPoints = "talent_insufficient_points";
    public const string MaxRank = "talent_max_rank";
    public const string TierLocked = "talent_tier_locked";
    public const string PrerequisiteMissing = "talent_prerequisite_missing";
    public const string LevelRequired = "talent_level_required";
    public const string InvalidRank = "talent_invalid_rank";
    public const string InvalidLoadout = "talent_invalid_loadout";
    public const string Conflict = "talent_state_conflict";
    public const string InvalidMutationId = "talent_invalid_mutation_id";
}

public sealed record TalentLearnResult(
    bool IsSuccess,
    string? ErrorCode,
    IReadOnlyDictionary<string, int> SelectedRanks,
    int AvailablePoints)
{
    public static TalentLearnResult Failure(
        string errorCode,
        IReadOnlyDictionary<string, int> ranks,
        int availablePoints) => new(false, errorCode, ranks, availablePoints);
}
