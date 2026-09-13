using System.Collections.Concurrent;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Server.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Elyndor.Server.Combat;

public static class BossCombatLogArchiveEndpoints
{
    public static IEndpointRouteBuilder MapBossCombatLogArchiveEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/combat")
            .RequireAuthorization()
            .WithTags("Combat");

        group.MapPost("/boss-log/telegram-v2", SendAsync);
        return endpoints;
    }

    private static async Task<IResult> SendAsync(
        BossCombatLogRequest request,
        ClaimsPrincipal user,
        CombatSessionRegistry registry,
        IContentSnapshotProvider contentProvider,
        GameDbContext dbContext,
        ITelegramMessageSender messageSender,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        if (request.SessionId == Guid.Empty)
        {
            return Results.BadRequest(
                new BossCombatLogResponse(false, "combat_log_session_invalid"));
        }

        CombatSessionSnapshot? snapshot = null;
        IReadOnlyList<CombatEvent>? events = null;
        GameContentSnapshot? contentSnapshot = null;

        CombatOperationResult current = registry.Resume(accountId);
        if (current.Succeeded
            && current.Snapshot is not null
            && current.Snapshot.SessionId == request.SessionId
            && current.Snapshot.Status != CombatSessionStatus.Active)
        {
            snapshot = current.Snapshot;
            contentSnapshot = current.ContentSnapshot;

            CombatOperationResult historyRead = await registry.ExecuteAsync(
                accountId,
                (session, pinnedContent, _) =>
                {
                    if (session.SessionId == request.SessionId)
                    {
                        events = session.GetEventsAfter(0);
                        contentSnapshot ??= pinnedContent;
                    }

                    return new CombatCommandResult(
                        session.SessionId == request.SessionId,
                        session.SessionId == request.SessionId
                            ? null
                            : CombatErrorCodes.NotFound,
                        session.Snapshot(),
                        []);
                },
                cancellationToken);

            if (!historyRead.Succeeded)
            {
                snapshot = null;
                events = null;
                contentSnapshot = null;
            }
        }

        BossCombatLogResponse response = await BossCombatLogArchive.SendAsync(
            accountId,
            request.SessionId,
            snapshot,
            events,
            contentSnapshot,
            contentProvider,
            dbContext,
            messageSender,
            loggerFactory.CreateLogger("Elyndor.BossCombatLog"),
            timeProvider.GetUtcNow(),
            cancellationToken);

        return Results.Ok(response);
    }

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}

internal static class BossCombatLogArchive
{
    private const int MaxEvents = 1500;
    private const int MaxSessions = 256;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private static readonly ConcurrentDictionary<ArchiveKey, ArchiveEntry> Entries = [];

    private static readonly Action<ILogger, Guid, Exception?> DocumentSenderUnavailable =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2301, nameof(DocumentSenderUnavailable)),
            "Boss combat log sender does not support documents for session {SessionId}.");

    private static readonly Action<ILogger, Guid, Guid, Exception?> DeliveryFailed =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Error,
            new EventId(2302, nameof(DeliveryFailed)),
            "Failed to send boss combat log {SessionId} for account {AccountId}; "
            + "the archived log is retained for retry.");

    private static readonly Action<ILogger, Guid, Guid, int, int, Exception?> DeliverySucceeded =
        LoggerMessage.Define<Guid, Guid, int, int>(
            LogLevel.Information,
            new EventId(2303, nameof(DeliverySucceeded)),
            "Sent boss combat log {SessionId} for account {AccountId} "
            + "with {EventCount} events ({DroppedEventCount} dropped from archive).");

    public static void Capture(
        Guid accountId,
        CombatOperationResult update,
        DateTimeOffset capturedAtUtc)
    {
        CombatSessionSnapshot? snapshot = update.Snapshot;
        if (accountId == Guid.Empty
            || snapshot is null
            || snapshot.SessionId == Guid.Empty)
        {
            return;
        }

        ArchiveKey key = new(accountId, snapshot.SessionId);
        ArchiveEntry entry = Entries.GetOrAdd(
            key,
            _ => new ArchiveEntry(snapshot, update.ContentSnapshot, capturedAtUtc));

        Merge(entry, snapshot, update.Events, update.ContentSnapshot, capturedAtUtc);
        Purge(capturedAtUtc);
    }

    public static async Task<BossCombatLogResponse> SendAsync(
        Guid accountId,
        Guid sessionId,
        CombatSessionSnapshot? authoritativeSnapshot,
        IReadOnlyList<CombatEvent>? authoritativeEvents,
        GameContentSnapshot? authoritativeContent,
        IContentSnapshotProvider contentProvider,
        GameDbContext dbContext,
        ITelegramMessageSender messageSender,
        ILogger logger,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Purge(nowUtc);

        ArchiveKey key = new(accountId, sessionId);
        ArchiveEntry? entry = null;

        if (authoritativeSnapshot is not null
            && authoritativeSnapshot.SessionId == sessionId)
        {
            entry = Entries.GetOrAdd(
                key,
                _ => new ArchiveEntry(
                    authoritativeSnapshot,
                    authoritativeContent,
                    nowUtc));
            Merge(
                entry,
                authoritativeSnapshot,
                authoritativeEvents ?? [],
                authoritativeContent,
                nowUtc);
        }
        else
        {
            Entries.TryGetValue(key, out entry);
        }

        if (entry is null)
            return new BossCombatLogResponse(false, "combat_log_session_not_found");

        await entry.SendGate.WaitAsync(cancellationToken);
        try
        {
            CombatSessionSnapshot snapshot;
            CombatEvent[] events;
            GameContentSnapshot? archivedContent;
            int droppedEvents;
            bool alreadySent;

            lock (entry.Gate)
            {
                snapshot = entry.Snapshot;
                events = entry.Events.Values.ToArray();
                archivedContent = entry.ContentSnapshot;
                droppedEvents = entry.DroppedEvents;
                alreadySent = entry.Sent;
                entry.UpdatedAtUtc = nowUtc;
            }

            if (alreadySent)
                return new BossCombatLogResponse(true, null);

            if (snapshot.Status == CombatSessionStatus.Active)
                return new BossCombatLogResponse(false, "combat_log_combat_active");

            GameContentSnapshot content =
                archivedContent ?? contentProvider.GetCurrent();
            CombatActorSnapshot[] enemies =
                (snapshot.Enemies ?? [snapshot.Enemy]).ToArray();
            MonsterDefinition[] bosses = enemies
                .Select(enemy => content.Package.Monsters?.FirstOrDefault(monster =>
                    string.Equals(
                        monster.Id,
                        enemy.DefinitionId,
                        StringComparison.Ordinal)))
                .Where(monster => monster?.Rank == MonsterRank.Boss)
                .Cast<MonsterDefinition>()
                .ToArray();

            if (bosses.Length == 0)
                return new BossCombatLogResponse(false, "combat_log_not_boss");

            if (events.Length == 0)
                return new BossCombatLogResponse(false, "combat_log_empty");

            long? telegramUserId = await dbContext.Accounts
                .AsNoTracking()
                .Where(account => account.Id == accountId)
                .Select(account => (long?)account.TelegramUserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (telegramUserId is null)
            {
                return new BossCombatLogResponse(
                    false,
                    "combat_log_account_not_found");
            }

            if (messageSender is not ITelegramDocumentSender documentSender)
            {
                DocumentSenderUnavailable(logger, sessionId, null);
                return new BossCombatLogResponse(
                    false,
                    "combat_log_sender_unavailable");
            }

            CombatEvent[] ordered = events
                .OrderBy(combatEvent => combatEvent.Sequence)
                .ToArray();
            string bossName = string.Join(
                ", ",
                bosses.Select(boss => boss.DisplayName ?? boss.Name));
            string log = BuildLog(snapshot, ordered, bosses, droppedEvents);
            string fileName = $"elyndor-boss-{sessionId:N}.txt";
            string caption =
                $"⚔️ Elyndor · {Sanitize(bossName, 180)} · {ordered.Length} событий";

            try
            {
                await documentSender.SendDocumentAsync(
                    telegramUserId.Value,
                    fileName,
                    log,
                    caption,
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested)
            {
                DeliveryFailed(logger, sessionId, accountId, exception);
                return new BossCombatLogResponse(
                    false,
                    "combat_log_telegram_failed");
            }

            lock (entry.Gate)
            {
                entry.Sent = true;
                entry.UpdatedAtUtc = nowUtc;
            }

            DeliverySucceeded(
                logger,
                sessionId,
                accountId,
                ordered.Length,
                droppedEvents,
                null);

            return new BossCombatLogResponse(true, null);
        }
        finally
        {
            entry.SendGate.Release();
        }
    }

    private static void Merge(
        ArchiveEntry entry,
        CombatSessionSnapshot snapshot,
        IReadOnlyList<CombatEvent> events,
        GameContentSnapshot? contentSnapshot,
        DateTimeOffset capturedAtUtc)
    {
        lock (entry.Gate)
        {
            entry.Snapshot = snapshot;
            if (contentSnapshot is not null)
                entry.ContentSnapshot = contentSnapshot;

            foreach (CombatEvent combatEvent in events)
                entry.Events[combatEvent.Sequence] = combatEvent;

            while (entry.Events.Count > MaxEvents)
            {
                long firstSequence = entry.Events.Keys.First();
                entry.Events.Remove(firstSequence);
                entry.DroppedEvents++;
            }

            entry.UpdatedAtUtc = capturedAtUtc;
        }
    }

    private static void Purge(DateTimeOffset nowUtc)
    {
        foreach (KeyValuePair<ArchiveKey, ArchiveEntry> pair in Entries)
        {
            DateTimeOffset updatedAtUtc;
            lock (pair.Value.Gate)
                updatedAtUtc = pair.Value.UpdatedAtUtc;

            if (nowUtc - updatedAtUtc > Lifetime)
                Entries.TryRemove(pair.Key, out _);
        }

        int overflow = Entries.Count - MaxSessions;
        if (overflow <= 0)
            return;

        ArchiveKey[] oldest = Entries
            .Select(pair =>
            {
                DateTimeOffset updatedAtUtc;
                lock (pair.Value.Gate)
                    updatedAtUtc = pair.Value.UpdatedAtUtc;
                return (pair.Key, UpdatedAtUtc: updatedAtUtc);
            })
            .OrderBy(item => item.UpdatedAtUtc)
            .Take(overflow)
            .Select(item => item.Key)
            .ToArray();

        foreach (ArchiveKey key in oldest)
            Entries.TryRemove(key, out _);
    }

    private static string BuildLog(
        CombatSessionSnapshot snapshot,
        IReadOnlyList<CombatEvent> events,
        IReadOnlyList<MonsterDefinition> bosses,
        int droppedEvents)
    {
        Dictionary<Guid, string> actorNames = new();
        foreach (CombatActorSnapshot actor in snapshot.Players ?? [snapshot.Player])
            actorNames[actor.ActorId] = actor.Name;
        foreach (CombatActorSnapshot actor in snapshot.Enemies ?? [snapshot.Enemy])
            actorNames[actor.ActorId] = actor.Name;
        if (snapshot.Companion is not null)
            actorNames[snapshot.Companion.ActorId] = snapshot.Companion.Name;

        string bossName = string.Join(
            ", ",
            bosses.Select(boss => boss.DisplayName ?? boss.Name));
        string result = snapshot.Status switch
        {
            CombatSessionStatus.Victory => "ПОБЕДА",
            CombatSessionStatus.Defeat => "ПОРАЖЕНИЕ",
            CombatSessionStatus.Cancelled => "ОТМЕНЁН",
            _ => snapshot.Status.ToString().ToUpperInvariant()
        };

        long[] missingSequences = FindMissingSequences(events);

        StringBuilder builder = new();
        builder.AppendLine("⚔️ ELYNDOR · ЛОГ БОЯ С БОССОМ");
        builder.Append("Босс: ").AppendLine(bossName);
        builder.Append("Результат: ").AppendLine(result);
        builder.Append("Сессия: ").AppendLine(snapshot.SessionId.ToString("D"));
        builder.Append("Контент: ").Append(snapshot.ContentVersion)
            .Append(" · баланс: ").AppendLine(snapshot.BalanceVersion);
        builder.Append("Событий: ")
            .AppendLine(events.Count.ToString(CultureInfo.InvariantCulture));

        if (events.Count > 0)
        {
            builder.Append("Sequence: #")
                .Append(events[0].Sequence.ToString(CultureInfo.InvariantCulture))
                .Append("–#")
                .AppendLine(events[^1].Sequence.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append("Пропуски sequence: ")
            .AppendLine(FormatMissingSequences(missingSequences));

        if (droppedEvents > 0)
        {
            builder.Append("Архив ограничен: отброшено старых событий: ")
                .AppendLine(droppedEvents.ToString(CultureInfo.InvariantCulture));
        }

        builder.AppendLine("────────────────────");

        foreach (CombatEvent combatEvent in events)
            WriteEvent(builder, combatEvent, actorNames);

        return builder.ToString();
    }

    private static void WriteEvent(
        StringBuilder builder,
        CombatEvent combatEvent,
        Dictionary<Guid, string> actorNames)
    {
        Guid sourceActorId = combatEvent.SourceActorId ?? combatEvent.ActorId;
        string source = ResolveActorName(sourceActorId, actorNames);
        string target = combatEvent.TargetActorId is Guid targetActorId
            ? ResolveActorName(targetActorId, actorNames)
            : "—";

        builder.Append('#')
            .Append(combatEvent.Sequence.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(FormatTime(combatEvent.OccurredAtUtc))
            .Append(" · ")
            .Append(combatEvent.Type)
            .Append(" · ")
            .Append(source);

        if (combatEvent.TargetActorId is not null)
            builder.Append(" → ").Append(target);
        if (!string.IsNullOrWhiteSpace(combatEvent.DefinitionId))
            builder.Append(" · ").Append(Sanitize(combatEvent.DefinitionId, 80));

        if (combatEvent.Amount != 0
            || combatEvent.AmountBeforeShields != 0
            || combatEvent.RawDamage != 0
            || combatEvent.DamageAfterMitigation != 0
            || combatEvent.DamageBeforeBlock != 0)
        {
            builder.Append(" · amount=").Append(FormatNumber(combatEvent.Amount));

            if (combatEvent.AmountBeforeShields != 0)
            {
                builder.Append(" · beforeShields=")
                    .Append(FormatNumber(combatEvent.AmountBeforeShields));
            }

            if (combatEvent.RawDamage != 0)
                builder.Append(" · raw=").Append(FormatNumber(combatEvent.RawDamage));
            if (combatEvent.DamageAfterMitigation != 0)
            {
                builder.Append(" · afterArmor=")
                    .Append(FormatNumber(combatEvent.DamageAfterMitigation));
            }

            if (combatEvent.DamageBeforeBlock != 0)
            {
                builder.Append(" · beforeBlock=")
                    .Append(FormatNumber(combatEvent.DamageBeforeBlock));
            }
        }

        if (combatEvent.WeaponHand is not null)
            builder.Append(" · ").Append(combatEvent.WeaponHand);
        if (!string.IsNullOrWhiteSpace(combatEvent.WeaponDefinitionId))
        {
            builder.Append(" · weapon=")
                .Append(Sanitize(combatEvent.WeaponDefinitionId, 80));
        }

        builder.AppendLine();
    }

    private static long[] FindMissingSequences(IReadOnlyList<CombatEvent> events)
    {
        if (events.Count == 0)
            return [];

        List<long> missing = [];
        long previous = 0;
        foreach (CombatEvent combatEvent in events)
        {
            for (long sequence = previous + 1;
                 sequence < combatEvent.Sequence;
                 sequence++)
            {
                missing.Add(sequence);
                if (missing.Count >= 100)
                    return missing.ToArray();
            }

            previous = Math.Max(previous, combatEvent.Sequence);
        }

        return missing.ToArray();
    }

    private static string FormatMissingSequences(long[] missing)
    {
        if (missing.Length == 0)
            return "нет";

        string values = string.Join(", ", missing.Take(20));
        return missing.Length <= 20
            ? values
            : $"{values}, … (не менее {missing.Length})";
    }

    private static string ResolveActorName(
        Guid actorId,
        IReadOnlyDictionary<Guid, string> names) =>
        names.TryGetValue(actorId, out string? name)
            ? name
            : actorId.ToString("N")[..8];

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime()
            .ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static string FormatNumber(decimal value) =>
        decimal.Round(value, 2)
            .ToString("0.##", CultureInfo.InvariantCulture);

    private static string Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";

        string sanitized = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return sanitized.Length <= maxLength
            ? sanitized
            : sanitized[..maxLength];
    }

    private readonly record struct ArchiveKey(Guid AccountId, Guid SessionId);

    private sealed class ArchiveEntry(
        CombatSessionSnapshot snapshot,
        GameContentSnapshot? contentSnapshot,
        DateTimeOffset updatedAtUtc)
    {
        public object Gate { get; } = new();
        public SemaphoreSlim SendGate { get; } = new(1, 1);
        public SortedDictionary<long, CombatEvent> Events { get; } = [];
        public CombatSessionSnapshot Snapshot { get; set; } = snapshot;
        public GameContentSnapshot? ContentSnapshot { get; set; } = contentSnapshot;
        public DateTimeOffset UpdatedAtUtc { get; set; } = updatedAtUtc;
        public int DroppedEvents { get; set; }
        public bool Sent { get; set; }
    }
}
