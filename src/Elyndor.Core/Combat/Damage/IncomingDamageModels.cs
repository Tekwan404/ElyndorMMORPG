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
