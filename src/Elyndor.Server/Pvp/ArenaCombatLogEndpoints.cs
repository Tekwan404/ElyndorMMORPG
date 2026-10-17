using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Elyndor.Core.Combat;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Pvp;
using Elyndor.Server.Administration;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Pvp;

/// <summary>
/// Opt-in arena diagnostics. Reads authoritative events only after a participant's match
/// finishes; the client never submits events or chooses another recipient.
/// </summary>
internal static class ArenaCombatLogEndpoints
{
    private const int MaxExportEvents = 10_000;
    private static readonly SemaphoreSlim DeliveryGate = new(1, 1);
    private static readonly Dictionary<(Guid AccountId, Guid MatchId), DateTimeOffset> Delivered = [];

    public static async Task<IResult> SendAsync(
        Guid matchId,
        ClaimsPrincipal user,
        ArenaMatchRuntime runtime,
        GameDbContext db,
        ITelegramMessageSender telegram,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid accountId)
            || accountId == Guid.Empty)
            return Results.Unauthorized();

        if (matchId == Guid.Empty)
            return Results.BadRequest(new { sent = false, errorCode = "arena_log_match_invalid" });

        // Membership is checked under the runtime's existing single-writer lock.
        ArenaTestState? state = runtime.GetState(accountId, matchId, 0);
        if (state is null)
            return Results.NotFound(new { sent = false, errorCode = "arena_match_not_found" });
        if (state.Status != ArenaTestStatus.Completed || state.Battle is null)
            return Results.Ok(new { sent = false, errorCode = "arena_log_match_active" });

        if (telegram is not ITelegramDocumentSender documents)
            return Results.Ok(new { sent = false, errorCode = "arena_log_sender_unavailable" });

        // Avoid duplicate Telegram documents on client retries and simultaneous terminal updates.
        await DeliveryGate.WaitAsync(cancellationToken);
        try
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-1);
            foreach (var stale in Delivered.Where(item => item.Value < cutoff).Select(item => item.Key).ToArray())
                Delivered.Remove(stale);

            if (Delivered.ContainsKey((accountId, matchId)))
                return Results.Ok(new { sent = true, errorCode = (string?)null });

            long? telegramUserId = await db.Accounts
                .AsNoTracking()
                .Where(account => account.Id == accountId)
                .Select(account => (long?)account.TelegramUserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (telegramUserId is null)
                return Results.Ok(new { sent = false, errorCode = "arena_log_account_not_found" });

            string text = FormatLog(state);
            try
            {
                await documents.SendDocumentAsync(
                    telegramUserId.Value,
                    $"elyndor-arena-{matchId:N}.txt",
                    text,
                    $"⚔️ Elyndor · Арена PvP · {state.Events.Count} событий",
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested)
            {
                return Results.Ok(new { sent = false, errorCode = "arena_log_telegram_failed" });
            }

            Delivered[(accountId, matchId)] = DateTimeOffset.UtcNow;
            return Results.Ok(new { sent = true, errorCode = (string?)null });
        }
        finally
        {
            DeliveryGate.Release();
        }
    }

    internal static string FormatLog(ArenaTestState state)
    {
        if (state.Battle is null)
            throw new ArgumentException("Arena battle is required for a combat report.", nameof(state));

        var actors = new Dictionary<Guid, string>
        {
            [state.Battle.Player.ActorId] = Clean(state.Battle.Player.Name),
            [state.Battle.Enemy.ActorId] = Clean(state.Battle.Enemy.Name)
        };
        CombatEvent[] ordered = state.Events.OrderBy(e => e.Sequence).ToArray();
        int count = Math.Min(ordered.Length, MaxExportEvents);
        string outcome = state.Battle.Status.ToString();
        var builder = new StringBuilder();
        builder.AppendLine("ELYNDOR · ПОЛНЫЙ ЖУРНАЛ PvP АРЕНЫ");
        builder.Append("Матч: ").AppendLine(state.MatchId?.ToString("D") ?? "—");
        builder.Append("Герои: ").Append(actors[state.Battle.Player.ActorId])
            .Append(" vs ").AppendLine(actors[state.Battle.Enemy.ActorId]);
        builder.Append("Результат: ").AppendLine(outcome);
        builder.Append("Всего серверных событий: ").AppendLine(ordered.Length.ToString(CultureInfo.InvariantCulture));
        if (ordered.Length > count)
            builder.Append("В файл включены первые ").Append(count)
                .AppendLine(" событий; остаток не помещается в лимит экспорта.");
        builder.AppendLine("Время UTC · sequence · тип · источник → цель · идентификатор · значения");
        builder.AppendLine("────────────────────────");

        foreach (CombatEvent combatEvent in ordered.Take(count))
        {
            string source = actors.GetValueOrDefault(combatEvent.SourceActorId ?? combatEvent.ActorId, "Система");
            string target = combatEvent.TargetActorId is Guid targetId
                ? actors.GetValueOrDefault(targetId, "Неизвестный участник") : "—";
            builder.Append('#').Append(combatEvent.Sequence.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(combatEvent.OccurredAtUtc.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(" · ").Append(combatEvent.Type)
                .Append(" · ").Append(source).Append(" → ").Append(target)
                .Append(" · ID=").Append(Clean(combatEvent.DefinitionId ?? "—"))
                .Append(" · amount=").Append(combatEvent.Amount.ToString(CultureInfo.InvariantCulture))
                .Append(" · beforeShield=").Append(combatEvent.AmountBeforeShields.ToString(CultureInfo.InvariantCulture));
            if (combatEvent.IsPeriodic)
                builder.Append(" · DoT/HoT");
            if (combatEvent.RawDamage > 0)
                builder.Append(" · raw=").Append(combatEvent.RawDamage.ToString(CultureInfo.InvariantCulture));
            if (combatEvent.DamageAfterMitigation > 0)
                builder.Append(" · mitigated=").Append(combatEvent.DamageAfterMitigation.ToString(CultureInfo.InvariantCulture));
            if (combatEvent.WeaponDefinitionId is not null)
                builder.Append(" · weapon=").Append(Clean(combatEvent.WeaponDefinitionId));
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static string Clean(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ')[..Math.Min(value.Length, 160)];
}
