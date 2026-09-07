using Elyndor.Core.Combat.Damage;

namespace Elyndor.Core.Combat;

public enum CombatRuntimeEventKind
{
    AbilityStarted,
    AbilityCompleted,
    AbilityInterrupted,
    AutoAttackStarted,
    DamageDealt,
    DamageTaken,
    CriticalHit,
    EnemyKilled,
    HealingApplied,
    ResourceChanged,
    HpThresholdReached,
    PartyEvent,
    CombatEnded
}

public sealed record CombatRuntimeEvent(
    CombatRuntimeEventKind Kind,
    DateTimeOffset OccurredAtUtc,
    Guid SourceActorId,
    Guid? TargetActorId = null,
    string? AbilityId = null,
    string? EffectId = null,
    decimal Amount = 0,
    DamageType? DamageType = null,
    bool IsPeriodic = false,
    bool IsProc = false,
    int ProcDepth = 0,
    string? ProcOriginId = null,
    long Sequence = 0);
