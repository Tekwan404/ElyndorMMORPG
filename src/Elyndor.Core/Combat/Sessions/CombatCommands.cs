using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Items;

namespace Elyndor.Core.Combat.Sessions;

public abstract record CombatCommand(string CommandId);

public sealed record UseAbilityCommand(
    string CommandId,
    string AbilityId,
    Guid TargetActorId) : CombatCommand(CommandId);

public sealed record ResolvedConsumableAction(
    ConsumableActionType Type,
    decimal Amount = 0,
    string? ResourceType = null,
    EffectDefinition? Effect = null,
    string? RemoveEffectId = null,
    string? DispelCategory = null);

public sealed record UseConsumableCommand(
    string CommandId,
    string ItemDefinitionId,
    IReadOnlyList<ResolvedConsumableAction> Actions,
    string CooldownCategoryId,
    TimeSpan Cooldown) : CombatCommand(CommandId);

public sealed record StartAutoAttackCommand(string CommandId) : CombatCommand(CommandId);

public sealed record StopAutoAttackCommand(string CommandId) : CombatCommand(CommandId);

public sealed record SelectTargetCommand(
    string CommandId,
    Guid TargetActorId) : CombatCommand(CommandId);
