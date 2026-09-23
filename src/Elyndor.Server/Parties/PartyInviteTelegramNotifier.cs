using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Parties;

public sealed class PartyInviteTelegramNotifier(
    GameDbContext dbContext,
    ITelegramMessageSender messageSender,
    ILogger<PartyInviteTelegramNotifier> logger)
{
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

            await messageSender.SendAsync(
                telegramUserId.Value,
                $"👥 {displayName} приглашает вас в группу в Elyndor.\n\nОткройте игру и примите приглашение. Оно действует 5 минут.",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The invite is already committed. Request cancellation must not turn it into a failed mutation.
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to send Telegram party invite notification for invite {InviteId} to character {TargetCharacterId}.",
                invite.Id,
                invite.TargetCharacterId);
        }
    }
}
