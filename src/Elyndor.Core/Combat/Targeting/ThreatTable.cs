namespace Elyndor.Core.Combat.Targeting;

public sealed class ThreatTable
{
    private readonly Dictionary<Guid, decimal> _threatByActor = [];

    public IReadOnlyDictionary<Guid, decimal> Snapshot =>
        new Dictionary<Guid, decimal>(_threatByActor);

    public decimal GetThreat(Guid actorId) =>
        _threatByActor.GetValueOrDefault(actorId);

    public decimal AddThreat(Guid actorId, decimal amount, decimal multiplier = 1)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Threat actor is required.", nameof(actorId));
        ArgumentOutOfRangeException.ThrowIfNegative(multiplier);

        decimal updated = Math.Max(0, GetThreat(actorId) + amount * multiplier);
        _threatByActor[actorId] = updated;
        return updated;
    }

    public Guid? SelectTarget(IReadOnlyList<Guid> candidateActorIds)
    {
        ArgumentNullException.ThrowIfNull(candidateActorIds);

        return candidateActorIds
            .Where(actorId => actorId != Guid.Empty)
            .OrderByDescending(GetThreat)
            .FirstOrDefault(actorId => GetThreat(actorId) > 0);
    }

    public void Remove(Guid actorId) => _threatByActor.Remove(actorId);

    public void Clear() => _threatByActor.Clear();
}
