using Elyndor.Core.Content;

namespace Elyndor.Core.Talents;

/// <summary>
/// Content profile for talent trees loaded from JSON.
/// </summary>
public sealed record TalentTreeProfile(
    string TalentTreeId,
    string ClassId,
    int MaxSpendablePoints,
    string Version,
    IReadOnlyList<TalentBranchProfile> Branches);

/// <summary>
/// Content profile for a talent branch.
/// </summary>
public sealed record TalentBranchProfile(
    string BranchId,
    string Name,
    string Description,
    IReadOnlyList<TalentNodeProfile> Nodes);

/// <summary>
/// Content profile for a talent node.
/// </summary>
public sealed record TalentNodeProfile(
    string TalentId,
    string BranchId,
    int Tier,
    string Name,
    string Description,
    int MaxRank,
    string EffectType,
    IReadOnlyList<string>? Prerequisites,
    int RequiredSpentPointsInBranch,
    decimal? StatValue,
    string? StatType,
    string? AbilityId,
    int? CooldownSeconds,
    int? InternalCooldownSeconds,
    bool CanTriggerFromProc,
    string? TriggerEvent);

/// <summary>
/// Extension to GameContentPackage to include talent trees.
/// </summary>
public static class TalentContentExtensions
{
    public static IReadOnlyList<TalentTreeProfile>? GetTalentTrees(this GameContentPackage package)
    {
        var talentDefinitions = package.Definitions
            .Where(d => string.Equals(d.Type, "TalentTree", StringComparison.Ordinal))
            .ToList();

        if (talentDefinitions.Count == 0)
            return null;

        // In a real implementation, this would deserialize from the referenced content files
        // For now, we'll return null and load via separate mechanism
        return new List<TalentTreeProfile>();
    }
}

/// <summary>
/// Represents a player's investment in talents for a specific character.
/// </summary>
public sealed record CharacterTalents(
    Guid CharacterId,
    string TalentTreeId,
    IReadOnlyDictionary<string, int> AllocatedPoints, // TalentId -> rank
    int TotalSpentPoints,
    int TotalAvailablePoints,
    DateTimeOffset LastModifiedUtc);

/// <summary>
/// Result of attempting to allocate a talent point.
/// </summary>
public sealed record TalentAllocationResult(
    bool Success,
    string? ErrorCode,
    string? Message,
    CharacterTalents? UpdatedTalents = null);
