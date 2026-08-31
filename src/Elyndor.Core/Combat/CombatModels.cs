using Elyndor.Core.Combat.Effects;

namespace Elyndor.Core.Combat;

public sealed record CombatStats(
    int Level,
    decimal Accuracy,
    decimal Dodge,
    decimal CriticalChance,
    decimal CriticalDamage,
    decimal Armor,
    decimal MagicResistance,
    decimal ArmorPenetration,
    decimal MagicPenetration,
    decimal AttackPower = 0,
    decimal SpellPower = 0)
{
    public static CombatStats Default { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 0);
}

public sealed class CombatActorState
{
    public CombatActorState(
        Guid actorId,
        decimal maxHp,
        decimal currentHp,
        decimal maxResource,
        decimal currentResource,
        CombatStats stats)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHp);
        ArgumentOutOfRangeException.ThrowIfNegative(maxResource);
        ActorId = actorId;
        MaxHp = maxHp;
        CurrentHp = Math.Clamp(currentHp, 0, maxHp);
        MaxResource = maxResource;
        CurrentResource = Math.Clamp(currentResource, 0, maxResource);
        Stats = stats;
    }

    public Guid ActorId { get; }
    public decimal MaxHp { get; }
    public decimal CurrentHp { get; private set; }
    public decimal MaxResource { get; }
    public decimal CurrentResource { get; private set; }
    public CombatStats Stats { get; }
    public List<ActiveEffect> ActiveEffects { get; } = [];
    public bool IsDead => CurrentHp <= 0;

    public static CombatActorState CreateDummy(
        decimal maxHp,
        decimal maxResource = 100,
        decimal? resource = null,
        CombatStats? stats = null) =>
        new(Guid.NewGuid(), maxHp, maxHp, maxResource, resource ?? maxResource, stats ?? CombatStats.Default);

    public void SetCurrentHp(decimal value) => CurrentHp = Math.Clamp(value, 0, MaxHp);
    public void ApplyDamage(decimal value) => SetCurrentHp(CurrentHp - Math.Max(0, value));
    public void ApplyHealing(decimal value) => SetCurrentHp(CurrentHp + Math.Max(0, value));

    public bool TrySpendResource(decimal amount)
    {
        if (amount < 0 || CurrentResource < amount)
        {
            return false;
        }

        CurrentResource -= amount;
        return true;
    }

    public decimal AddResource(decimal amount)
    {
        decimal previous = CurrentResource;
        CurrentResource = Math.Clamp(CurrentResource + amount, 0, MaxResource);
        return CurrentResource - previous;
    }
}

public enum CombatEventType
{
    EffectApplied,
    EffectRefreshed,
    EffectTicked,
    EffectExpired,
    EffectRemoved,
    ShieldAbsorbed,
    DamageApplied,
    HealingApplied,
    AbilityStarted,
    AbilityCompleted,
    AbilityInterrupted,
    TauntApplied,
    ResourceChanged,
    ActorDied
}

public sealed record CombatEvent(
    CombatEventType Type,
    DateTimeOffset OccurredAtUtc,
    Guid ActorId,
    string? DefinitionId = null,
    decimal Amount = 0);
