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
