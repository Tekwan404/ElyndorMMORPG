using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Combat.SetPassives;

public enum SetPassiveActorRole
{
    Source,
    Target
}

public enum SetPassiveActionKind
{
    ApplyEffect,
    AddShield
}

public sealed record SetPassiveTriggerDefinition(
    CombatEventType EventType,
    SetPassiveActorRole ActorRole);

public sealed record SetPassiveConditionDefinition(
    int EveryNth = 1,
    TimeSpan? InternalCooldown = null);

public sealed record SetPassiveActionDefinition(
    SetPassiveActionKind Kind,
    string? ReferenceId = null,
    decimal Magnitude = 0m,
    TimeSpan? Duration = null,
    EffectStat? ModifiedStat = null,
    EffectModifierMode ModifierMode = EffectModifierMode.Flat,
    EffectStackPolicy StackPolicy = EffectStackPolicy.Refresh,
    int MaxStacks = 1,
    bool ScaleWithMaxHp = false,
    string? DisplayName = null,
    string? Description = null,
    string? IconId = null);

public sealed record SetPassiveDefinition(
    string Id,
    string SetId,
    int RequiredPieces,
    SetPassiveTriggerDefinition Trigger,
    SetPassiveConditionDefinition Conditions,
    IReadOnlyList<SetPassiveActionDefinition> Actions);

public sealed record SetPassiveActionInvocation(
    string PassiveId,
    string SetId,
    Guid ActorId,
    DateTimeOffset OccurredAtUtc,
    SetPassiveActionDefinition Action);
