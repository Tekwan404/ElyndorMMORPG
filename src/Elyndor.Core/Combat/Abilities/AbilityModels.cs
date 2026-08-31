namespace Elyndor.Core.Combat.Abilities;

using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;

public enum AbilityType { Instant, Casted, NextAttackModifier, Taunt }
public enum AbilityTargetType
{
    Self,
    SingleAlly,
    SingleEnemy,
    AllEnemiesInCombat,
    NEnemiesInCombat,
    SelfAndPartyMembersInCombat,
    ActiveCompanion,
    Owner
}
public enum GlobalCooldownCategory { None, Reduced, Standard }
public enum AbilityActionType { Damage, Healing, ApplyEffect, ResourceChange, Taunt }
public enum AbilityErrorCode
{
    None,
    DuplicateCommand,
    InvalidTarget,
    DeadActor,
    InsufficientResource,
    CooldownActive,
    GlobalCooldownActive,
    SchoolLocked,
    ActorStunned,
    ActorSilenced,
    AbilityUnavailable,
    CastAlreadyActive,
    NoActiveCast,
    CastNotReady,
    CastNotInterruptible
}

public sealed record AbilityDefinition(
    string Id,
    AbilityType Type,
    AbilityTargetType TargetType,
    decimal ResourceCost,
    TimeSpan Cooldown,
    TimeSpan CastTime,
    bool UsesGlobalCooldown,
    GlobalCooldownCategory GlobalCooldownCategory,
    bool IsSpell,
    string School,
    bool Interruptible = true,
    bool AllowSelfTarget = true,
    bool CanUseWhileCasting = false,
    bool CanUseWhileSilenced = false,
    IReadOnlyList<AbilityActionDefinition>? Actions = null);

public sealed record AbilityActionDefinition(
    AbilityActionType Type,
    decimal Amount = 0,
    DamageType DamageType = DamageType.Physical,
    EffectDefinition? Effect = null,
    bool CanMiss = true,
    bool CanCrit = true,
    bool CanDodge = true,
    decimal AttackPowerCoefficient = 0,
    TimeSpan? Duration = null);

public sealed record AbilityIntent(string CommandId, string AbilityId, Guid TargetId);

public sealed record ActiveCast(
    Guid CastId,
    AbilityDefinition Ability,
    Guid TargetId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ResolvesAtUtc);

public sealed record AbilityExecutionResult(
    bool Succeeded,
    AbilityErrorCode ErrorCode,
    IReadOnlyList<CombatEvent> Events)
{
    public static AbilityExecutionResult Failure(AbilityErrorCode code) => new(false, code, []);
}

public sealed class CombatRuntimeState(CombatActorState actor)
{
    public CombatActorState Actor { get; } = actor;
    public Dictionary<Guid, CombatActorState> Actors { get; } = new() { [actor.ActorId] = actor };
    public Dictionary<string, DateTimeOffset> Cooldowns { get; } = [];
    public Dictionary<string, DateTimeOffset> SchoolLockouts { get; } = [];
    public HashSet<string> ProcessedCommandIds { get; } = [];
    public DateTimeOffset? GlobalCooldownEndsAtUtc { get; internal set; }
    public ActiveCast? ActiveCast { get; internal set; }
    public long Version { get; internal set; }

    public void AddActor(CombatActorState actor) => Actors.Add(actor.ActorId, actor);
}
