using Elyndor.Contracts.Combat;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Combat;
using Microsoft.AspNetCore.SignalR;

namespace Elyndor.Server.Combat;

public sealed class SignalRCombatUpdatePublisher(
    IHubContext<CombatHub> hubContext,
    IContentSnapshotProvider contentProvider,
    TimeProvider timeProvider) : ICombatUpdatePublisher
{
    public async Task PublishAsync(
        Guid accountId,
        CombatOperationResult update,
        CancellationToken cancellationToken)
    {
        // Keep a short-lived server-side copy of combat history before the client is told
        // that the fight ended. A following fight is then free to clear the runtime session
        // without racing boss-log delivery.
        BossCombatLogArchive.Capture(accountId, update, timeProvider.GetUtcNow());

        CombatUpdateResponse response = CombatContractMapper.ToResponse(update, contentProvider.GetCurrent().Package);
        IClientProxy client = hubContext.Clients.Group(CombatHub.GroupName(accountId));
        await client.SendAsync("CombatUpdated", response, cancellationToken);
        if (update.Snapshot?.Status != Core.Combat.Sessions.CombatSessionStatus.Active)
            await client.SendAsync("CombatEnded", response, cancellationToken);
    }
}
