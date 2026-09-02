using Elyndor.Contracts.Combat;
using Elyndor.Infrastructure.Combat;
using Microsoft.AspNetCore.SignalR;

namespace Elyndor.Server.Combat;

public sealed class SignalRCombatUpdatePublisher(IHubContext<CombatHub> hubContext)
    : ICombatUpdatePublisher
{
    public async Task PublishAsync(
        Guid accountId,
        CombatOperationResult update,
        CancellationToken cancellationToken)
    {
        CombatUpdateResponse response = CombatContractMapper.ToResponse(update);
        IClientProxy client = hubContext.Clients.Group(CombatHub.GroupName(accountId));
        await client.SendAsync("CombatUpdated", response, cancellationToken);
        if (update.Snapshot?.Status != Core.Combat.Sessions.CombatSessionStatus.Active)
            await client.SendAsync("CombatEnded", response, cancellationToken);
    }
}
