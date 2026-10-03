using Elyndor.Core.Combat.Abilities;

namespace Elyndor.Core.Combat;

// Callbacks observe execution at the existing boundaries. Each resolved mechanic
// retains its own proc admission; started callbacks consume primary ability buffs.
internal sealed record CombatAbilityReactionHandlers(
    Action<AbilityDefinition, DateTimeOffset> PyromancerStarted,
    Action<AbilityDefinition, DateTimeOffset> MageStarted,
    Action<AbilityDefinition, DateTimeOffset> ArcherStarted,
    Action<AbilityDefinition, AbilityExecutionResult, DateTimeOffset> WarriorResolved,
    Action<AbilityDefinition, AbilityExecutionResult, DateTimeOffset> PyromancerResolved,
    Action<AbilityDefinition, AbilityExecutionResult, DateTimeOffset> MageResolved,
    Action<AbilityDefinition, AbilityExecutionResult, DateTimeOffset> ArcherResolved);
