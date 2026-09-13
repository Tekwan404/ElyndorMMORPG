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
            return Results.BadRequest(
                new BossCombatLogResponse(false, "combat_log_session_invalid"));

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

        BossCombatLogResponse result = await BossCombatLogArchive.SendAsync(
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

        return Results.Ok(result);
    }

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}

internal static class BossCombatLogArchive
{
    private const int MaxEvents = 1500;
    private const int MaxSessions = 256;
    private const string CombatRegenDefinitionId = "COMBAT_REGEN";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private static readonly ConcurrentDictionary<ArchiveKey, ArchiveEntry> Entries = [];

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

        ArchiveEntry entry = Entries.GetOrAdd(
            new ArchiveKey(accountId, snapshot.SessionId),
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
            MonsterDefinition[] bossDefinitions = enemies
                .Select(enemy => content.Package.Monsters?.FirstOrDefault(monster =>
                    string.Equals(
                        monster.Id,
                        enemy.DefinitionId,
                        StringComparison.Ordinal)))
                .Where(monster => monster?.Rank == MonsterRank.Boss)
                .Cast<MonsterDefinition>()
                .ToArray();

            if (bossDefinitions.Length == 0)
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
                logger.LogError(
                    "Boss combat log sender does not support documents for session {SessionId}.",
                    sessionId);
                return new BossCombatLogResponse(
                    false,
                    "combat_log_sender_unavailable");
            }

            BossCombatLogEventRequest[] logEvents = events
                .Select(ToLogEvent)
                .OrderBy(item => item.Sequence)
                .ToArray();

            string bossName = string.Join(
                ", ",
                bossDefinitions.Select(boss => boss.DisplayName ?? boss.Name));
            string log = BuildLog(
                snapshot,
                logEvents,
                bossDefinitions,
                droppedEvents);
            string fileName = $"elyndor-boss-{sessionId:N}.txt";
            string caption =
                $"⚔️ Elyndor · {Sanitize(bossName, 180)} · {logEvents.Length} событий";

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
                logger.LogError(
                    exception,
                    "Failed to send boss combat log {SessionId} for account {AccountId}; "
                    + "the archived log is retained for retry.",
                    sessionId,
                    accountId);
                return new BossCombatLogResponse(
                    false,
                    "combat_log_telegram_failed");
            }

            lock (entry.Gate)
            {
                entry.Sent = true;
                entry.UpdatedAtUtc = nowUtc;
            }

            logger.LogInformation(
                "Sent boss combat log {SessionId} for account {AccountId} "
                + "with {EventCount} events ({DroppedEventCount} dropped from archive).",
                sessionId,
                accountId,
                logEvents.Length,
                droppedEvents);

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
                long first = entry.Events.Keys.First();
                entry.Events.Remove(first);
                entry.DroppedEvents++;
            }

            entry.UpdatedAtUtc = capturedAtUtc;
        }
    }

    private static void Purge(DateTimeOffset nowUtc)
    {
        foreach ((ArchiveKey key, ArchiveEntry entry) in Entries)
        {
            DateTimeOffset updatedAt;
            lock (entry.Gate)
                updatedAt = entry.UpdatedAtUtc;

            if (nowUtc - updatedAt > Lifetime)
                Entries.TryRemove(key, out _);
        }

        int overflow = Entries.Count - MaxSessions;
        if (overflow <= 0)
            return;

        ArchiveKey[] oldest = Entries
            .Select(pair =>
            {
                DateTimeOffset updatedAt;
                lock (pair.Value.Gate)
                    updatedAt = pair.Value.UpdatedAtUtc;
                return (pair.Key, UpdatedAtUtc: updatedAt);
            })
            .OrderBy(item => item.UpdatedAtUtc)
            .Take(overflow)
            .Select(item => item.Key)
            .ToArray();

        foreach (ArchiveKey key in oldest)
            Entries.TryRemove(key, out _);
    }

    private static BossCombatLogEventRequest ToLogEvent(CombatEvent combatEvent) => new(
        combatEvent.Sequence,
        combatEvent.Type.ToString(),
        combatEvent.ActorId,
        combatEvent.SourceActorId,
        combatEvent.TargetActorId,
        combatEvent.DefinitionId,
        combatEvent.Amount,
        combatEvent.AmountBeforeShields,
        combatEvent.OccurredAtUtc,
        combatEvent.WeaponHand?.ToString(),
        combatEvent.WeaponDefinitionId,
        combatEvent.RawDamage,
        combatEvent.DamageAfterMitigation,
        combatEvent.DamageBeforeBlock);

    private static string BuildLog(
        CombatSessionSnapshot snapshot,
        IReadOnlyList<BossCombatLogEventRequest> events,
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

        BossCombatLogEventRequest[] ordered = events
            .OrderBy(item => item.Sequence)
            .ToArray();
        long[] missingSequences = FindMissingSequences(ordered);
        int rawRegenEvents = ordered.Count(IsCombatRegen);
        int compactedRegenGroups = CountCombatRegenGroups(ordered);

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

        StringBuilder builder = new();
        builder.AppendLine("⚔️ ELYNDOR · ЛОГ БОЯ С БОССОМ");
        builder.Append("Босс: ").AppendLine(bossName);
        builder.Append("Результат: ").AppendLine(result);
        builder.Append("Сессия: ").AppendLine(snapshot.SessionId.ToString("D"));
        builder.Append("Контент: ").Append(snapshot.ContentVersion)
            .Append(" · баланс: ").AppendLine(snapshot.BalanceVersion);
        builder.Append("Событий: ")
            .AppendLine(ordered.Length.ToString(CultureInfo.InvariantCulture));
        if (droppedEvents > 0)
        {
            builder.Append("Архив ограничен: отброшено старых событий: ")
                .AppendLine(droppedEvents.ToString(CultureInfo.InvariantCulture));
        }

        if (ordered.Length > 0)
        {
            builder.Append("Sequence: #")
                .Append(ordered[0].Sequence.ToString(CultureInfo.InvariantCulture))
                .Append("–#")
                .AppendLine(ordered[^1].Sequence.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append("Пропуски sequence: ")
            .AppendLine(FormatMissingSequences(missingSequences));
        if (rawRegenEvents > 0)
        {
            builder.Append("COMBAT_REGEN: ")
                .Append(rawRegenEvents.ToString(CultureInfo.InvariantCulture))
                .Append(" событий → ")
                .Append(compactedRegenGroups.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" строк (соседние тики объединены)");
        }

        builder.AppendLine(
            "Примечание: DamageBlocked — блок экипированным щитом; "
            + "ShieldAbsorbed — поглощение временным эффектом/барьером. "
            + "Для блока щитом ниже печатается цепочка "
            + "raw → armor → block → barrier → HP.");
        builder.AppendLine("────────────────────");

        for (int index = 0; index < ordered.Length; index++)
        {
            BossCombatLogEventRequest combatEvent = ordered[index];
            if (IsCombatRegen(combatEvent))
            {
                int end = index;
                decimal total = combatEvent.Amount;
                while (end + 1 < ordered.Length
                    && CanMergeCombatRegen(ordered[end], ordered[end + 1]))
                {
                    end++;
                    total += ordered[end].Amount;
                }

                WriteCombatRegenGroup(
                    builder,
                    ordered[index],
                    ordered[end],
                    total,
                    end - index + 1,
                    actorNames);
                index = end;
                continue;
            }

            if (string.Equals(
                    combatEvent.Type,
                    nameof(CombatEventType.DamageBlocked),
                    StringComparison.Ordinal))
            {
                WriteBlockBreakdown(builder, ordered, index, actorNames);
                continue;
            }

            WriteEvent(builder, combatEvent, actorNames);
        }

        return builder.ToString();
    }

    private static void WriteBlockBreakdown(
        StringBuilder builder,
        BossCombatLogEventRequest[] events,
        int blockIndex,
        Dictionary<Guid, string> actorNames)
    {
        BossCombatLogEventRequest blockEvent = events[blockIndex];
        string source = ResolveActorName(
            blockEvent.SourceActorId ?? blockEvent.ActorId,
            actorNames);
        string target = blockEvent.TargetActorId is Guid targetActorId
            ? ResolveActorName(targetActorId, actorNames)
            : "—";

        BossCombatLogEventRequest? damageEvent = null;
        decimal barrierAbsorbed = 0;
        for (int index = blockIndex + 1;
             index < events.Length && index <= blockIndex + 4;
             index++)
        {
            BossCombatLogEventRequest candidate = events[index];
            if (candidate.ServerTimeUtc != blockEvent.ServerTimeUtc
                || candidate.SourceActorId != blockEvent.SourceActorId
                || candidate.TargetActorId != blockEvent.TargetActorId)
            {
                continue;
            }

            if (string.Equals(
                    candidate.Type,
                    nameof(CombatEventType.ShieldAbsorbed),
                    StringComparison.Ordinal))
            {
                barrierAbsorbed += candidate.Amount;
                continue;
            }

            if (string.Equals(
                    candidate.Type,
                    nameof(CombatEventType.DamageDealt),
                    StringComparison.Ordinal))
            {
                damageEvent = candidate;
                break;
            }
        }

        decimal beforeBlock = blockEvent.DamageBeforeBlock > 0
            ? blockEvent.DamageBeforeBlock
            : blockEvent.Amount + blockEvent.AmountBeforeShields;
        decimal raw = blockEvent.RawDamage > 0
            ? blockEvent.RawDamage
            : beforeBlock;
        decimal afterMitigation = blockEvent.DamageAfterMitigation > 0
            ? blockEvent.DamageAfterMitigation
            : beforeBlock;
        decimal received = damageEvent?.Amount
            ?? Math.Max(0, blockEvent.AmountBeforeShields - barrierAbsorbed);

        builder.Append('#')
            .Append(blockEvent.Sequence.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(FormatTime(blockEvent.ServerTimeUtc))
            .Append(" · BLOCK · ")
            .Append(source)
            .Append(" → ")
            .Append(target)
            .Append(" · наносит ")
            .Append(FormatNumber(raw))
            .Append(" → после брони: ")
            .Append(FormatNumber(afterMitigation));

        if (beforeBlock != afterMitigation)
        {
            builder.Append(" → после модификаторов: ")
                .Append(FormatNumber(beforeBlock));
        }

        builder.Append(" → щит блокирует ")
            .Append(FormatNumber(blockEvent.Amount));
        if (barrierAbsorbed > 0)
        {
            builder.Append(" → барьер поглощает ")
                .Append(FormatNumber(barrierAbsorbed));
        }

        builder.Append(" → получено ")
            .Append(FormatNumber(received));
        if (received <= 0)
            builder.Append(" · ПОЛНЫЙ БЛОК");
        builder.AppendLine();
    }

    private static void WriteEvent(
        StringBuilder builder,
        BossCombatLogEventRequest combatEvent,
        Dictionary<Guid, string> actorNames)
    {
        string source = ResolveActorName(
            combatEvent.SourceActorId ?? combatEvent.ActorId,
            actorNames);
        string target = combatEvent.TargetActorId is Guid targetActorId
            ? ResolveActorName(targetActorId, actorNames)
            : "—";
        string time = FormatTime(combatEvent.ServerTimeUtc);

        builder.Append('#')
            .Append(combatEvent.Sequence.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(time)
            .Append(" · ")
            .Append(Sanitize(combatEvent.Type, 48))
            .Append(" · ")
            .Append(source);

        if (combatEvent.TargetActorId is not null)
            builder.Append(" → ").Append(target);
        if (!string.IsNullOrWhiteSpace(combatEvent.DefinitionId))
            builder.Append(" · ").Append(Sanitize(combatEvent.DefinitionId, 80));

        bool isShieldAbsorb = string.Equals(
            combatEvent.Type,
            nameof(CombatEventType.ShieldAbsorbed),
            StringComparison.Ordinal);
        if (isShieldAbsorb)
        {
            builder.Append(" · absorbed=")
                .Append(FormatNumber(combatEvent.Amount));
        }
        else
        {
            bool alwaysWriteAmount = string.Equals(
                combatEvent.Type,
                nameof(CombatEventType.ResourceChanged),
                StringComparison.Ordinal);
            if (alwaysWriteAmount
                || combatEvent.Amount != 0
                || combatEvent.AmountBeforeShields != 0)
            {
                builder.Append(" · ")
                    .Append(FormatNumber(combatEvent.Amount));
                if (combatEvent.AmountBeforeShields != 0
                    && combatEvent.AmountBeforeShields != combatEvent.Amount)
                {
                    builder.Append(" (до barrier/HP cap ")
                        .Append(FormatNumber(combatEvent.AmountBeforeShields))
                        .Append(')');
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(combatEvent.WeaponHand))
            builder.Append(" · ").Append(Sanitize(combatEvent.WeaponHand, 24));
        builder.AppendLine();
    }

    private static void WriteCombatRegenGroup(
        StringBuilder builder,
        BossCombatLogEventRequest first,
        BossCombatLogEventRequest last,
        decimal total,
        int count,
        Dictionary<Guid, string> actorNames)
    {
        string source = ResolveActorName(
            first.SourceActorId ?? first.ActorId,
            actorNames);
        string target = first.TargetActorId is Guid targetActorId
            ? ResolveActorName(targetActorId, actorNames)
            : "—";

        builder.Append('#')
            .Append(first.Sequence.ToString(CultureInfo.InvariantCulture));
        if (last.Sequence != first.Sequence)
        {
            builder.Append("–#")
                .Append(last.Sequence.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(' ')
            .Append(FormatTime(first.ServerTimeUtc));
        if (last.ServerTimeUtc != first.ServerTimeUtc)
            builder.Append('–').Append(FormatTime(last.ServerTimeUtc));

        builder.Append(" · ResourceChanged · ")
            .Append(source);
        if (first.TargetActorId is not null)
            builder.Append(" → ").Append(target);
        builder.Append(" · COMBAT_REGEN · ")
            .Append(FormatNumber(total));
        if (count > 1)
        {
            builder.Append(" · ")
                .Append(count.ToString(CultureInfo.InvariantCulture))
                .Append(" тика");
        }

        builder.AppendLine();
    }

    private static bool IsCombatRegen(BossCombatLogEventRequest combatEvent) =>
        string.Equals(
            combatEvent.Type,
            nameof(CombatEventType.ResourceChanged),
            StringComparison.Ordinal)
        && string.Equals(
            combatEvent.DefinitionId,
            CombatRegenDefinitionId,
            StringComparison.Ordinal);

    private static bool CanMergeCombatRegen(
        BossCombatLogEventRequest previous,
        BossCombatLogEventRequest next) =>
        IsCombatRegen(previous)
        && IsCombatRegen(next)
        && next.Sequence == previous.Sequence + 1
        && next.ActorId == previous.ActorId
        && next.SourceActorId == previous.SourceActorId
        && next.TargetActorId == previous.TargetActorId;

    private static int CountCombatRegenGroups(BossCombatLogEventRequest[] events)
    {
        int groups = 0;
        bool previousWasMergeableRegen = false;
        BossCombatLogEventRequest? previous = null;
        foreach (BossCombatLogEventRequest combatEvent in events)
        {
            if (!IsCombatRegen(combatEvent))
            {
                previousWasMergeableRegen = false;
                previous = combatEvent;
                continue;
            }

            bool sameGroup = previousWasMergeableRegen
                && previous is not null
                && CanMergeCombatRegen(previous, combatEvent);
            if (!sameGroup)
                groups++;
            previousWasMergeableRegen = true;
            previous = combatEvent;
        }

        return groups;
    }

    private static long[] FindMissingSequences(BossCombatLogEventRequest[] events)
    {
        if (events.Length == 0)
            return [];

        List<long> missing = [];
        long previous = 0;
        foreach (BossCombatLogEventRequest combatEvent in events)
        {
            long current = combatEvent.Sequence;
            for (long sequence = previous + 1; sequence < current; sequence++)
            {
                missing.Add(sequence);
                if (missing.Count >= 100)
                    return missing.ToArray();
            }

            previous = Math.Max(previous, current);
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
        Dictionary<Guid, string> names) =>
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
