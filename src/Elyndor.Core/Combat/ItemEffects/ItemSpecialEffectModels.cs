using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Combat.ItemEffects;

public enum ItemSpecialEffectActorRole
{
    Source,
    Target
}

public enum ItemSpecialEffectTargetRole
{
    Owner,
    EventSource,
    EventTarget
}

public enum ItemSpecialEffectActionKind
{
    ApplyEffect,
    AddShield,
    TriggerDamage,
    Heal,
    RestoreResource,
    ModifyCooldown
}

public sealed record ItemSpecialEffectTriggerDefinition(
    CombatEventType EventType,
    ItemSpecialEffectActorRole ActorRole,
    IReadOnlyList<string>? DefinitionIds = null,
    DamageType? DamageType = null);

public sealed record ItemSpecialEffectConditionDefinition(
    int EveryNth = 1,
    TimeSpan? InternalCooldown = null);

public sealed record ItemSpecialEffectActionDefinition(
    ItemSpecialEffectActionKind Kind,
    ItemSpecialEffectTargetRole TargetRole = ItemSpecialEffectTargetRole.Owner,
    string? ReferenceId = null,
    decimal Magnitude = 0m,
    decimal EventAmountPercent = 0m,
    TimeSpan? Duration = null,
    EffectStat? ModifiedStat = null,
    EffectModifierMode ModifierMode = EffectModifierMode.Flat,
    EffectStackPolicy StackPolicy = EffectStackPolicy.Refresh,
    int MaxStacks = 1,
    bool ScaleWithMaxHp = false,
    DamageType DamageType = DamageType.Physical,
    decimal AttackPowerCoefficient = 0m,
    decimal SpellPowerCoefficient = 0m,
    string? AbilityId = null,
    string? DisplayName = null,
    string? Description = null,
    string? IconId = null);

public sealed record ItemSpecialEffectDefinition(
    string Id,
    ItemSpecialEffectTriggerDefinition Trigger,
    ItemSpecialEffectConditionDefinition Conditions,
    IReadOnlyList<ItemSpecialEffectActionDefinition> Actions);

public sealed record ItemSpecialEffectInvocation(
    string EffectId,
    Guid OwnerActorId,
    DateTimeOffset OccurredAtUtc,
    CombatEvent TriggerEvent,
    ItemSpecialEffectActionDefinition Action);
