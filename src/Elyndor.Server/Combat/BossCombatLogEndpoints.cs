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

public static class BossCombatLogEndpoints
{
    private const int MaxEvents = 1500;
    private const string CombatRegenDefinitionId = "COMBAT_REGEN";

    public static IEndpointRouteBuilder MapBossCombatLogEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/v1/combat")
            .RequireAuthorization()
            .WithTags("Combat");

        group.MapPost("/boss-log/telegram", SendAsync);
        return endpoints;
    }

    private static async Task<IResult> SendAsync(
        BossCombatLogRequest request,
        ClaimsPrincipal user,
        CombatSessionRegistry registry,
        IContentSnapshotProvider contentProvider,
        GameDbContext dbContext,
        ITelegramMessageSender messageSender,
        CancellationToken cancellationToken)
    {
        if (!TryGetAccountId(user, out Guid accountId))
            return Results.Unauthorized();

        if (request.SessionId == Guid.Empty)
            return Results.BadRequest(new BossCombatLogResponse(false, "combat_log_session_invalid"));

        CombatOperationResult current = registry.Resume(accountId);
        CombatSessionSnapshot? snapshot = current.Snapshot;
        if (!current.Succeeded
            || snapshot is null
            || snapshot.SessionId != request.SessionId)
        {
            return Results.NotFound(new BossCombatLogResponse(false, "combat_log_session_not_found"));
        }

        if (snapshot.Status == CombatSessionStatus.Active)
            return Results.Ok(new BossCombatLogResponse(false, "combat_log_combat_active"));

        GameContentSnapshot content = current.ContentSnapshot ?? contentProvider.GetCurrent();
        CombatActorSnapshot[] enemies = (snapshot.Enemies ?? [snapshot.Enemy]).ToArray();
        MonsterDefinition[] bossDefinitions = enemies
            .Select(enemy => content.Package.Monsters?.FirstOrDefault(monster =>
                string.Equals(monster.Id, enemy.DefinitionId, StringComparison.Ordinal)))
            .Where(monster => monster?.Rank == MonsterRank.Boss)
            .Cast<MonsterDefinition>()
            .ToArray();
        if (bossDefinitions.Length == 0)
            return Results.Ok(new BossCombatLogResponse(false, "combat_log_not_boss"));

        IReadOnlyList<CombatEvent>? authoritativeEvents = null;
        CombatOperationResult historyRead = await registry.ExecuteAsync(
            accountId,
            (session, _) =>
            {
                if (session.SessionId == request.SessionId)
                    authoritativeEvents = session.GetEventsAfter(0);

                // This read runs under the registry session gate. Return an empty event
                // delta so the diagnostic read itself never re-publishes combat history.
                return new CombatCommandResult(
                    session.SessionId == request.SessionId,
                    session.SessionId == request.SessionId ? null : CombatErrorCodes.NotFound,
                    session.Snapshot(),
                    []);
            },
            cancellationToken);

        if (!historyRead.Succeeded || authoritativeEvents is null)
            return Results.NotFound(new BossCombatLogResponse(false, "combat_log_session_not_found"));
        if (authoritativeEvents.Count == 0)
            return Results.BadRequest(new BossCombatLogResponse(false, "combat_log_empty"));
        if (authoritativeEvents.Count > MaxEvents)
            return Results.BadRequest(new BossCombatLogResponse(false, "combat_log_too_large"));

        BossCombatLogEventRequest[] logEvents = authoritativeEvents
            .Select(ToLogEvent)
            .ToArray();

        long? telegramUserId = await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Id == accountId)
            .Select(account => (long?)account.TelegramUserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (telegramUserId is null)
            return Results.NotFound(new BossCombatLogResponse(false, "combat_log_account_not_found"));

        if (messageSender is not ITelegramDocumentSender documentSender)
        {
            throw new InvalidOperationException(
                "Configured Telegram sender does not support document delivery.");
        }

        string log = BuildLog(snapshot, logEvents, bossDefinitions);
        string bossName = string.Join(", ", bossDefinitions.Select(boss =>
            boss.DisplayName ?? boss.Name));
        string fileName = $"elyndor-boss-{request.SessionId:N}.txt";
        string caption = $"⚔️ Elyndor · {Sanitize(bossName, 180)} · {logEvents.Length} событий";

        await documentSender.SendDocumentAsync(
            telegramUserId.Value,
            fileName,
            log,
            caption,
            cancellationToken);

        return Results.Ok(new BossCombatLogResponse(true, null));
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
        combatEvent.WeaponDefinitionId);

    private static string BuildLog(
        CombatSessionSnapshot snapshot,
        IReadOnlyList<BossCombatLogEventRequest> events,
        IReadOnlyList<MonsterDefinition> bosses)
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

        string bossName = string.Join(", ", bosses.Select(boss =>
            boss.DisplayName ?? boss.Name));
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
        builder.AppendLine("Примечание: «входящий» урон — значение до shield absorption и ограничения остатком HP; реальный щит имеет отдельное событие ShieldAbsorbed.");
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

            WriteEvent(builder, combatEvent, actorNames);
        }

        return builder.ToString();
    }

    private static void WriteEvent(
        StringBuilder builder,
        BossCombatLogEventRequest combatEvent,
        Dictionary<Guid, string> actorNames)
    {
        string source = ResolveActorName(combatEvent.SourceActorId ?? combatEvent.ActorId, actorNames);
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

        bool alwaysWriteAmount = string.Equals(
            combatEvent.Type,
            "ResourceChanged",
            StringComparison.Ordinal);
        if (alwaysWriteAmount || combatEvent.Amount != 0 || combatEvent.AmountBeforeShields != 0)
        {
            builder.Append(" · ")
                .Append(FormatNumber(combatEvent.Amount));
            if (combatEvent.AmountBeforeShields != 0
                && combatEvent.AmountBeforeShields != combatEvent.Amount)
            {
                builder.Append(" (входящий ")
                    .Append(FormatNumber(combatEvent.AmountBeforeShields))
                    .Append(')');
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
        string source = ResolveActorName(first.SourceActorId ?? first.ActorId, actorNames);
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
            builder.Append("–").Append(FormatTime(last.ServerTimeUtc));

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
        string.Equals(combatEvent.Type, "ResourceChanged", StringComparison.Ordinal)
        && string.Equals(combatEvent.DefinitionId, CombatRegenDefinitionId, StringComparison.Ordinal);

    private static bool CanMergeCombatRegen(
        BossCombatLogEventRequest previous,
        BossCombatLogEventRequest next) =>
        IsCombatRegen(previous)
        && IsCombatRegen(next)
        && next.Sequence == previous.Sequence + 1
        && next.ActorId == previous.ActorId
        && next.SourceActorId == previous.SourceActorId
        && next.TargetActorId == previous.TargetActorId;

    private static int CountCombatRegenGroups(IReadOnlyList<BossCombatLogEventRequest> events)
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
            if (!sameGroup) groups++;
            previousWasMergeableRegen = true;
            previous = combatEvent;
        }
        return groups;
    }

    private static long[] FindMissingSequences(IReadOnlyList<BossCombatLogEventRequest> events)
    {
        if (events.Count < 2) return [];

        List<long> missing = [];
        long previous = events[0].Sequence;
        for (int index = 1; index < events.Count; index++)
        {
            long current = events[index].Sequence;
            for (long sequence = previous + 1; sequence < current; sequence++)
            {
                missing.Add(sequence);
                if (missing.Count >= 100) return missing.ToArray();
            }
            previous = Math.Max(previous, current);
        }
        return missing.ToArray();
    }

    private static string FormatMissingSequences(IReadOnlyList<long> missing)
    {
        if (missing.Count == 0) return "нет";
        string values = string.Join(", ", missing.Take(20));
        return missing.Count <= 20
            ? values
            : $"{values}, … (не менее {missing.Count})";
    }

    private static string ResolveActorName(Guid actorId, Dictionary<Guid, string> names) =>
        names.TryGetValue(actorId, out string? name)
            ? name
            : actorId.ToString("N")[..8];

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static string FormatNumber(decimal value) =>
        decimal.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

    private static string Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "—";
        string sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength];
    }

    private static bool TryGetAccountId(ClaimsPrincipal user, out Guid accountId) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out accountId)
        && accountId != Guid.Empty;
}

public sealed record BossCombatLogRequest(Guid SessionId);

public sealed record BossCombatLogEventRequest(
    long Sequence,
    string Type,
    Guid ActorId,
    Guid? SourceActorId,
    Guid? TargetActorId,
    string? DefinitionId,
    decimal Amount,
    decimal AmountBeforeShields,
    DateTimeOffset ServerTimeUtc,
    string? WeaponHand = null,
    string? WeaponDefinitionId = null);

public sealed record BossCombatLogResponse(bool Sent, string? ErrorCode);
