using Elyndor.Core.Characters;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Professions;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public static class WorldEncounterErrorCodes
{
    public const string CharacterNotFound = "world_character_not_found";
    public const string LocationUnavailable = "world_encounter_location_unavailable";
    public const string EncounterUnavailable = "world_encounter_unavailable";
    public const string Travelling = "world_encounter_travelling";
    public const string InvalidSelection = "world_encounter_invalid_selection";
    public const string TargetUnavailable = "world_target_unavailable";
}

public sealed record WorldSceneSnapshot(
    Guid CharacterId,
    LocationDefinition Location,
    GameContentSnapshot Content,
    DateTimeOffset ServerTimeUtc,
    IReadOnlySet<string>? CompletedQuestIds = null,
    IReadOnlyDictionary<string, DateTimeOffset>? EliteRespawns = null);

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
        CancellationToken cancellationToken) =>
        await PrepareAsync(accountId, null, null, cancellationToken);

    public async Task<(WorldEncounterSnapshot? Encounter, string? ErrorCode)> SelectAsync(
        Guid accountId,
        string locationId,
        string monsterId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(monsterId))
            return (null, WorldEncounterErrorCodes.InvalidSelection);
        return await PrepareAsync(accountId, locationId, monsterId, cancellationToken);
    }

    public async Task<(WorldSceneSnapshot? Scene, string? ErrorCode)> GetSceneAsync(
        Guid accountId, CancellationToken cancellationToken)
    {
        GameContentSnapshot content = contentProvider.GetCurrent();

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
            return (null, WorldEncounterErrorCodes.Travelling);
        }

        CharacterLocation? characterLocation = await dbContext.CharacterLocations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.CharacterId == character.Id, cancellationToken);
        if (characterLocation is null)
            return (null, WorldEncounterErrorCodes.LocationUnavailable);

        if (!content.Indexes.LocationsById.TryGetValue(characterLocation.LocationId, out var location))
            return (null, WorldEncounterErrorCodes.LocationUnavailable);

        var localQuestIds = (content.Package.Quests ?? []).Where(quest => quest.OfferLocationId == location.Id)
            .Select(quest => quest.Id).ToArray();
        var completedQuests = await dbContext.CharacterQuestStates.AsNoTracking()
            .Where(quest => quest.CharacterId == character.Id && quest.Status == Elyndor.Core.Quests.QuestStateStatuses.Completed
                && localQuestIds.Contains(quest.QuestId))
            .Select(quest => quest.QuestId)
            .Union(dbContext.CharacterContractCompletions.AsNoTracking()
                .Where(quest => quest.CharacterId == character.Id && localQuestIds.Contains(quest.ContractId))
                .Select(quest => quest.ContractId))
            .Union(dbContext.QuestRewardGrants.AsNoTracking()
                .Where(quest => quest.CharacterId == character.Id && localQuestIds.Contains(quest.QuestId))
                .Select(quest => quest.QuestId))
            .ToArrayAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var respawns = await GetEliteRespawnsAsync(location, now, cancellationToken);
        return (new WorldSceneSnapshot(character.Id, location, content, now, completedQuests.ToHashSet(StringComparer.Ordinal), respawns), null);
    }

    public async Task<bool> IsTargetAvailableAsync(string locationId, string monsterId, CancellationToken cancellationToken)
    {
        var content = contentProvider.GetCurrent();
        if (!content.Indexes.LocationsById.TryGetValue(locationId, out var location)) return false;
        var encounter = location.Encounters?.SingleOrDefault(item => item.MonsterId == monsterId);
        if (encounter is null) return false;
        var now = timeProvider.GetUtcNow();
        return LocationEncounterAvailability.IsAvailable(encounter, now)
            && (encounter.RespawnSeconds == 0
                || !(await GetEliteRespawnsAsync(location, now, cancellationToken)).ContainsKey(monsterId));
    }

    private async Task<IReadOnlyDictionary<string, DateTimeOffset>> GetEliteRespawnsAsync(
        LocationDefinition location, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var encounters = (location.Encounters ?? []).Where(item => item.RespawnSeconds > 0)
            .ToDictionary(item => item.MonsterId, StringComparer.Ordinal);
        if (encounters.Count == 0) return new Dictionary<string, DateTimeOffset>();
        var ids = encounters.Keys.ToArray();
        var cutoff = now.AddSeconds(-encounters.Values.Max(item => item.RespawnSeconds));
        var kills = await dbContext.CombatRewardGrants.AsNoTracking()
            .Where(grant => ids.Contains(grant.PrimaryMonsterId) && grant.GrantedAtUtc > cutoff)
            .GroupBy(grant => grant.PrimaryMonsterId)
            .Select(group => new { MonsterId = group.Key, KilledAt = group.Max(grant => grant.GrantedAtUtc) })
            .ToArrayAsync(cancellationToken);
        return kills.Select(kill => new { kill.MonsterId, RespawnAt = kill.KilledAt.AddSeconds(encounters[kill.MonsterId].RespawnSeconds) })
            .Where(kill => kill.RespawnAt > now).ToDictionary(kill => kill.MonsterId, kill => kill.RespawnAt, StringComparer.Ordinal);
    }

    private async Task<(WorldEncounterSnapshot? Encounter, string? ErrorCode)> PrepareAsync(
        Guid accountId, string? expectedLocationId, string? monsterId, CancellationToken cancellationToken)
    {
        (WorldSceneSnapshot? scene, string? error) = await GetSceneAsync(accountId, cancellationToken);
        if (scene is null)
            return (null, error);
        LocationDefinition location = scene.Location;
        GameContentIndexes indexes = scene.Content.Indexes;
        if (expectedLocationId is not null
            && (!string.Equals(expectedLocationId, location.Id, StringComparison.Ordinal)
                || location.DangerLevel == "SAFE"
                || indexes.DungeonsById.Values.Any(dungeon =>
                    dungeon.Id == location.Id || dungeon.EntryLocationId == location.Id)))
            return (null, WorldEncounterErrorCodes.InvalidSelection);
        if (monsterId is not null && !(location.Encounters ?? []).Any(entry => entry.MonsterId == monsterId))
            return (null, WorldEncounterErrorCodes.InvalidSelection);

        LocationEncounterDefinition[] encounters = (location.Encounters ?? [])
            .Where(encounter => LocationEncounterAvailability.IsAvailable(encounter, scene.ServerTimeUtc)
                && scene.EliteRespawns?.ContainsKey(encounter.MonsterId) != true)
            .ToArray();
        if (encounters.Length == 0)
        {
            return (null, monsterId is null
                ? WorldEncounterErrorCodes.EncounterUnavailable
                : WorldEncounterErrorCodes.TargetUnavailable);
        }

        LocationEncounterDefinition? selected = monsterId is null
            ? WorldEncounterSelector.Select(encounters, randomFactory.Create().NextUnit())
            : encounters.FirstOrDefault(encounter => encounter.MonsterId == monsterId);
        if (selected is null)
            return (null, WorldEncounterErrorCodes.TargetUnavailable);
        MonsterDefinition? monster = indexes.MonstersById.GetValueOrDefault(selected.MonsterId);
        if (monster is null
            || string.IsNullOrWhiteSpace(monster.DisplayName)
            || string.IsNullOrWhiteSpace(monster.ArtId))
        {
            return (null, WorldEncounterErrorCodes.EncounterUnavailable);
        }

        // Only a valid new encounter abandons the previous combat aftermath.
        await ProfessionCorpsePersistence.DiscardPendingAsync(dbContext, scene.CharacterId, cancellationToken);
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
