using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Server.Administration;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Parties;

public sealed class PartyInviteTelegramNotifier(
    GameDbContext dbContext,
    ITelegramMessageSender messageSender,
    ILogger<PartyInviteTelegramNotifier> logger)
{
    private const string PartyInviteBaseUrl = "https://elyndor.su/world";
    private const string AcceptButtonText = "✅ Принять и войти";

    private static readonly Action<ILogger, Guid, Guid, Exception?> FailedToSendNotification =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(FailedToSendNotification)),
            "Failed to send Telegram party invite notification for invite {InviteId} to character {TargetCharacterId}.");

    public async Task NotifyAsync(
        PartyInviteView invite,
        CancellationToken cancellationToken)
    {
        try
        {
            Guid? targetAccountId = await dbContext.Characters
                .AsNoTracking()
                .Where(character => character.Id == invite.TargetCharacterId)
                .Select(character => (Guid?)character.AccountId)
                .SingleOrDefaultAsync(cancellationToken);
            if (targetAccountId is null)
                return;

            long? telegramUserId = await dbContext.Accounts
                .AsNoTracking()
                .Where(account => account.Id == targetAccountId.Value)
                .Select(account => (long?)account.TelegramUserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (telegramUserId is null or <= 0)
                return;

            string? inviterName = await dbContext.Characters
                .AsNoTracking()
                .Where(character => character.Id == invite.InviterCharacterId)
                .Select(character => character.Name)
                .SingleOrDefaultAsync(cancellationToken);
            string displayName = string.IsNullOrWhiteSpace(inviterName)
                ? "Игрок"
                : inviterName;
            string text = $"👥 {displayName} приглашает вас в группу в Elyndor.\n\nПриглашение действует 5 минут.";

            if (messageSender is ITelegramWebAppMessageSender webAppMessageSender)
            {
                string webAppUrl = $"{PartyInviteBaseUrl}?partyInvite={invite.Id:D}";
                await webAppMessageSender.SendWebAppAsync(
                    telegramUserId.Value,
                    text,
                    AcceptButtonText,
                    webAppUrl,
                    cancellationToken);
                return;
            }

            await messageSender.SendAsync(
                telegramUserId.Value,
                $"{text}\n\nОткройте игру и примите приглашение.",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The invite is already committed. Request cancellation must not turn it into a failed mutation.
        }
        catch (Exception exception)
        {
            FailedToSendNotification(
                logger,
                invite.Id,
                invite.TargetCharacterId,
                exception);
        }
    }
}
