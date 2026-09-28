using System.Data;
using Elyndor.Core.Characters;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elyndor.Infrastructure.Pvp;

public sealed record ArenaQueueStatus(
    Guid CharacterId,
    bool IsQueued,
    ArenaQueueMode? QueueMode,
    DateTimeOffset? JoinedAtUtc,
    Guid? ActiveMatchId,
    ArenaQueueMode? ActiveMatchMode);

public sealed record ArenaQueueMutationResult(
    bool Succeeded,
    string? ErrorCode,
    ArenaQueueStatus Status);

public sealed record ArenaMatchCreated(
    Guid MatchId,
    Guid CharacterAId,
    Guid CharacterBId,
    ArenaQueueMode Mode,
    DateTimeOffset StartedAtUtc);

public sealed class ArenaQueueService(GameDbContext db, TimeProvider timeProvider)
{
    public async Task<ArenaQueueStatus> GetStatusAsync(Guid accountId, CancellationToken cancellationToken)
    {
        Character? character = await db.Characters.AsNoTracking()
            .SingleOrDefaultAsync(x => x.AccountId == accountId, cancellationToken);
        if (character is null) return EmptyStatus();

        ArenaMatch? active = await db.ArenaMatches.AsNoTracking()
            .Where(x => x.Outcome == ArenaMatchOutcome.Active
                && (x.CharacterAId == character.Id || x.CharacterBId == character.Id))
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        ArenaQueueEntry? queued = active is null
            ? await db.ArenaQueueEntries.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CharacterId == character.Id, cancellationToken)
            : null;
        return BuildStatus(character.Id, queued, active);
    }

    public Task<ArenaQueueMutationResult> JoinAsync(
        Guid accountId,
        ArenaQueueMode mode,
        CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (!Enum.IsDefined(mode))
                return new ArenaQueueMutationResult(false, "arena_queue_mode_invalid", EmptyStatus());

            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            Character? character = await db.Characters.FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (character is null)
                return new ArenaQueueMutationResult(false, "character_not_found", EmptyStatus());

            ArenaMatch? active = await FindActiveMatchAsync(character.Id, cancellationToken);
            if (active is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new ArenaQueueMutationResult(false, "arena_match_active",
                    BuildStatus(character.Id, null, active));
            }

            ArenaQueueEntry? existing = await db.ArenaQueueEntries
                .SingleOrDefaultAsync(x => x.CharacterId == character.Id, cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                bool sameMode = existing.Mode == mode;
                return new ArenaQueueMutationResult(sameMode,
                    sameMode ? null : "arena_already_queued",
                    BuildStatus(character.Id, existing, null));
            }

            int rating = await db.ArenaStandings.AsNoTracking()
                .Where(x => x.CharacterId == character.Id && x.SeasonId == ArenaSeason.CurrentId)
                .Select(x => (int?)x.Rating)
                .SingleOrDefaultAsync(cancellationToken)
                ?? ArenaProgressionRules.InitialRating;
            var entry = new ArenaQueueEntry(character.Id, mode, character.Level, rating,
                timeProvider.GetUtcNow());
            db.ArenaQueueEntries.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ArenaQueueMutationResult(true, null,
                BuildStatus(character.Id, entry, null));
        });

    public Task<ArenaQueueMutationResult> LeaveAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            Character? character = await db.Characters.FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"AccountId\" = {accountId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (character is null)
                return new ArenaQueueMutationResult(false, "character_not_found", EmptyStatus());

            ArenaMatch? active = await FindActiveMatchAsync(character.Id, cancellationToken);
            ArenaQueueEntry? entry = await db.ArenaQueueEntries
                .SingleOrDefaultAsync(x => x.CharacterId == character.Id, cancellationToken);
            if (entry is not null)
            {
                db.ArenaQueueEntries.Remove(entry);
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new ArenaQueueMutationResult(true, null,
                BuildStatus(character.Id, null, active));
        });

    private Task<ArenaMatch?> FindActiveMatchAsync(Guid characterId, CancellationToken cancellationToken) =>
        db.ArenaMatches
            .Where(x => x.Outcome == ArenaMatchOutcome.Active
                && (x.CharacterAId == characterId || x.CharacterBId == characterId))
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private static ArenaQueueStatus BuildStatus(
        Guid characterId,
        ArenaQueueEntry? queue,
        ArenaMatch? match) =>
        new(characterId, queue is not null, queue?.Mode, queue?.JoinedAtUtc,
            match?.Id, match?.Mode);

    private static ArenaQueueStatus EmptyStatus() =>
        new(Guid.Empty, false, null, null, null, null);
}

public sealed class ArenaMatchmakingService(GameDbContext db, TimeProvider timeProvider)
{
    public Task<ArenaMatchCreated?> TryCreateMatchAsync(
        ArenaQueueMode mode,
        CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            string modeText = mode.ToString();
            ArenaQueueEntry[] waiting = await db.ArenaQueueEntries.FromSqlInterpolated(
                    $"SELECT * FROM game.arena_queue_entries WHERE \"Mode\" = {modeText} ORDER BY \"JoinedAtUtc\" FOR UPDATE SKIP LOCKED LIMIT 32")
                .ToArrayAsync(cancellationToken);
            if (waiting.Length < 2)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            ArenaQueueEntry? first = null;
            ArenaQueueEntry? second = null;
            foreach (ArenaQueueEntry candidate in waiting.OrderBy(x => x.JoinedAtUtc))
            {
                ArenaQueueEntry? opponent = waiting
                    .Where(x => x.CharacterId != candidate.CharacterId
                        && ArenaMatchRules.CanPair(candidate.CharacterId, candidate.Level,
                            x.CharacterId, x.Level))
                    .OrderBy(x => mode == ArenaQueueMode.Ranked
                        ? Math.Abs(x.RatingSnapshot - candidate.RatingSnapshot)
                        : 0)
                    .ThenBy(x => x.JoinedAtUtc)
                    .FirstOrDefault();
                if (opponent is null) continue;
                first = candidate;
                second = opponent;
                break;
            }

            if (first is null || second is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            Guid[] characterIds = [first.CharacterId, second.CharacterId];
            Character[] lockedCharacters = await db.Characters.FromSqlInterpolated(
                    $"SELECT * FROM game.characters WHERE \"Id\" = ANY({characterIds}) ORDER BY \"Id\" FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            if (lockedCharacters.Length != 2)
            {
                db.ArenaQueueEntries.RemoveRange(first, second);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            ArenaMatch[] activeMatches = await db.ArenaMatches.AsNoTracking()
                .Where(x => x.Outcome == ArenaMatchOutcome.Active
                    && (characterIds.Contains(x.CharacterAId) || characterIds.Contains(x.CharacterBId)))
                .ToArrayAsync(cancellationToken);
            Guid[] busyCharacters = activeMatches
                .SelectMany(x => new[] { x.CharacterAId, x.CharacterBId })
                .Where(characterIds.Contains)
                .Distinct()
                .ToArray();
            if (busyCharacters.Length > 0)
            {
                ArenaQueueEntry[] stale = waiting
                    .Where(x => busyCharacters.Contains(x.CharacterId))
                    .ToArray();
                db.ArenaQueueEntries.RemoveRange(stale);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            var match = new ArenaMatch(Guid.NewGuid(), first.CharacterId, second.CharacterId,
                now, ArenaSeason.CurrentId, mode);
            db.ArenaMatches.Add(match);
            db.ArenaQueueEntries.RemoveRange(first, second);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ArenaMatchCreated(match.Id, match.CharacterAId, match.CharacterBId,
                match.Mode, match.StartedAtUtc);
        });
}

public sealed class ArenaMatchmakingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ArenaMatchmakingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                var matchmaking = scope.ServiceProvider.GetRequiredService<ArenaMatchmakingService>();
                foreach (ArenaQueueMode mode in Enum.GetValues<ArenaQueueMode>())
                {
                    while (await matchmaking.TryCreateMatchAsync(mode, stoppingToken) is not null)
                    {
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Arena matchmaking iteration failed.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
        }
    }
}