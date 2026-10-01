using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Pvp;
using Elyndor.Server.Administration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Server.Pvp;

public sealed partial class ArenaInvitationNotifier(GameDbContext db, ITelegramMessageSender telegram,
    IHubContext<ArenaHub> hub, ILogger<ArenaInvitationNotifier> logger)
{
    public async Task NotifyAsync(ArenaInvitationView invite, bool sendTelegram)
    {
        // Invitation state is already committed; delivery must never fail the mutation response.
        try { await NotifyCoreAsync(invite, sendTelegram); }
        catch (Exception exception) { LogFailure(logger, invite.Id, exception); }
    }

    private async Task NotifyCoreAsync(ArenaInvitationView invite, bool sendTelegram)
    {
        Guid[] ids = [invite.InviterCharacterId, invite.TargetCharacterId];
        var recipients = await (from character in db.Characters.AsNoTracking()
                                join account in db.Accounts on character.AccountId equals account.Id
                                where ids.Contains(character.Id)
                                select new { CharacterId = character.Id, AccountId = account.Id, account.TelegramUserId })
            .ToArrayAsync();
        foreach (var recipient in recipients)
        {
            try
            {
                await hub.Clients.Group(ArenaHub.GroupName(recipient.AccountId))
                    .SendAsync("ArenaInvitesChanged");
            }
            catch (Exception exception) { LogFailure(logger, invite.Id, exception); }
        }
        var target = recipients.SingleOrDefault(x => x.CharacterId == invite.TargetCharacterId);
        if (!sendTelegram || target is null || target.TelegramUserId <= 0) return;
        try
        {
            string text = $"⚔️ {invite.InviterName} приглашает вас на дружескую арену 1×1 в Elyndor.\n\nБез рейтинга и чести. Приглашение действует 5 минут.";
            string url = $"https://elyndor.su/world?arenaInvite={invite.Id:D}";
            if (telegram is ITelegramWebAppMessageSender webApp)
                await webApp.SendWebAppAsync(target.TelegramUserId, text, "⚔️ Открыть приглашение", url, CancellationToken.None);
            else await telegram.SendAsync(target.TelegramUserId, $"{text}\n\nОткройте игру → Арена.", CancellationToken.None);
        }
        catch (Exception exception) { LogFailure(logger, invite.Id, exception); }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Arena invitation {InviteId} notification failed.")]
    private static partial void LogFailure(ILogger logger, Guid inviteId, Exception exception);
}
