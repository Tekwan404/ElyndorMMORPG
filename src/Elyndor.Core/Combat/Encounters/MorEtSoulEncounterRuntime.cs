namespace Elyndor.Core.Combat.Encounters;

public sealed record MorEtSoulEncounterDefinition(
    IReadOnlyList<decimal> TriggerHpPercents,
    IReadOnlyList<int> WaveSizes,
    TimeSpan SoulLifetime,
    decimal FailureHealMaxHpRatio,
    decimal FailureDamageBonusPercent,
    TimeSpan SuccessBuffDuration)
{
    public static MorEtSoulEncounterDefinition Default { get; } = new(
        [0.65m, 0.30m],
        [2, 3],
        TimeSpan.FromSeconds(18),
        FailureHealMaxHpRatio: 0.07m,
        FailureDamageBonusPercent: 0.08m,
        SuccessBuffDuration: TimeSpan.FromSeconds(15));

    public void Validate()
    {
        if (TriggerHpPercents is null
            || WaveSizes is null
            || TriggerHpPercents.Count == 0
            || TriggerHpPercents.Count != WaveSizes.Count)
        {
            throw new ArgumentException("Soul encounter thresholds and wave sizes must align.");
        }
        if (TriggerHpPercents.Any(value => value <= 0 || value >= 1)
            || TriggerHpPercents.Distinct().Count() != TriggerHpPercents.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(TriggerHpPercents));
        }
        if (WaveSizes.Any(value => value <= 0))
            throw new ArgumentOutOfRangeException(nameof(WaveSizes));
        if (SoulLifetime <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(SoulLifetime));
        if (FailureHealMaxHpRatio < 0 || FailureHealMaxHpRatio > 1)
            throw new ArgumentOutOfRangeException(nameof(FailureHealMaxHpRatio));
        if (FailureDamageBonusPercent < 0)
            throw new ArgumentOutOfRangeException(nameof(FailureDamageBonusPercent));
        if (SuccessBuffDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(SuccessBuffDuration));
    }
}

public sealed record MorEtSoulState(
    Guid SoulActorId,
    Guid OwnerActorId,
    DateTimeOffset ExpiresAtUtc);

public sealed class MorEtSoulEncounterRuntime
{
    private readonly MorEtSoulEncounterDefinition _definition;
    private readonly Queue<(decimal Threshold, int Size)> _pendingWaves;
    private readonly Dictionary<Guid, MorEtSoulState> _activeSouls = [];

    public MorEtSoulEncounterRuntime(MorEtSoulEncounterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        _definition = definition;
        _pendingWaves = new Queue<(decimal Threshold, int Size)>(
            definition.TriggerHpPercents
                .Zip(definition.WaveSizes)
                .OrderByDescending(item => item.First)
                .Select(item => (item.First, item.Second)));
    }

    public int ActiveSoulCount => _activeSouls.Count;
    public int RemainingWaves => _pendingWaves.Count;
    public bool HasActiveWave => _activeSouls.Count > 0;
    public bool IsComplete => _pendingWaves.Count == 0 && _activeSouls.Count == 0;
    public IReadOnlyCollection<Guid> ActiveSoulActorIds => _activeSouls.Keys;
    public TimeSpan SoulLifetime => _definition.SoulLifetime;
    public decimal FailureHealMaxHpRatio => _definition.FailureHealMaxHpRatio;
    public decimal FailureDamageBonusPercent => _definition.FailureDamageBonusPercent;
    public TimeSpan SuccessBuffDuration => _definition.SuccessBuffDuration;

    public bool TryBeginNextWave(
        decimal currentHp,
        decimal maxHp,
        IReadOnlyList<Guid> eligibleOwners,
        out IReadOnlyList<Guid> selectedOwners)
    {
        ValidateHp(currentHp, maxHp);
        ArgumentNullException.ThrowIfNull(eligibleOwners);
        selectedOwners = [];
        if (HasActiveWave || _pendingWaves.Count == 0 || eligibleOwners.Count == 0)
            return false;

        (decimal threshold, int size) = _pendingWaves.Peek();
        if (currentHp / maxHp > threshold)
            return false;

        _pendingWaves.Dequeue();
        selectedOwners = eligibleOwners
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(size)
            .ToArray();
        return selectedOwners.Count > 0;
    }

    public MorEtSoulState RegisterSoul(Guid soulActorId, Guid ownerActorId, DateTimeOffset now)
    {
        if (soulActorId == Guid.Empty || ownerActorId == Guid.Empty)
            throw new ArgumentException("Soul and owner identifiers are required.");
        if (_activeSouls.ContainsKey(soulActorId))
            throw new InvalidOperationException("Soul actor is already registered.");
        if (_activeSouls.Values.Any(state => state.OwnerActorId == ownerActorId))
            throw new InvalidOperationException("A player can own only one active soul in a wave.");

        MorEtSoulState state = new(soulActorId, ownerActorId, now + _definition.SoulLifetime);
        _activeSouls.Add(soulActorId, state);
        return state;
    }

    public MorEtSoulState? RegisterSoulDeath(Guid soulActorId)
    {
        if (!_activeSouls.Remove(soulActorId, out MorEtSoulState? state))
            return null;
        return state;
    }

    public MorEtSoulState? RegisterSoulTimeout(Guid soulActorId, DateTimeOffset now)
    {
        if (!_activeSouls.TryGetValue(soulActorId, out MorEtSoulState? state)
            || now < state.ExpiresAtUtc)
        {
            return null;
        }

        _activeSouls.Remove(soulActorId);
        return state;
    }

    public MorEtSoulState? GetSoul(Guid soulActorId) =>
        _activeSouls.GetValueOrDefault(soulActorId);

    private static void ValidateHp(decimal currentHp, decimal maxHp)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHp);
        ArgumentOutOfRangeException.ThrowIfNegative(currentHp);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(currentHp, maxHp);
    }
}
