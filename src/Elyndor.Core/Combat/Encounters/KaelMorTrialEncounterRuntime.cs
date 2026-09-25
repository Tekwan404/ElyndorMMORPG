namespace Elyndor.Core.Combat.Encounters;

public sealed record KaelMorTrialEncounterDefinition(
    IReadOnlyList<decimal> TriggerHpPercents,
    TimeSpan TrialLifetime,
    decimal FailureHealMaxHpRatio,
    decimal FailureDamageBonusPercent,
    TimeSpan SuccessBuffDuration)
{
    public static KaelMorTrialEncounterDefinition Default { get; } = new(
        [0.75m, 0.50m, 0.25m],
        TimeSpan.FromSeconds(15),
        FailureHealMaxHpRatio: 0.05m,
        FailureDamageBonusPercent: 0.05m,
        SuccessBuffDuration: TimeSpan.FromSeconds(15));

    public void Validate()
    {
        if (TriggerHpPercents is null
            || TriggerHpPercents.Count == 0
            || TriggerHpPercents.Any(value => value <= 0 || value >= 1)
            || TriggerHpPercents.Distinct().Count() != TriggerHpPercents.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(TriggerHpPercents));
        }
        if (TrialLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(TrialLifetime));
        if (FailureHealMaxHpRatio is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(FailureHealMaxHpRatio));
        if (FailureDamageBonusPercent < 0)
            throw new ArgumentOutOfRangeException(nameof(FailureDamageBonusPercent));
        if (SuccessBuffDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(SuccessBuffDuration));
    }
}

public sealed record KaelMorTrialState(
    Guid OwnerActorId,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyCollection<Guid> ActiveAddActorIds);

public sealed class KaelMorTrialEncounterRuntime
{
    private readonly KaelMorTrialEncounterDefinition _definition;
    private readonly Queue<decimal> _pendingThresholds;
    private Guid? _ownerActorId;
    private DateTimeOffset _expiresAtUtc;
    private readonly HashSet<Guid> _activeAddActorIds = [];

    public KaelMorTrialEncounterRuntime(KaelMorTrialEncounterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        _definition = definition;
        _pendingThresholds = new Queue<decimal>(definition.TriggerHpPercents.OrderDescending());
    }

    public bool HasActiveTrial => _ownerActorId.HasValue;
    public Guid? ActiveOwnerActorId => _ownerActorId;
    public int RemainingTrials => _pendingThresholds.Count;
    public TimeSpan TrialLifetime => _definition.TrialLifetime;
    public decimal FailureHealMaxHpRatio => _definition.FailureHealMaxHpRatio;
    public decimal FailureDamageBonusPercent => _definition.FailureDamageBonusPercent;
    public TimeSpan SuccessBuffDuration => _definition.SuccessBuffDuration;
    public IReadOnlyCollection<Guid> ActiveAddActorIds => _activeAddActorIds;

    public bool CanActorTargetEnemy(Guid actorId, Guid targetActorId)
    {
        if (!HasActiveTrial)
            return true;

        bool isTrialAdd = _activeAddActorIds.Contains(targetActorId);
        return _ownerActorId == actorId ? isTrialAdd : !isTrialAdd;
    }

    public bool TryBeginNextTrial(
        decimal currentHp,
        decimal maxHp,
        IReadOnlyList<Guid> eligibleOwners,
        DateTimeOffset now,
        out Guid ownerActorId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHp);
        ArgumentOutOfRangeException.ThrowIfNegative(currentHp);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(currentHp, maxHp);
        ArgumentNullException.ThrowIfNull(eligibleOwners);
        ownerActorId = Guid.Empty;
        if (HasActiveTrial || _pendingThresholds.Count == 0)
            return false;

        if (currentHp / maxHp > _pendingThresholds.Peek())
            return false;

        ownerActorId = eligibleOwners.FirstOrDefault(id => id != Guid.Empty);
        if (ownerActorId == Guid.Empty)
            return false;

        _pendingThresholds.Dequeue();
        _ownerActorId = ownerActorId;
        _expiresAtUtc = now + _definition.TrialLifetime;
        _activeAddActorIds.Clear();
        return true;
    }

    public void RegisterAdd(Guid addActorId)
    {
        if (!HasActiveTrial || addActorId == Guid.Empty)
            throw new InvalidOperationException("An active trial and a valid add actor are required.");
        if (!_activeAddActorIds.Add(addActorId))
            throw new InvalidOperationException("Trial add is already registered.");
    }

    public KaelMorTrialState? RegisterAddDeath(Guid addActorId)
    {
        if (!HasActiveTrial || !_activeAddActorIds.Remove(addActorId) || _activeAddActorIds.Count > 0)
            return null;

        return CompleteTrial();
    }

    public KaelMorTrialState? RegisterTimeout(Guid addActorId, DateTimeOffset now)
    {
        if (!HasActiveTrial
            || !_activeAddActorIds.Contains(addActorId)
            || now < _expiresAtUtc)
        {
            return null;
        }

        return CompleteTrial();
    }

    public KaelMorTrialState? CancelActiveTrial() =>
        HasActiveTrial ? CompleteTrial() : null;

    private KaelMorTrialState CompleteTrial()
    {
        KaelMorTrialState state = new(
            _ownerActorId!.Value,
            _expiresAtUtc,
            _activeAddActorIds.ToArray());
        _ownerActorId = null;
        _activeAddActorIds.Clear();
        return state;
    }
}
