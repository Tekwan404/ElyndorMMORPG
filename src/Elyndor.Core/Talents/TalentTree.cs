namespace Elyndor.Core.Talents;

/// <summary>
/// Represents a talent tree for a specific class.
/// Contains all nodes organized by branches and tiers.
/// </summary>
public sealed record TalentTree(
    string TalentTreeId,
    string ClassId,
    IReadOnlyList<TalentBranch> Branches,
    int MaxSpendablePoints,
    string Version);

/// <summary>
/// A thematic branch within a talent tree (e.g., Guardian, Berserker, Commander for Warrior).
/// </summary>
public sealed record TalentBranch(
    string BranchId,
    string Name,
    string Description,
    IReadOnlyList<TalentNode> Nodes);

/// <summary>
/// A single talent node that can be invested in.
/// </summary>
public sealed record TalentNode(
    string TalentId,
    string TalentTreeId,
    string BranchId,
    int Tier,
    string Name,
    string Description,
    int MaxRank,
    TalentEffectType EffectType,
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
/// The type of effect this talent provides.
/// </summary>
public enum TalentEffectType
{
    StatModifier,           // Modifies a stat (Strength, Armor, etc.)
    ResourceModifier,       // Modifies resource (max Rage, regen, etc.)
    AbilityModifier,        // Modifies an ability (cooldown, cost, threat)
    DamageModifier,         // Modifies damage dealt or taken
    EffectModifier,         // Modifies effect duration or potency
    EventTriggered,         // Triggers on specific events (dodge, hit, etc.)
    Conditional,            // Active only under certain conditions
    GrantAbility,           // Grants a new active ability
    LethalPrevention        // Prevents death (one-time per combat)
}
