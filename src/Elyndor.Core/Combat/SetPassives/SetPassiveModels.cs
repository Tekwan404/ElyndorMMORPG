using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Combat.SetPassives;

public enum SetPassiveActorRole
{
    Source,
    Target,
    CompanionOwner,
    OwnerOrCompanion
}

public enum SetPassiveActionKind
{
    ApplyEffect,
    AddShield,
    EmpowerNextDirect,
    ReduceCooldown,
    ExtendTargetEffect,
    RestoreResource,
    ModifyDirectDamage,
    EmpowerNextEffect
}

public sealed record SetPassiveTriggerDefinition(
    CombatEventType EventType,
    SetPassiveActorRole ActorRole);

public sealed record SetPassiveConditionDefinition(
    int EveryNth = 1,
    TimeSpan? InternalCooldown = null,
    IReadOnlyList<string>? AbilityIds = null,
    string? School = null,
    bool CriticalOnly = false,
    bool PositiveAmountOnly = false,
    bool ClassAbilityOnly = false,
    bool ResourceAbilityOnly = false,
    decimal ResourceSpent = 0,
    TimeSpan? SpendingWindow = null,
    IReadOnlyList<string>? TargetEffectIds = null,
    IReadOnlyList<string>? OwnerEffectIds = null,
    bool ControlledTargetOnly = false,
    bool AlternatingSources = false,
    bool OncePerAction = false);

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
    string? IconId = null,
    IReadOnlyList<string>? AbilityIds = null,
    bool SpellOnly = false,
    bool Healing = false,
    bool ClassAbilityOnly = false,
    IReadOnlyList<string>? TargetEffectIds = null,
    bool ControlledTargetOnly = false,
    bool HealingOrDamage = false);

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
