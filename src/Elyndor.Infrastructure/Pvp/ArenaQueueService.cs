using System.Data;
using Elyndor.Core.Characters;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

public sealed class ArenaQueueService(
    GameDbContext db,
    TimeProvider timeProvider,
    ArenaMatchmakingSignal? matchmakingSignal = null)
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
            matchmakingSignal?.Pulse();
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

    /// <summary>Removes queue entries whose owner has no live arena connection past the grace period.</summary>
    public async Task<int> PurgeOfflineAsync(ArenaPresenceTracker presence, TimeSpan grace,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset joinedCutoff = now - grace;
        var entries = await (from entry in db.ArenaQueueEntries
                             join character in db.Characters.AsNoTracking() on entry.CharacterId equals character.Id
                             where entry.JoinedAtUtc <= joinedCutoff
                             orderby entry.JoinedAtUtc
                             select new { Entry = entry, character.AccountId })
            .Take(256)
            .ToListAsync(cancellationToken);
        var stale = entries.Where(x =>
        {
            if (presence.IsConnected(x.AccountId)) return false;
            DateTimeOffset since = presence.LastSeenUtc(x.AccountId) is { } seen && seen > x.Entry.JoinedAtUtc
                ? seen : x.Entry.JoinedAtUtc;
            return now - since >= grace;
        }).Select(x => x.Entry).ToArray();
        if (stale.Length == 0) return 0;
        db.ArenaQueueEntries.RemoveRange(stale);
        await db.SaveChangesAsync(cancellationToken);
        return stale.Length;
    }

    /// <summary>Puts a character back into the queue (e.g. after the opponent failed to start).</summary>
    public async Task RequeueAsync(Guid characterId, ArenaQueueMode mode, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        Character? character = await db.Characters.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == characterId, cancellationToken);
        if (character is null
            || await db.ArenaQueueEntries.AnyAsync(x => x.CharacterId == characterId, cancellationToken)
            || await db.ArenaMatches.AnyAsync(x => x.Outcome == ArenaMatchOutcome.Active
                && (x.CharacterAId == characterId || x.CharacterBId == characterId), cancellationToken))
            return;
        int rating = await db.ArenaStandings.AsNoTracking()
            .Where(x => x.CharacterId == characterId && x.SeasonId == ArenaSeason.CurrentId)
            .Select(x => (int?)x.Rating).SingleOrDefaultAsync(cancellationToken)
            ?? ArenaProgressionRules.InitialRating;
        db.ArenaQueueEntries.Add(new ArenaQueueEntry(characterId, mode, character.Level, rating,
            timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        matchmakingSignal?.Pulse();
    }

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

public sealed class ArenaMatchmakingService(GameDbContext db, TimeProvider timeProvider,
    IOptions<ArenaOptions>? options = null)
{
    private readonly ArenaOptions _options = options?.Value ?? new ArenaOptions();

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

            Guid[] waitingIds = waiting.Select(x => x.CharacterId).ToArray();
            DateTimeOffset recentSince = timeProvider.GetUtcNow() - _options.RematchCooldown;
            var recentPairs = (await db.ArenaMatches.AsNoTracking()
                    .Where(x => x.StartedAtUtc >= recentSince
                        && waitingIds.Contains(x.CharacterAId) && waitingIds.Contains(x.CharacterBId))
                    .Select(x => new { x.CharacterAId, x.CharacterBId })
                    .ToArrayAsync(cancellationToken))
                .Select(x => PairKey(x.CharacterAId, x.CharacterBId))
                .ToHashSet();

            ArenaQueueEntry? first = null;
            ArenaQueueEntry? second = null;
            foreach (ArenaQueueEntry candidate in waiting.OrderBy(x => x.JoinedAtUtc))
            {
                ArenaQueueEntry? opponent = waiting
                    .Where(x => x.CharacterId != candidate.CharacterId
                        && (mode == ArenaQueueMode.Unranked
                            || !recentPairs.Contains(PairKey(candidate.CharacterId, x.CharacterId)))
                        && (mode == ArenaQueueMode.Unranked
                            ? ArenaMatchRules.CanPairTest(candidate.CharacterId, candidate.Level,
                                x.CharacterId, x.Level)
                            : ArenaMatchRules.CanPair(candidate.CharacterId, candidate.Level,
                                x.CharacterId, x.Level)))
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
                now, ArenaSeason.CurrentId, mode, ArenaProgressionRules.FormulaVersion);
            db.ArenaMatches.Add(match);
            db.ArenaQueueEntries.RemoveRange(first, second);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ArenaMatchCreated(match.Id, match.CharacterAId, match.CharacterBId,
                match.Mode, match.StartedAtUtc);
        });

    private static (Guid, Guid) PairKey(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);
}

public sealed partial class ArenaMatchmakingWorker(
    IServiceScopeFactory scopeFactory,
    ArenaPresenceTracker presence,
    ArenaMatchmakingSignal matchmakingSignal,
    TimeProvider timeProvider,
    IOptions<ArenaOptions> options,
    ILogger<ArenaMatchmakingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ReconciliationInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan OfflineCleanupInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset nextOfflineCleanup = DateTimeOffset.MinValue;
        var firstPass = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!firstPass)
                await matchmakingSignal.WaitAsync(ReconciliationInterval, stoppingToken);
            firstPass = false;

            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<ArenaQueueService>();
                var matchmaking = scope.ServiceProvider.GetRequiredService<ArenaMatchmakingService>();
                var starter = scope.ServiceProvider.GetRequiredService<ArenaMatchStarter>();

                DateTimeOffset now = timeProvider.GetUtcNow();
                if (now >= nextOfflineCleanup)
                {
                    await queue.PurgeOfflineAsync(
                        presence,
                        options.Value.QueueOfflineGrace,
                        stoppingToken);
                    nextOfflineCleanup = now + OfflineCleanupInterval;
                }

                foreach (ArenaQueueMode mode in Enum.GetValues<ArenaQueueMode>())
                {
                    while (await matchmaking.TryCreateMatchAsync(mode, stoppingToken) is { } created)
                        await starter.StartAsync(created, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogMatchmakingFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Arena matchmaking iteration failed.")]
    private static partial void LogMatchmakingFailed(ILogger logger, Exception exception);
}
