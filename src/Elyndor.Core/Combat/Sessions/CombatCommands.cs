namespace Elyndor.Core.Combat.Sessions;

public abstract record CombatCommand(string CommandId);

public sealed record UseAbilityCommand(
    string CommandId,
    string AbilityId,
    Guid TargetActorId) : CombatCommand(CommandId);

public sealed record StartAutoAttackCommand(string CommandId) : CombatCommand(CommandId);

public sealed record StopAutoAttackCommand(string CommandId) : CombatCommand(CommandId);
