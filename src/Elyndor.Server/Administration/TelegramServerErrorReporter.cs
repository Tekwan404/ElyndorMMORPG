using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elyndor.Server.Administration;

public sealed class TelegramServerErrorReporter(
    ITelegramMessageSender messageSender,
    IOptions<TelegramAdminOptions> configuredOptions,
    GameDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<TelegramServerErrorReporter> logger)
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastSentByFingerprint =
        new(StringComparer.Ordinal);
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(1);

    public async Task ReportAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        TelegramAdminOptions options = configuredOptions.Value;
        if (!options.Enabled || options.AllowedUserIds.Length == 0)
            return;

        string fingerprint = string.Join('|',
            exception.GetType().FullName,
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty,
            ShortMessage(exception.Message, 160));
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (LastSentByFingerprint.TryGetValue(fingerprint, out DateTimeOffset previous)
            && now - previous < DuplicateWindow)
        {
            return;
        }
        LastSentByFingerprint[fingerprint] = now;
        PurgeOldFingerprints(now);

        Guid? accountId = TryGetAccountId(context.User);
        Guid? characterId = null;
        if (accountId.HasValue)
        {
            try
            {
                characterId = await dbContext.Characters
                    .AsNoTracking()
                    .Where(character => character.AccountId == accountId.Value)
                    .Select(character => (Guid?)character.Id)
                    .SingleOrDefaultAsync(cancellationToken);
            }
            catch (Exception lookupException)
            {
                logger.LogWarning(
                    lookupException,
                    "Failed to resolve character while preparing Telegram server error alert for {TraceId}.",
                    context.TraceIdentifier);
            }
        }

        string text = BuildMessage(context, exception, now, accountId, characterId);
        foreach (long chatId in options.AllowedUserIds.Distinct().Where(id => id > 0))
        {
            try
            {
                await messageSender.SendAsync(chatId, text, cancellationToken);
            }
            catch (Exception sendException)
            {
                logger.LogWarning(
                    sendException,
                    "Failed to send Telegram server error alert to administrator {TelegramUserId} for {TraceId}.",
                    chatId,
                    context.TraceIdentifier);
            }
        }
    }

    private static string BuildMessage(
        HttpContext context,
        Exception exception,
        DateTimeOffset now,
        Guid? accountId,
        Guid? characterId)
    {
        string account = accountId?.ToString("N") ?? "—";
        string character = characterId?.ToString("N") ?? "—";
        return $"🚨 Elyndor server error\n"
            + $"UTC: {now:yyyy-MM-dd HH:mm:ss}\n"
            + $"Request: {context.Request.Method} {context.Request.Path}\n"
            + $"Error: {exception.GetType().Name}: {ShortMessage(exception.Message, 320)}\n"
            + $"Account: {account}\n"
            + $"Character: {character}\n"
            + $"Trace: {context.TraceIdentifier}";
    }

    private static Guid? TryGetAccountId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid accountId)
        && accountId != Guid.Empty
            ? accountId
            : null;

    private static string ShortMessage(string? value, int maxLength)
    {
        string normalized = string.IsNullOrWhiteSpace(value)
            ? "No exception message"
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "…";
    }

    private static void PurgeOldFingerprints(DateTimeOffset now)
    {
        DateTimeOffset cutoff = now - TimeSpan.FromMinutes(10);
        foreach ((string key, DateTimeOffset sentAt) in LastSentByFingerprint)
        {
            if (sentAt < cutoff)
                LastSentByFingerprint.TryRemove(key, out _);
        }
    }
}
