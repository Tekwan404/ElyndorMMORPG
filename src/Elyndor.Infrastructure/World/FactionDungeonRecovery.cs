using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.World;

internal static class FactionDungeonRecovery
{
    private static readonly Action<ILogger, Guid, Guid, string?, Exception?> MissingCounterpart =
        LoggerMessage.Define<Guid, Guid, string?>(
            LogLevel.Warning,
            new EventId(2310, nameof(MissingCounterpart)),
            "Leaving incompatible dungeon run {RunId} for character {CharacterId}: counterpart {TargetDungeonId} missing.");

    private static readonly Action<ILogger, Guid, Guid, string, string, Exception?> MismatchedEncounters =
        LoggerMessage.Define<Guid, Guid, string, string>(
            LogLevel.Warning,
            new EventId(2311, nameof(MismatchedEncounters)),
            "Leaving incompatible dungeon run {RunId} for character {CharacterId}: encounters differ between {SourceDungeonId} and {TargetDungeonId}.");

    public static async Task<bool> RepairAsync(GameDbContext db, GameContentSnapshot content,
        Guid characterId, string raceId, DateTimeOffset now, CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        string[] forbiddenDungeons = content.Indexes.DungeonsById.Values
            .Where(dungeon => content.Indexes.LocationsById.TryGetValue(dungeon.EntryLocationId, out var location)
                && !WorldLocationAccess.CanEnter(location, raceId)).Select(dungeon => dungeon.Id).ToArray();
        Guid[] runIds = await db.DungeonRuns.AsNoTracking()
            .Where(run => run.State == DungeonRunState.Active && forbiddenDungeons.Contains(run.DungeonId)
                && run.Members.Any(member => member.CharacterId == characterId && member.State == DungeonRunMemberState.Active))
            .Select(run => run.Id).OrderBy(id => id).Take(1).ToArrayAsync(cancellationToken);
        if (runIds.Length == 0) return true;

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = db.Database.CurrentTransaction is null
                ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            foreach (Guid runId in runIds)
            {
                string lockKey = $"dungeon-run:{runId:N}";
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({lockKey}))", cancellationToken);
                var run = await db.DungeonRuns.Include(candidate => candidate.Members)
                    .Include(candidate => candidate.Encounters).SingleAsync(candidate => candidate.Id == runId, cancellationToken);
                if (run.State != DungeonRunState.Active) continue;
                if (content.Indexes.DungeonsById.TryGetValue(run.DungeonId, out var current)
                    && content.Indexes.LocationsById.TryGetValue(current.EntryLocationId, out var currentEntry)
                    && WorldLocationAccess.CanEnter(currentEntry, raceId)) continue;
                if (run.Encounters.Any(encounter => encounter.State == DungeonEncounterState.Active)) return false;
                string? targetId = content.Indexes.DungeonsById.TryGetValue(run.DungeonId, out var mapped)
                    ? mapped.FactionCounterpartId : null;
                if (targetId is null || !content.Indexes.DungeonsById.TryGetValue(targetId, out var target)
                    || !content.Indexes.LocationsById.TryGetValue(target.EntryLocationId, out var targetEntry)
                    || !WorldLocationAccess.CanEnter(targetEntry, raceId))
                {
                    if (logger is not null)
                        MissingCounterpart(logger, run.Id, characterId, targetId, null);
                    run.Members.Single(member => member.CharacterId == characterId).MarkLeft();
                    continue;
                }
                Guid[] memberIds = run.Members.Where(member => member.State == DungeonRunMemberState.Active)
                    .Select(member => member.CharacterId).ToArray();
                bool sameFaction = !await db.Characters.AnyAsync(
                    character => memberIds.Contains(character.Id) && character.RaceId != raceId, cancellationToken);
                if (!sameFaction)
                {
                    // The original run remains intact for its other members. The relocated
                    // character must form a same-faction party for a new homeland run.
                    run.Members.Single(member => member.CharacterId == characterId).MarkLeft();
                    continue;
                }

                var source = content.Indexes.DungeonsById[run.DungeonId];
                if (!source.Encounters.Select(encounter => (encounter.Id, encounter.MonsterId, encounter.CheckpointId))
                    .SequenceEqual(target.Encounters.Select(encounter => (encounter.Id, encounter.MonsterId, encounter.CheckpointId))))
                {
                    if (logger is not null)
                        MismatchedEncounters(logger, run.Id, characterId, source.Id, target.Id, null);
                    run.Members.Single(member => member.CharacterId == characterId).MarkLeft();
                    continue;
                }
                run.MoveToFactionDungeon(target.Id);
                var locations = await db.CharacterLocations.Where(location => memberIds.Contains(location.CharacterId)
                    && location.LocationId == source.EntryLocationId).ToArrayAsync(cancellationToken);
                foreach (var location in locations)
                    location.Relocate(target.EntryLocationId, now < location.UpdatedAtUtc ? location.UpdatedAtUtc : now);
            }
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }
}
