using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Content;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public static class WorldEncounterErrorCodes
{
    public const string CharacterNotFound = "world_character_not_found";
    public const string LocationUnavailable = "world_encounter_location_unavailable";
    public const string EncounterUnavailable = "world_encounter_unavailable";
    public const string Travelling = "world_encounter_travelling";
}

public sealed record WorldEncounterSnapshot(
    Guid EncounterId,
    string LocationId,
    string MonsterId,
    string Name,
    int Level,
    string Rank,
    string Description,
    string ArtId);

public sealed record PendingWorldEncounter(
    Guid EncounterId,
    string LocationId,
    string MonsterId,
    DateTimeOffset CreatedAtUtc);

public sealed class WorldEncounterRegistry : IDisposable
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, PendingWorldEncounter> _byAccount = [];
    private readonly ITimer _purgeTimer;
    private bool _disposed;

    public WorldEncounterRegistry(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _purgeTimer = _timeProvider.CreateTimer(
            _ => PurgeExpired(),
            null,
            PurgeInterval,
            PurgeInterval);
    }

    public PendingWorldEncounter Register(Guid accountId, string locationId, string monsterId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(monsterId);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        PendingWorldEncounter encounter = new(
            Guid.CreateVersion7(),
            locationId,
            monsterId,
            now);
        lock (_gate)
        {
            PurgeExpiredLocked(now);
            _byAccount[accountId] = encounter;
        }

        return encounter;
    }

    public bool TryConsume(Guid accountId, Guid encounterId, out PendingWorldEncounter encounter)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            PurgeExpiredLocked(_timeProvider.GetUtcNow());
            encounter = null!;
            if (!_byAccount.TryGetValue(accountId, out PendingWorldEncounter? current))
                return false;

            if (current.EncounterId != encounterId)
                return false;

            _byAccount.Remove(accountId);
            encounter = current;
            return true;
        }
    }

    public void Clear(Guid accountId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            _byAccount.Remove(accountId);
        }
    }

    public int PurgeExpired()
    {
        if (_disposed)
            return 0;

        lock (_gate)
        {
            return PurgeExpiredLocked(_timeProvider.GetUtcNow());
        }
    }

    private int PurgeExpiredLocked(DateTimeOffset now)
    {
        Guid[] expiredAccountIds = _byAccount
            .Where(pair => now - pair.Value.CreatedAtUtc > Lifetime)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (Guid accountId in expiredAccountIds)
            _byAccount.Remove(accountId);

        return expiredAccountIds.Length;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _purgeTimer.Dispose();
        lock (_gate)
        {
            _byAccount.Clear();
        }
    }
}

public sealed class WorldEncounterService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    IGameRandomFactory randomFactory,
    WorldEncounterRegistry registry,
    TimeProvider timeProvider)
{
    public WorldEncounterService(
        GameDbContext dbContext,
        WorldMap worldMap,
        GameContentPackage content,
        IGameRandomFactory randomFactory,
        WorldEncounterRegistry registry)
        : this(
            dbContext,
            new StaticContentSnapshotProvider(content),
            randomFactory,
            registry,
            TimeProvider.System)
    {
    }

    public async Task<(WorldEncounterSnapshot? Encounter, string? ErrorCode)> ExploreAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        GameContentSnapshot contentSnapshot = contentProvider.GetCurrent();
        WorldMap worldMap = contentSnapshot.WorldMap;
        GameContentIndexes indexes = contentSnapshot.Indexes;

        Character? character = await dbContext.Characters
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.AccountId == accountId, cancellationToken);
        if (character is null)
            return (null, WorldEncounterErrorCodes.CharacterNotFound);

        if (await TravelPersistence.IsTravellingAsync(
                dbContext,
                character.Id,
                timeProvider.GetUtcNow(),
                cancellationToken))
        {
            registry.Clear(accountId);
            return (null, WorldEncounterErrorCodes.Travelling);
        }

        CharacterLocation? characterLocation = await dbContext.CharacterLocations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
        if (characterLocation is null)
            return (null, WorldEncounterErrorCodes.LocationUnavailable);

        LocationDefinition location;
        try
        {
            location = worldMap.GetRequired(characterLocation.LocationId);
        }
        catch (KeyNotFoundException)
        {
            return (null, WorldEncounterErrorCodes.LocationUnavailable);
        }

        IReadOnlyList<LocationEncounterDefinition> encounters = location.Encounters ?? [];
        if (encounters.Count == 0)
        {
            registry.Clear(accountId);
            return (null, WorldEncounterErrorCodes.EncounterUnavailable);
        }

        LocationEncounterDefinition selected = WorldEncounterSelector.Select(
            encounters,
            randomFactory.Create().NextUnit());
        MonsterDefinition? monster = indexes.MonstersById.GetValueOrDefault(selected.MonsterId);
        if (monster is null
            || string.IsNullOrWhiteSpace(monster.DisplayName)
            || string.IsNullOrWhiteSpace(monster.ArtId))
        {
            registry.Clear(accountId);
            return (null, WorldEncounterErrorCodes.EncounterUnavailable);
        }

        PendingWorldEncounter pending = registry.Register(accountId, location.Id, monster.Id);
        return (new WorldEncounterSnapshot(
            pending.EncounterId,
            location.Id,
            monster.Id,
            monster.DisplayName,
            monster.Level,
            monster.Rank.ToString(),
            monster.Description,
            monster.ArtId), null);
    }
}
