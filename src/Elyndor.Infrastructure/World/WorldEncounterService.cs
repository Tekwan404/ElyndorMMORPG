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

public sealed class WorldEncounterRegistry(TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly object _gate = new();
    private readonly Dictionary<Guid, PendingWorldEncounter> _byAccount = [];

    public PendingWorldEncounter Register(Guid accountId, string locationId, string monsterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(monsterId);

        PendingWorldEncounter encounter = new(
            Guid.CreateVersion7(),
            locationId,
            monsterId,
            timeProvider.GetUtcNow());
        lock (_gate)
        {
            _byAccount[accountId] = encounter;
        }

        return encounter;
    }

    public bool TryConsume(Guid accountId, Guid encounterId, out PendingWorldEncounter encounter)
    {
        lock (_gate)
        {
            encounter = null!;
            if (!_byAccount.TryGetValue(accountId, out PendingWorldEncounter? current))
                return false;

            if (timeProvider.GetUtcNow() - current.CreatedAtUtc > Lifetime)
            {
                _byAccount.Remove(accountId);
                return false;
            }

            if (current.EncounterId != encounterId)
                return false;

            _byAccount.Remove(accountId);
            encounter = current;
            return true;
        }
    }

    public void Clear(Guid accountId)
    {
        lock (_gate)
        {
            _byAccount.Remove(accountId);
        }
    }
}

public sealed class WorldEncounterService(
    GameDbContext dbContext,
    IContentSnapshotProvider contentProvider,
    IGameRandomFactory randomFactory,
    WorldEncounterRegistry registry)
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
            registry)
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
