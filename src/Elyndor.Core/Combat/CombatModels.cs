using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Talents;

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
    decimal SpellPower = 0,
    decimal BlockChance = 0,
    decimal BlockValueMin = 0,
    decimal BlockValueMax = 0)
{
    public static CombatStats Default { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 0);
}

public sealed class CombatActorState
{
    private readonly decimal _baseMaxHp;
    private readonly Dictionary<string, decimal> _temporaryMaxHpPercentBonuses = new(StringComparer.Ordinal);
    private int _deferredDeathScopes;

    public CombatActorState(
        Guid actorId,
        decimal maxHp,
        decimal currentHp,
        decimal maxResource,
        decimal currentResource,
        CombatStats stats,
        TalentCombatModifiers? talentModifiers = null,
        bool canDie = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHp);
        ArgumentOutOfRangeException.ThrowIfNegative(maxResource);
        ActorId = actorId;
        _baseMaxHp = maxHp;
        MaxHp = maxHp;
        CanDie = canDie;
        CurrentHp = ClampHp(currentHp);
        MaxResource = maxResource;
        CurrentResource = Math.Clamp(currentResource, 0, maxResource);
        Stats = stats;
        TalentModifiers = talentModifiers ?? new TalentCombatModifiers();
    }

    public Func<string, bool, CombatActorState, DateTimeOffset, bool, decimal>? SetPassiveMultiplier { get; set; }
    public Func<string, DateTimeOffset, decimal>? SetPassiveEffectMultiplier { get; set; }

    public Guid ActorId { get; }
    public decimal BaseMaxHp => _baseMaxHp;
    public decimal MaxHp { get; private set; }
    public decimal CurrentHp { get; private set; }
    public decimal MaxResource { get; private set; }
    public decimal CurrentResource { get; private set; }
    public CombatStats Stats { get; }
    public TalentCombatModifiers TalentModifiers { get; }
    public IncomingDamageModifier? IncomingDamageModifier { get; set; }
    public IncomingHpDamageInterceptor? IncomingHpDamageInterceptor { get; set; }
    public Func<Guid, EffectDefinition, DateTimeOffset, EffectApplicationPolicyResult>? EffectApplicationPolicy { get; set; }
    public decimal IncomingCriticalDamageReductionPercent { get; set; }
    public decimal IncomingControlDurationMultiplier { get; set; } = 1;
    public decimal OwnShieldMagnitudeMultiplier { get; set; } = 1;
    public bool CanDie { get; }
    public DateTimeOffset? UntargetableUntilUtc { get; private set; }
    public List<ActiveEffect> ActiveEffects { get; } = [];
    public bool IsDead => CanDie && CurrentHp <= 0 && _deferredDeathScopes == 0;

    public static CombatActorState CreateDummy(
        decimal maxHp,
        decimal maxResource = 100,
        decimal? resource = null,
        CombatStats? stats = null,
        TalentCombatModifiers? talentModifiers = null,
        bool canDie = true) =>
        new(
            Guid.NewGuid(),
            maxHp,
            maxHp,
            maxResource,
            resource ?? maxResource,
            stats ?? CombatStats.Default,
            talentModifiers,
            canDie);

    public void SetCurrentHp(decimal value) => CurrentHp = ClampHp(value);
    public void ApplyDamage(decimal value) => SetCurrentHp(CurrentHp - Math.Max(0, value));
    public void ApplyHealing(decimal value) => SetCurrentHp(CurrentHp + Math.Max(0, value));

    internal IDisposable DeferDeathForCurrentBatch()
    {
        if (IsDead)
            throw new InvalidOperationException("Death can only be deferred for an actor alive at batch start.");

        _deferredDeathScopes++;
        return new DeathDeferralLease(this);
    }

    public bool IsTargetable(DateTimeOffset now) =>
        UntargetableUntilUtc is not { } until || now >= until;

    public void SetTemporaryUntargetable(DateTimeOffset now, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "Untargetable duration must be positive.");
        }

        DateTimeOffset proposed = now + duration;
        if (UntargetableUntilUtc is not { } current || proposed > current)
            UntargetableUntilUtc = proposed;
    }

    public void ConfigureResource(decimal maxResource, decimal currentResource)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxResource);
        MaxResource = maxResource;
        CurrentResource = Math.Clamp(currentResource, 0, maxResource);
    }

    public void SetTemporaryMaxHpPercentBonus(
        string sourceId,
        decimal percent,
        bool healByIncrease,
        IReadOnlyList<string>? replacedSources = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        decimal previousMaxHp = MaxHp;
        foreach (string replaced in replacedSources ?? [])
            _temporaryMaxHpPercentBonuses.Remove(replaced);
        _temporaryMaxHpPercentBonuses[sourceId] = Math.Max(0, percent);
        RecalculateMaxHp();
        if (healByIncrease && MaxHp > previousMaxHp)
        {
            CurrentHp = ClampHp(CurrentHp + MaxHp - previousMaxHp);
        }
        CurrentHp = ClampHp(CurrentHp);
    }

    public void RemoveTemporaryMaxHpPercentBonus(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (!_temporaryMaxHpPercentBonuses.Remove(sourceId))
            return;

        RecalculateMaxHp();
        CurrentHp = ClampHp(CurrentHp);
    }

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

    private void RecalculateMaxHp()
    {
        decimal totalPercent = _temporaryMaxHpPercentBonuses.Values.Sum();
        MaxHp = _baseMaxHp * (1 + totalPercent / 100m);
    }

    private decimal ClampHp(decimal value)
    {
        decimal minimum = CanDie ? 0 : Math.Min(1m, MaxHp);
        return Math.Clamp(value, minimum, MaxHp);
    }

    private void ReleaseDeathDeferral()
    {
        if (_deferredDeathScopes <= 0)
            throw new InvalidOperationException("Death deferral scope is not active.");
        _deferredDeathScopes--;
    }

    private sealed class DeathDeferralLease(CombatActorState actor) : IDisposable
    {
        private CombatActorState? _actor = actor;

        public void Dispose()
        {
            CombatActorState? actorToRelease = Interlocked.Exchange(ref _actor, null);
            actorToRelease?.ReleaseDeathDeferral();
        }
    }
}

public enum CombatWeaponHand
{
    MainHand,
    OffHand
}

public enum CombatEventType
{
    CombatStarted,
    AbilityUsed,
    ConsumableUsed,
    AutoAttackStarted,
    AutoAttackStopped,
    TargetChanged,
    ActorSummoned,
    DamageDealt,
    CriticalHit,
    EnemyKilled,
    CombatEnded,
    EffectApplied,
    EffectRefreshed,
    EffectTicked,
    EffectExpired,
    EffectRemoved,
    ShieldAbsorbed,
    HealingApplied,
    AbilityStarted,
    AbilityCompleted,
    AbilityInterrupted,
    TauntApplied,
    ThreatAdded,
    ThreatDropped,
    ThreatCleared,
    FixateApplied,
    TargetabilityChanged,
    ResourceChanged,
    DamageBlocked,
    UnblockableHit,
    Dodge,
    ActorDied,
    EffectImmune,
    ActionRejected
}

public sealed record CombatEvent(
    CombatEventType Type,
    DateTimeOffset OccurredAtUtc,
    Guid ActorId,
    string? DefinitionId = null,
    decimal Amount = 0,
    Guid? SourceActorId = null,
    Guid? TargetActorId = null,
    long Sequence = 0,
    bool IsPeriodic = false,
    decimal AmountBeforeShields = 0,
    DamageType? DamageType = null,
    CombatWeaponHand? WeaponHand = null,
    string? WeaponDefinitionId = null,
    decimal RawDamage = 0,
    decimal DamageAfterMitigation = 0,
    decimal DamageBeforeBlock = 0,
    bool IsUnblockable = false,
    bool IsCritical = false,
    HealingOrigin? HealingOrigin = null,
    bool IsReflected = false,
    bool IsProc = false,
    int ProcDepth = 0,
    string? ProcOriginId = null)
{
    // Preserved by record copies and event adapters, not exposed in transport DTOs.
    internal object ProcDispatchToken { get; init; } = new();
    public decimal BaseDamage { get; init; }
}
