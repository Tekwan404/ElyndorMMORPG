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
public enum AbilityTargetSelectorProfile
{
    EncounterOrder,
    RandomEnemy,
    CurrentThreatTarget,
    HighestThreat,
    LowestHpAlly,
    RandomManaUser,
    NonTankRandom,
    CastInProgressEnemy,
    OwnerLinkedTarget
}
public enum AbilityActionType { Damage, Healing, ApplyEffect, ResourceChange, Dispel, Taunt, Interrupt }
public enum AbilityResourceTarget { Caster, Target }
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
    ActorRooted,
    ActorFeared,
    ActorDisarmed,
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
    IReadOnlyList<AbilityActionDefinition>? Actions = null,
    bool CanUseWhileStunned = false,
    decimal DamageMultiplier = 1,
    decimal AccuracyBonus = 0,
    decimal CriticalChanceBonus = 0,
    decimal CriticalDamageBonus = 0,
    decimal MagicPenetrationBonus = 0,
    string? DisplayName = null,
    string? Description = null,
    string? IconId = null,
    int TargetCount = 0,
    AbilityTargetSelectorProfile TargetSelectorProfile = AbilityTargetSelectorProfile.EncounterOrder,
    IReadOnlyDictionary<string, decimal>? RuntimeParameters = null,
    string? RequiredActiveEffectId = null,
    string? FreeResourceCostWhileEffectId = null,
    string? ConsumeEffectId = null,
    bool RequiresWeapon = false,
    bool RequiresMobility = false,
    bool CanUseWhileFeared = false);

public sealed record AbilityActionDefinition(
    AbilityActionType Type,
    decimal Amount = 0,
    DamageType DamageType = DamageType.Physical,
    EffectDefinition? Effect = null,
    bool CanMiss = true,
    bool CanCrit = true,
    bool CanDodge = true,
    decimal AttackPowerCoefficient = 0,
    TimeSpan? Duration = null,
    decimal ArmorPenetrationBonus = 0,
    decimal SpellPowerCoefficient = 0,
    decimal DamagePerCharacterLevel = 0,
    bool IsUnblockable = false,
    decimal BlockValueCoefficient = 0,
    bool HealingCanCrit = false,
    AbilityResourceTarget ResourceTarget = AbilityResourceTarget.Caster,
    string? DispelCategory = null,
    TimeSpan? Delay = null,
    TimeSpan? InterruptLockout = null,
    decimal LifestealPercent = 0);

public sealed record AbilityTargetModifier(
    decimal DamageMultiplier = 1,
    decimal AccuracyBonus = 0,
    decimal CriticalChanceBonus = 0,
    decimal CriticalDamageBonus = 0,
    decimal ArmorPenetrationBonus = 0,
    decimal MagicPenetrationBonus = 0);

public sealed record AbilityIntent(
    string CommandId,
    string AbilityId,
    Guid TargetId,
    IReadOnlyList<Guid>? TargetIds = null,
    IReadOnlyDictionary<Guid, AbilityTargetModifier>? TargetModifiers = null);

public sealed record ActiveCast(
    Guid CastId,
    AbilityDefinition Ability,
    Guid TargetId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ResolvesAtUtc,
    IReadOnlyList<Guid>? TargetIds = null,
    IReadOnlyDictionary<Guid, AbilityTargetModifier>? TargetModifiers = null);

public sealed record PendingAbilityAction(
    long Sequence,
    AbilityDefinition Ability,
    AbilityActionDefinition Action,
    Guid TargetId,
    AbilityTargetModifier TargetModifier,
    DateTimeOffset ExecuteAtUtc);

public sealed record AbilityExecutionResult(
    bool Succeeded,
    AbilityErrorCode ErrorCode,
    IReadOnlyList<CombatEvent> Events)
{
    public static AbilityExecutionResult Failure(AbilityErrorCode code) => new(false, code, []);
}

public sealed class CombatRuntimeState(CombatActorState actor)
{
    private long _pendingActionSequence;

    public CombatActorState Actor { get; } = actor;
    public Dictionary<Guid, CombatActorState> Actors { get; } = new() { [actor.ActorId] = actor };
    public Dictionary<string, DateTimeOffset> Cooldowns { get; } = [];
    public Dictionary<string, DateTimeOffset> SchoolLockouts { get; } = [];
    public HashSet<string> ProcessedCommandIds { get; } = [];
    public List<PendingAbilityAction> PendingActions { get; } = [];
    public DateTimeOffset? GlobalCooldownEndsAtUtc { get; internal set; }
    public ActiveCast? ActiveCast { get; internal set; }
    public long Version { get; internal set; }
    public DateTimeOffset? NextPendingActionAtUtc => PendingActions.Count == 0
        ? null
        : PendingActions.Min(action => action.ExecuteAtUtc);

    public void AddActor(CombatActorState actor) => Actors.Add(actor.ActorId, actor);

    internal void SchedulePendingAction(
        AbilityDefinition ability,
        AbilityActionDefinition action,
        Guid targetId,
        AbilityTargetModifier targetModifier,
        DateTimeOffset executeAtUtc)
    {
        PendingActions.Add(new PendingAbilityAction(
            ++_pendingActionSequence,
            ability,
            action,
            targetId,
            targetModifier,
            executeAtUtc));
    }
}
