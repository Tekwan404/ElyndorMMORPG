namespace Elyndor.Core.Combat.Encounters;

public enum MirrorEncounterAddRole
{
    Guardian,
    Priest,
    Executioner
}

public sealed record MirrorBarrierEncounterDefinition(
    IReadOnlyList<decimal> TriggerHpPercents,
    decimal BaseReflectionRatio,
    decimal GuardianReflectionBonusRatio,
    decimal BrokenMirrorDamageTakenMultiplier,
    TimeSpan BrokenMirrorDuration)
{
    public static MirrorBarrierEncounterDefinition Default { get; } = new(
        [0.70m, 0.35m],
        BaseReflectionRatio: 0.50m,
        GuardianReflectionBonusRatio: 0.20m,
        BrokenMirrorDamageTakenMultiplier: 1.25m,
        BrokenMirrorDuration: TimeSpan.FromSeconds(8));

    public void Validate()
    {
        if (TriggerHpPercents is null || TriggerHpPercents.Count == 0)
            throw new ArgumentException("At least one HP threshold is required.", nameof(TriggerHpPercents));
        if (TriggerHpPercents.Any(value => value <= 0 || value >= 1))
            throw new ArgumentOutOfRangeException(nameof(TriggerHpPercents), "HP thresholds must be between 0 and 1.");
        if (TriggerHpPercents.Distinct().Count() != TriggerHpPercents.Count)
            throw new ArgumentException("HP thresholds must be unique.", nameof(TriggerHpPercents));
        if (BaseReflectionRatio < 0 || BaseReflectionRatio > 1)
            throw new ArgumentOutOfRangeException(nameof(BaseReflectionRatio));
        if (GuardianReflectionBonusRatio < 0
            || BaseReflectionRatio + GuardianReflectionBonusRatio > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(GuardianReflectionBonusRatio));
        }
        if (BrokenMirrorDamageTakenMultiplier < 1)
            throw new ArgumentOutOfRangeException(nameof(BrokenMirrorDamageTakenMultiplier));
        if (BrokenMirrorDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(BrokenMirrorDuration));
    }
}

public sealed record MirrorEncounterWave(
    decimal TriggerHpPercent,
    IReadOnlyList<MirrorEncounterAddRole> RequiredRoles);

public sealed record MirrorBarrierBroken(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    decimal DamageTakenMultiplier);

/// <summary>
/// Pure encounter-state runtime for the Mirror Castellan test boss.
/// It owns phase thresholds and linked-add state only; it never calculates
/// damage, threat, AI actions, rewards, or persistence.
/// </summary>
public sealed class MirrorBarrierEncounterRuntime
{
    private static readonly MirrorEncounterAddRole[] RequiredRoles =
    [
        MirrorEncounterAddRole.Guardian,
        MirrorEncounterAddRole.Priest,
        MirrorEncounterAddRole.Executioner
    ];

    private readonly MirrorBarrierEncounterDefinition _definition;
    private readonly Queue<decimal> _pendingThresholds;
    private readonly Dictionary<Guid, MirrorEncounterAddRole> _activeAdds = [];
    private readonly HashSet<MirrorEncounterAddRole> _registeredRoles = [];
    private DateTimeOffset? _brokenMirrorExpiresAtUtc;

    public MirrorBarrierEncounterRuntime(MirrorBarrierEncounterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();

        _definition = definition;
        _pendingThresholds = new Queue<decimal>(
            definition.TriggerHpPercents.OrderByDescending(value => value));
    }

    public bool BarrierActive { get; private set; }
    public int RemainingWaves => _pendingThresholds.Count;
    public int ActiveAddCount => _activeAdds.Count;

    public decimal ReflectionRatio => !BarrierActive
        ? 0
        : _definition.BaseReflectionRatio
            + (_activeAdds.Values.Contains(MirrorEncounterAddRole.Guardian)
                ? _definition.GuardianReflectionBonusRatio
                : 0);

    public bool IsBrokenMirrorActive(DateTimeOffset now) =>
        _brokenMirrorExpiresAtUtc is { } expiresAtUtc && now < expiresAtUtc;

    public decimal GetBossDamageTakenMultiplier(DateTimeOffset now) =>
        IsBrokenMirrorActive(now)
            ? _definition.BrokenMirrorDamageTakenMultiplier
            : 1m;

    public bool TryBeginNextWave(
        decimal currentHp,
        decimal maxHp,
        out MirrorEncounterWave? wave)
    {
        ValidateHp(currentHp, maxHp);
        wave = null;

        if (BarrierActive || _pendingThresholds.Count == 0)
            return false;

        decimal nextThreshold = _pendingThresholds.Peek();
        if (currentHp / maxHp > nextThreshold)
            return false;

        _pendingThresholds.Dequeue();
        BarrierActive = true;
        _activeAdds.Clear();
        _registeredRoles.Clear();
        _brokenMirrorExpiresAtUtc = null;
        wave = new MirrorEncounterWave(nextThreshold, RequiredRoles);
        return true;
    }

    public void RegisterAdd(Guid actorId, MirrorEncounterAddRole role)
    {
        if (!BarrierActive)
            throw new InvalidOperationException("Mirror adds can only be registered while the barrier is active.");
        if (actorId == Guid.Empty)
            throw new ArgumentException("Actor identifier cannot be empty.", nameof(actorId));
        if (!RequiredRoles.Contains(role))
            throw new ArgumentOutOfRangeException(nameof(role));
        if (_activeAdds.ContainsKey(actorId))
            throw new InvalidOperationException($"Mirror add '{actorId}' is already registered.");
        if (!_registeredRoles.Add(role))
            throw new InvalidOperationException($"Mirror add role '{role}' is already registered for this wave.");

        _activeAdds.Add(actorId, role);
    }

    public MirrorBarrierBroken? RegisterAddDeath(Guid actorId, DateTimeOffset now)
    {
        if (!BarrierActive || !_activeAdds.Remove(actorId))
            return null;

        if (_activeAdds.Count != 0 || _registeredRoles.Count != RequiredRoles.Length)
            return null;

        BarrierActive = false;
        _brokenMirrorExpiresAtUtc = now + _definition.BrokenMirrorDuration;
        return new MirrorBarrierBroken(
            now,
            _brokenMirrorExpiresAtUtc.Value,
            _definition.BrokenMirrorDamageTakenMultiplier);
    }

    public MirrorEncounterAddRole? GetRole(Guid actorId) =>
        _activeAdds.TryGetValue(actorId, out MirrorEncounterAddRole role)
            ? role
            : null;

    private static void ValidateHp(decimal currentHp, decimal maxHp)
    {
        if (maxHp <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHp));
        if (currentHp < 0 || currentHp > maxHp)
            throw new ArgumentOutOfRangeException(nameof(currentHp));
    }
}
