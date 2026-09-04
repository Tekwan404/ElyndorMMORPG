using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Combat.Sessions;

public enum CombatSessionStatus
{
    Active,
    Victory,
    Defeat,
    Cancelled
}

public enum CombatActorKind
{
    Player,
    Monster
}

public sealed record AutoAttackProfile(
    TimeSpan Interval,
    decimal BaseDamage,
    decimal AttackPowerCoefficient,
    decimal ResourceOnHit);

public sealed record CombatParticipantDefinition(
    CombatActorState Actor,
    CombatActorKind Kind,
    string DefinitionId,
    string Name,
    string ResourceType,
    AutoAttackProfile AutoAttack,
    IReadOnlySet<string> KnownAbilityIds,
    decimal ResourceRegenPerSecond = 0);

public sealed record CombatEffectSnapshot(string Id, int Stacks, DateTimeOffset ExpiresAtUtc);
public sealed record CombatAbilitySnapshot(string Id, decimal ResourceCost, TimeSpan Cooldown);

public sealed record CombatActorSnapshot(
    Guid ActorId,
    CombatActorKind Kind,
    string DefinitionId,
    string Name,
    decimal Hp,
    decimal MaxHp,
    string ResourceType,
    decimal Resource,
    decimal MaxResource,
    bool AutoAttackEnabled,
    DateTimeOffset? ConsumableCooldownReadyAtUtc,
    IReadOnlyDictionary<string, DateTimeOffset> Cooldowns,
    IReadOnlySet<string> KnownAbilityIds,
    IReadOnlyList<CombatAbilitySnapshot> Abilities,
    IReadOnlyList<CombatEffectSnapshot> Effects);

public sealed record CombatSessionSnapshot(
    Guid SessionId,
    long Sequence,
    CombatSessionStatus Status,
    DateTimeOffset ServerTimeUtc,
    CombatActorSnapshot Player,
    CombatActorSnapshot Enemy,
    string ContentVersion = "UNVERSIONED",
    string BalanceVersion = "UNVERSIONED");

public static class CombatErrorCodes
{
    public const string NotFound = "combat_not_found";
    public const string Ended = "combat_ended";
    public const string DuplicateCommand = "duplicate_command";
    public const string AbilityNotKnown = "ability_not_known";
    public const string AbilityOnCooldown = "ability_on_cooldown";
    public const string InsufficientResource = "insufficient_resource";
    public const string InvalidTarget = "invalid_target";
    public const string ActorDead = "actor_dead";
    public const string CommandRejected = "combat_command_rejected";
    public const string AlreadyActive = "combat_already_active";
    public const string InvalidEncounter = "combat_encounter_invalid";
    public const string UnsupportedMonster = "combat_monster_unsupported";
    public const string UnsupportedClass = "combat_class_unsupported";
    public const string InvalidLocation = "combat_location_invalid";
    public const string ConsumableOnCooldown = "combat_consumable_on_cooldown";
    public const string ConsumableNotNeeded = "combat_consumable_not_needed";
    public const string ConsumableUnavailable = "combat_consumable_unavailable";
}

public sealed record CombatCommandResult(
    bool Succeeded,
    string? ErrorCode,
    CombatSessionSnapshot Snapshot,
    IReadOnlyList<CombatEvent> Events);
