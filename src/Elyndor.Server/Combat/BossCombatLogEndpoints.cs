using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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

        if (request.Events is null || request.Events.Count == 0)
            return Results.BadRequest(new BossCombatLogResponse(false, "combat_log_empty"));

        if (request.Events.Count > MaxEvents)
            return Results.BadRequest(new BossCombatLogResponse(false, "combat_log_too_large"));

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

        string log = BuildLog(snapshot, request.Events, bossDefinitions);
        string bossName = string.Join(", ", bossDefinitions.Select(boss =>
            boss.DisplayName ?? boss.Name));
        string fileName = $"elyndor-boss-{request.SessionId:N}.txt";
        string caption = $"⚔️ Elyndor · {Sanitize(bossName, 180)} · {request.Events.Count} событий";

        await documentSender.SendDocumentAsync(
            telegramUserId.Value,
            fileName,
            log,
            caption,
            cancellationToken);

        return Results.Ok(new BossCombatLogResponse(true, null));
    }

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
        builder.Append("Событий: ").AppendLine(events.Count.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("────────────────────");

        foreach (BossCombatLogEventRequest combatEvent in events.OrderBy(item => item.Sequence))
        {
            string source = ResolveActorName(combatEvent.SourceActorId ?? combatEvent.ActorId, actorNames);
            string target = combatEvent.TargetActorId is Guid targetActorId
                ? ResolveActorName(targetActorId, actorNames)
                : "—";
            string time = combatEvent.ServerTimeUtc.ToUniversalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

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
            if (combatEvent.Amount != 0 || combatEvent.AmountBeforeShields != 0)
            {
                builder.Append(" · ")
                    .Append(FormatNumber(combatEvent.Amount));
                if (combatEvent.AmountBeforeShields != 0
                    && combatEvent.AmountBeforeShields != combatEvent.Amount)
                {
                    builder.Append(" (до щита ")
                        .Append(FormatNumber(combatEvent.AmountBeforeShields))
                        .Append(')');
                }
            }
            if (!string.IsNullOrWhiteSpace(combatEvent.WeaponHand))
                builder.Append(" · ").Append(Sanitize(combatEvent.WeaponHand, 24));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string ResolveActorName(Guid actorId, Dictionary<Guid, string> names) =>
        names.TryGetValue(actorId, out string? name)
            ? name
            : actorId.ToString("N")[..8];

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

public sealed record BossCombatLogRequest(
    Guid SessionId,
    IReadOnlyList<BossCombatLogEventRequest> Events);

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
