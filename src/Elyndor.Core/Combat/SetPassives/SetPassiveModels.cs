namespace Elyndor.Core.Combat.SetPassives;

public enum SetPassiveActorRole
{
    Source,
    Target
}

public enum SetPassiveActionKind
{
    ApplyEffect,
    ApplyShield,
    GainResource,
    GrantProcToken
}

public sealed record SetPassiveTriggerDefinition(
    CombatEventType EventType,
    SetPassiveActorRole ActorRole,
    int RequiredOccurrences = 1);

public sealed record SetPassiveActionDefinition(
    SetPassiveActionKind Kind,
    string? ReferenceId = null,
    decimal Value = 0m,
    int DurationTicks = 0,
    bool ScaleWithMaxHp = false);

public sealed record SetPassiveDefinition(
    string Id,
    string SetId,
    int RequiredPieces,
    SetPassiveTriggerDefinition Trigger,
    IReadOnlyList<SetPassiveActionDefinition> Actions,
    int InternalCooldownTicks = 0);

public sealed record SetPassiveActionInvocation(
    string PassiveId,
    string SetId,
    Guid ActorId,
    int Tick,
    SetPassiveActionDefinition Action);
