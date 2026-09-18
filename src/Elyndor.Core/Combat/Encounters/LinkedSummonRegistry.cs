namespace Elyndor.Core.Combat.Encounters;

public sealed record LinkedSummonRegistration(
    Guid ActorId,
    Guid OwnerActorId,
    string MonsterId,
    DateTimeOffset SpawnedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    bool LinkToOwner,
    bool DespawnOnOwnerDeath,
    bool NoReward);

public sealed class LinkedSummonRegistry
{
    private readonly Dictionary<Guid, LinkedSummonRegistration> _active = [];

    public IReadOnlyCollection<LinkedSummonRegistration> Active => _active.Values;

    public int CountActive(string monsterId) =>
        _active.Values.Count(item => string.Equals(
            item.MonsterId,
            monsterId,
            StringComparison.Ordinal));

    public LinkedSummonRegistration Register(
        Guid actorId,
        Guid ownerActorId,
        SummonDefinition definition,
        DateTimeOffset now)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Summon actor id is required.", nameof(actorId));
        if (ownerActorId == Guid.Empty)
            throw new ArgumentException("Summon owner actor id is required.", nameof(ownerActorId));
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.MonsterId)
            || definition.Count <= 0
            || definition.MaxActive < 0
            || definition.Lifetime is { } lifetime && lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentException("Summon definition is invalid.", nameof(definition));
        }
        if (_active.ContainsKey(actorId))
            throw new InvalidOperationException($"Summon actor '{actorId}' is already registered.");
        if (definition.MaxActive > 0
            && CountActive(definition.MonsterId) >= definition.MaxActive)
        {
            throw new InvalidOperationException(
                $"Summon cap for '{definition.MonsterId}' is already reached.");
        }

        LinkedSummonRegistration registration = new(
            actorId,
            ownerActorId,
            definition.MonsterId,
            now,
            definition.Lifetime is { } lifetimeValue ? now + lifetimeValue : null,
            definition.LinkToCaster,
            definition.DespawnOnBossDeath,
            definition.NoReward);
        _active.Add(actorId, registration);
        return registration;
    }

    public bool TryGet(Guid actorId, out LinkedSummonRegistration? registration) =>
        _active.TryGetValue(actorId, out registration);

    public bool TryRemove(Guid actorId, out LinkedSummonRegistration? removed)
    {
        if (!_active.Remove(actorId, out LinkedSummonRegistration? registration))
        {
            removed = null;
            return false;
        }

        removed = registration;
        return true;
    }

    public IReadOnlyList<LinkedSummonRegistration> CollectExpired(DateTimeOffset now) =>
        RemoveWhere(item => item.ExpiresAtUtc is { } expiresAt && expiresAt <= now);

    public IReadOnlyList<LinkedSummonRegistration> CollectForOwnerDeath(Guid ownerActorId)
    {
        if (ownerActorId == Guid.Empty)
            return [];

        return RemoveWhere(item =>
            item.OwnerActorId == ownerActorId && item.DespawnOnOwnerDeath);
    }

    public bool IsRewardEligible(Guid actorId) =>
        !_active.TryGetValue(actorId, out LinkedSummonRegistration? summon)
        || !summon.NoReward;

    public void Clear() => _active.Clear();

    private LinkedSummonRegistration[] RemoveWhere(
        Func<LinkedSummonRegistration, bool> predicate)
    {
        LinkedSummonRegistration[] removed = _active.Values
            .Where(predicate)
            .OrderBy(item => item.SpawnedAtUtc)
            .ThenBy(item => item.ActorId)
            .ToArray();
        foreach (LinkedSummonRegistration item in removed)
            _active.Remove(item.ActorId);
        return removed;
    }
}
