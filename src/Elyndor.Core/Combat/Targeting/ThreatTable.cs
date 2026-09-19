namespace Elyndor.Core.Combat.Targeting;

public sealed class ThreatTable
{
    private readonly Dictionary<Guid, decimal> _threatByActor = [];
    private readonly Dictionary<Guid, int> _suppressedNextAddsByActor = [];

    public IReadOnlyDictionary<Guid, decimal> Snapshot =>
        new Dictionary<Guid, decimal>(_threatByActor);

    public decimal GetThreat(Guid actorId) =>
        _threatByActor.GetValueOrDefault(actorId);

    public decimal AddThreat(Guid actorId, decimal amount, decimal multiplier = 1)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Threat actor is required.", nameof(actorId));
        ArgumentOutOfRangeException.ThrowIfNegative(multiplier);

        if (TryConsumeSuppressedAdd(actorId))
            return GetThreat(actorId);

        decimal updated = Math.Max(0, GetThreat(actorId) + amount * multiplier);
        _threatByActor[actorId] = updated;
        return updated;
    }

    public decimal AddExplicitThreat(Guid actorId, decimal amount)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Threat actor is required.", nameof(actorId));
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        decimal updated = GetThreat(actorId) + amount;
        _threatByActor[actorId] = updated;
        return updated;
    }

    public decimal DropThreatPercent(Guid actorId, decimal percent)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Threat actor is required.", nameof(actorId));
        ValidatePercent(percent);

        if (!_threatByActor.TryGetValue(actorId, out decimal currentThreat))
            return 0;

        decimal updated = currentThreat * (1 - percent / 100m);
        _threatByActor[actorId] = updated;
        return updated;
    }

    public void DropAllThreatPercent(decimal percent)
    {
        ValidatePercent(percent);
        foreach (Guid actorId in _threatByActor.Keys.ToArray())
            _threatByActor[actorId] *= 1 - percent / 100m;
    }

    internal void SuppressNextAutomaticAdd(Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Threat actor is required.", nameof(actorId));

        _suppressedNextAddsByActor[actorId] =
            _suppressedNextAddsByActor.GetValueOrDefault(actorId) + 1;
    }

    public Guid? SelectTarget(IReadOnlyList<Guid> candidateActorIds)
    {
        ArgumentNullException.ThrowIfNull(candidateActorIds);

        return candidateActorIds
            .Where(actorId => actorId != Guid.Empty)
            .OrderByDescending(GetThreat)
            .FirstOrDefault(actorId => GetThreat(actorId) > 0);
    }

    public void Remove(Guid actorId)
    {
        _threatByActor.Remove(actorId);
        _suppressedNextAddsByActor.Remove(actorId);
    }

    public void Clear()
    {
        _threatByActor.Clear();
        _suppressedNextAddsByActor.Clear();
    }

    private static void ValidatePercent(decimal percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        if (percent > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percent),
                percent,
                "Threat percent cannot exceed 100.");
        }
    }

    private bool TryConsumeSuppressedAdd(Guid actorId)
    {
        if (!_suppressedNextAddsByActor.TryGetValue(actorId, out int remaining)
            || remaining <= 0)
        {
            return false;
        }

        if (remaining == 1)
            _suppressedNextAddsByActor.Remove(actorId);
        else
            _suppressedNextAddsByActor[actorId] = remaining - 1;

        return true;
    }
}
