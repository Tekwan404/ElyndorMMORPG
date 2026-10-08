using Elyndor.Core.Combat.Randomness;

namespace Elyndor.Core.Combat.Damage;

/// <summary>
/// Shared pre-HP damage context. Runtime-specific systems may inspect the resolved hit
/// and return a deterministic adjusted amount without mutating actor HP themselves.
/// </summary>
public sealed record IncomingDamageContext(
    CombatActorState Source,
    CombatActorState Target,
    DamageType DamageType,
    decimal BaseAmount,
    decimal RawAmount,
    decimal AfterMitigation,
    decimal CurrentAmount,
    bool WasCritical,
    DateTimeOffset OccurredAtUtc);

public delegate decimal IncomingDamageModifier(
    IncomingDamageContext context,
    IGameRandom random);

/// <summary>
/// Runs after armor, damage modifiers, block and shield absorption, but before HP
/// and lethal prevention are committed. Returned redirected events are published
/// alongside the original hit; the interceptor owns any secondary actor damage.
/// </summary>
public sealed record IncomingHpDamageContext(
    CombatActorState Source,
    CombatActorState Target,
    DamageType DamageType,
    decimal PendingHpDamage,
    DateTimeOffset OccurredAtUtc);

public sealed record IncomingHpDamageResult(
    decimal DamageToTarget,
    IReadOnlyList<CombatEvent> RedirectedEvents);

public delegate IncomingHpDamageResult IncomingHpDamageInterceptor(
    IncomingHpDamageContext context,
    IGameRandom random);
