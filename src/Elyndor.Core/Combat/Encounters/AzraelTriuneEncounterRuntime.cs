namespace Elyndor.Core.Combat.Encounters;

public enum AzraelCloneRole
{
    Fire,
    Frost,
    Void
}

public sealed record AzraelTriuneEncounterDefinition(
    decimal SplitTriggerHpPercent,
    TimeSpan ReviveWindow,
    decimal ReviveHpPercent,
    decimal FinalDamageBonusPerRevive)
{
    public static AzraelTriuneEncounterDefinition Default { get; } = new(
        SplitTriggerHpPercent: 0.65m,
        ReviveWindow: TimeSpan.FromSeconds(10),
        ReviveHpPercent: 0.35m,
        FinalDamageBonusPerRevive: 0.05m);

    public void Validate()
    {
        if (SplitTriggerHpPercent <= 0 || SplitTriggerHpPercent >= 1)
            throw new ArgumentOutOfRangeException(nameof(SplitTriggerHpPercent));
        if (ReviveWindow <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ReviveWindow));
        if (ReviveHpPercent <= 0 || ReviveHpPercent >= 1)
            throw new ArgumentOutOfRangeException(nameof(ReviveHpPercent));
        if (FinalDamageBonusPerRevive < 0)
            throw new ArgumentOutOfRangeException(nameof(FinalDamageBonusPerRevive));
    }
}

public sealed record AzraelCloneDeathResult(
    bool Accepted,
    bool WindowStarted,
    bool SplitCompleted,
    DateTimeOffset? WindowExpiresAtUtc);

public sealed class AzraelTriuneEncounterRuntime
{
    private readonly AzraelTriuneEncounterDefinition _definition;
    private readonly Dictionary<Guid, AzraelCloneRole> _clones = [];
    private readonly HashSet<Guid> _deadClones = [];

    public AzraelTriuneEncounterRuntime(AzraelTriuneEncounterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();
        _definition = definition;
    }

    public bool SplitTriggered { get; private set; }
    public bool SplitCompleted { get; private set; }
    public DateTimeOffset? WindowExpiresAtUtc { get; private set; }
    public decimal ReviveHpPercent => _definition.ReviveHpPercent;
    public decimal FinalDamageBonusPerRevive => _definition.FinalDamageBonusPerRevive;
    public TimeSpan ReviveWindow => _definition.ReviveWindow;
    public IReadOnlyDictionary<Guid, AzraelCloneRole> Clones => _clones;
    public IReadOnlySet<Guid> DeadClones => _deadClones;

    public bool TryTriggerSplit(decimal currentHp, decimal maxHp)
    {
        ValidateHp(currentHp, maxHp);
        if (SplitTriggered || currentHp / maxHp > _definition.SplitTriggerHpPercent)
            return false;

        SplitTriggered = true;
        return true;
    }

    public void RegisterClone(Guid actorId, AzraelCloneRole role)
    {
        if (!SplitTriggered)
            throw new InvalidOperationException("Azrael split has not started.");
        if (actorId == Guid.Empty)
            throw new ArgumentException("Clone actor identifier is required.", nameof(actorId));
        if (_clones.ContainsKey(actorId) || _clones.Values.Contains(role))
            throw new InvalidOperationException("Azrael clone actor or role is already registered.");
        _clones.Add(actorId, role);
    }

    public AzraelCloneDeathResult RegisterCloneDeath(Guid actorId, DateTimeOffset now)
    {
        if (SplitCompleted || !_clones.ContainsKey(actorId) || !_deadClones.Add(actorId))
            return new AzraelCloneDeathResult(false, false, SplitCompleted, WindowExpiresAtUtc);

        if (_deadClones.Count == _clones.Count && _clones.Count > 0)
        {
            SplitCompleted = true;
            WindowExpiresAtUtc = null;
            return new AzraelCloneDeathResult(true, false, true, null);
        }

        bool windowStarted = WindowExpiresAtUtc is null;
        if (windowStarted)
            WindowExpiresAtUtc = now + _definition.ReviveWindow;
        return new AzraelCloneDeathResult(true, windowStarted, false, WindowExpiresAtUtc);
    }

    public IReadOnlyList<Guid> ExpireReviveWindow(DateTimeOffset now)
    {
        if (SplitCompleted
            || WindowExpiresAtUtc is not { } expiresAt
            || now < expiresAt
            || _deadClones.Count == 0)
        {
            return [];
        }

        Guid[] revived = _deadClones.ToArray();
        _deadClones.Clear();
        WindowExpiresAtUtc = null;
        return revived;
    }

    private static void ValidateHp(decimal currentHp, decimal maxHp)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHp);
        ArgumentOutOfRangeException.ThrowIfNegative(currentHp);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(currentHp, maxHp);
    }
}
